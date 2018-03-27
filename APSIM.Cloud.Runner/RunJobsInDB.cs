// -----------------------------------------------------------------------
// <copyright file="CheckDBForJobs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Runner
{
    using APSIM.Cloud.Shared;
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB
    {
        private List<Exception> errors = new List<Exception>();
        private bool cancel = false;
        private Dictionary<string, string> appSettings;
        private string nameOfCurrentJob;

        /// <summary>Constructor</summary>
        /// <param name="runner"></param>
        /// <param name="appSettings">Application settings</param>
        /// <param name="archiveLocation">Folder to ftp or copy final .zip file to</param>
        public RunJobsInDB(Dictionary<string, string> appSettings)
        {
            this.appSettings = appSettings;
        }

        /// <summary></summary>
        public void Start()
        {
            // Run all jobs on background thread
            Task t = Task.Run(() => JobRunnerThread());
        }

        /// <summary>Stop running jobs</summary>
        public void Stop()
        {
            cancel = true;
        }

        /// <summary>Main DoWork method for the task thread.</summary>
        private void JobRunnerThread()
        { 
            IJobManager nextJob = null;
            while (!cancel)
            {
                try
                {
                    nextJob = GetJobFromServer();

                    if (nextJob != null)
                    {
                        // Run the job.
                        IJobRunner runner = new JobRunnerAsync();
                        runner.Run(nextJob, 
                                   wait: true, 
                                   numberOfProcessors: Convert.ToInt32(appSettings["MaximumNumberOfCores"]));

                        // Tell the server we've finished the job.
                        UpdateServerForCompletedJob(nextJob);
                    }
                }
                catch (Exception err)
                {
                    WriteToLog(err.Message);
                }
            }
        }

        /// <summary>
        /// Get the next job from the server.
        /// </summary>
        /// <returns></returns>
        private IJobManager GetJobFromServer()
        {
            IJobManager nextJob = null;
            while (nextJob == null && !cancel)
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    JobsService.Job runningJobDescription = jobsClient.GetNextToRun();

                    if (runningJobDescription != null)
                    {
                        nameOfCurrentJob = runningJobDescription.Name;
                        string jobXML = jobsClient.GetJobXML(nameOfCurrentJob);

                        if (IsF4PJob(runningJobDescription.Name) == true)
                        {
                            RuntimeEnvironment environment = new RuntimeEnvironment
                            {
                                AusfarmRevision = appSettings["AusfarmRevision"],
                            };
                            nextJob = new RunF4PJob(jobXML, environment);
                        }
                        else
                        {
                            RuntimeEnvironment environment = new RuntimeEnvironment
                            {
                                APSIMRevision = appSettings["APSIMRevision"],
                                RuntimePackage = appSettings["RuntimePackage"],
                            };
                            nextJob = new RunYPJob(jobXML, environment);
                        }
                    }
                    else
                    {
                        // No jobs to run so wait a bit.
                        Thread.Sleep(5 * 1000); // 5 sec.
                    }
                }
            }

            return nextJob;
        }

        private static bool IsF4PJob(string jobName)
        {
            return jobName.EndsWith("_F4P", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// The current job has completed - update server.
        /// </summary>
        /// <param name="jobManager"></param>
        private void UpdateServerForCompletedJob(IJobManager runningJob)
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
                        foreach (string err in (runningJob as RunYPJob).Errors)
                            errorMessage += err;
                    }
                    else
                    {
                        (runningJob as RunF4PJob).AllFilesZipped.Seek(0, SeekOrigin.Begin);
                        (runningJob as RunF4PJob).AllFilesZipped.CopyTo(s);
                        outputs = (runningJob as RunF4PJob).Outputs;
                        foreach (string err in (runningJob as RunF4PJob).Errors)
                            errorMessage += err;
                    }
                }

                string archiveLocation = appSettings["ArchiveFolder"];
                if (archiveLocation.StartsWith("ftp://"))
                    FTPClient.Upload(zipFileName, archiveLocation + "/" + nameOfCurrentJob + ".zip", usernamepwd[0], usernamepwd[1]);
                else
                    File.Copy(zipFileName, archiveLocation);
                File.Delete(zipFileName);

                if (appSettings["CallStoreReport"] == "true")
                {
                    if (runningJob is RunYPJob)
                    {
                        // YieldProphet - StoreReport
                        // validation runs have a report name of the year e.g. 2015. 
                        // Don't need to call StoreReport for them.
                        using (YPReporting.ReportingClient ypClient = new YPReporting.ReportingClient())
                        {
                            try
                            {
                                ypClient.StoreReport(nameOfCurrentJob, outputs);
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
                                f4pClient.StoreReport(nameOfCurrentJob, dataSet);
                            }
                            catch (Exception err)
                            {
                                throw new Exception("Cannot call F4P StoreReport web service method: " + err.Message);
                            }
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
                if (errorMessage != null)
                    errorMessage = errorMessage.Replace("'", "");
                jobsClient.SetCompleted(nameOfCurrentJob, errorMessage);
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
