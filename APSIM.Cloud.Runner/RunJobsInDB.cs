// -----------------------------------------------------------------------
// <copyright file="CheckDBForJobs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Runner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using APSIM.Cloud;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Diagnostics;
    using APSIM.Shared.Utilities;
    using System.ComponentModel;
    using APSIM.Cloud.Shared;

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : IJobManager
    {
        private IJobManager runningJob = null;
        private JobsService.Job runningJobDescription = null;
        private IJobRunner runner;
        private List<Exception> errors = new List<Exception>();
        private bool cancel = false;

        /// <summary>Constructor</summary>
        /// <param name="runner"></param>
        public RunJobsInDB(IJobRunner jobRunner)
        {
            runner = jobRunner;
            runner.JobCompleted += OnJobCompleted;
        }

        /// <summary>Stop running jobs</summary>
        public void Stop()
        {
            cancel = true;
        }

        /// <summary>Called to get the next job</summary>
        public IRunnable GetNextJobToRun()
        {
            IRunnable nextJob = null;
            while (nextJob == null && !cancel)
            {
                try
                {
                    // Has the previous job completed? If so, signal back to server.
                    if (runningJob != null)
                    {
                        nextJob = runningJob.GetNextJobToRun();
                        if (nextJob == null)
                        {
                            // Current job has finished.
                            UpdateServerForCompletedJob();
                            errors.Clear();
                            runningJob = null;
                        }
                    }

                    // If there is no YP job running, then add a new job.  Once a YP job is running don't add any more jobs (regardless of type).
                    if (runningJob == null)
                    {
                        using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                        {
                            runningJobDescription = jobsClient.GetNextToRun();

                            if (runningJobDescription != null)
                            {
                                string jobXML = jobsClient.GetJobXML(runningJobDescription.Name);

                                if (IsF4PJob(runningJobDescription.Name) == true)
                                {
                                    RuntimeEnvironment environment = new RuntimeEnvironment
                                    {
                                        AusfarmRevision = "AusFarm-1.4.12",
                                    };
                                    runningJob = new RunF4PJob(jobXML, environment);
                                }
                                else
                                {
                                    RuntimeEnvironment environment = new RuntimeEnvironment
                                    {
                                        APSIMRevision = "Apsim7.8-R4000",
                                    };
                                    runningJob = new RunYPJob(jobXML, environment);
                                }

                                nextJob = runningJob.GetNextJobToRun();
                            }
                        }
                    }

                    if (nextJob == null)
                    {
                        // No jobs to run so wait a bit.
                        Thread.Sleep(5 * 1000); // 5 sec.
                    }
                }
                catch (Exception err)
                {
                    WriteToLog(err.Message);
                }
            }

            return nextJob;
        }

        private void OnJobCompleted(object sender, JobCompleteArgs e)
        {
            if (e.exceptionThrowByJob != null)
                errors.Add(e.exceptionThrowByJob);
        }

        /// <summary>Called when all jobs completed</summary>
        public void Completed()
        {
            throw new NotImplementedException();
        }

        private static bool IsF4PJob(string jobName)
        {
            return jobName.EndsWith("_F4P", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// The current job has completed - update server.
        /// </summary>
        /// <param name="jobManager"></param>
        private void UpdateServerForCompletedJob()
        {
            string errorMessage = null;

            try
            {
                string pwdFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ftpuserpwd.txt");
                if (!File.Exists(pwdFile))
                    throw new Exception("Cannot find file: " + pwdFile);

                string[] usernamepwd = File.ReadAllText(pwdFile).Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                string zipFileName = Path.GetTempFileName();
                DataSet outputs;
                using (var s = File.Create(zipFileName))
                {
                    if (runningJob is RunYPJob)
                    {
                        (runningJob as RunYPJob).AllFilesZipped.Seek(0, SeekOrigin.Begin);
                        (runningJob as RunYPJob).AllFilesZipped.CopyTo(s);
                        outputs = (runningJob as RunYPJob).Outputs;
                    }
                    else
                    {
                        (runningJob as RunF4PJob).AllFilesZipped.Seek(0, SeekOrigin.Begin);
                        (runningJob as RunF4PJob).AllFilesZipped.CopyTo(s);
                        outputs = (runningJob as RunF4PJob).Outputs;
                    }
                }

                string archiveLocation = @"ftp://bob.apsim.info/APSIM.Cloud.Archive";
                FTPClient.Upload(zipFileName, archiveLocation + "/" + runningJobDescription.Name + ".zip", usernamepwd[0], usernamepwd[1]);
                File.Delete(zipFileName);

                if (runningJob is RunYPJob)
                {
                    // YieldProphet - StoreReport
                    // validation runs have a report name of the year e.g. 2015. 
                    // Don't need to call StoreReport for them.
                    using (YPReporting.ReportingClient ypClient = new YPReporting.ReportingClient())
                    {
                        try
                        {
                            ypClient.StoreReport(runningJobDescription.Name, outputs);
                        }
                        catch (Exception err)
                        {
                            throw new Exception("Cannot call YP StoreReport web service method: " + err.Message);
                        }
                    }
                }
                else if (runningJob is RunF4PJob)
                { 
                    RunF4PJob f4pJob = runningJob as RunF4PJob;

                    DataSet dataSet = new DataSet();
                    foreach (DataTable table in f4pJob.Outputs.Tables)
                    {
                        // Don't send the cropping daily and monthly files
                        if (table.TableName.EndsWith("_daily.txt") == false && 
                            table.TableName.EndsWith("_monthly.txt") == false)
                            dataSet.Tables.Add(table);
                    }

                        // Call Farm4Prophet web service.
                    using (F4P.F4PClient f4pClient = new F4P.F4PClient())
                    {
                        try
                        {
                            f4pClient.StoreReport(runningJobDescription.Name, dataSet);
                        }
                        catch (Exception err)
                        {
                            throw new Exception("Cannot call F4P StoreReport web service method: " + err.Message);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                errorMessage = err.ToString();
            }

            using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
            {
                foreach (Exception err in errors)
                    errorMessage += err.ToString();
                if (errorMessage != null)
                    errorMessage = errorMessage.Replace("'", "");
                jobsClient.SetCompleted(runningJobDescription.Name, errorMessage);
            }
        }

        /// <summary>
        /// Writes to log.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        private void WriteToLog(string errorMessage)
        {
            try
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    jobsClient.AddLogMessage(errorMessage, true);
                }
                Thread.Sleep(1000);  // 1 sec.
            }
            catch (Exception)
            {
                // Network must be down - wait 5 minutes
                Thread.Sleep(1000 * 60 * 5);
            }
        }

    }
}
