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
    using RunnableJobs;

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : JobManager.IRunnable
    {
        private JobManager.IRunnable runningJob = null;
        JobsService.Job runningJobDescription = null;

        /// <summary>Called to start the job.</summary>
        /// <param name="jobManager">Job manager</param>
        /// <param name="worker">Background worker</param>
        public void Run(JobManager jobManager, BackgroundWorker worker)
        {
            while (!worker.CancellationPending)
            {
                try
                {
                    // Process that have been added.
                    ProcessAddedJobs(jobManager);

                    Thread.Sleep(1000); // 1 seconds
                }
                catch (Exception err)
                {
                    WriteToLog(err.Message);
                }
            }
        }

        /// <summary>Processes any jobs that have been added.</summary>
        /// <param name="jobManager">The job manager.</param>
        private void ProcessAddedJobs(JobManager jobManager)
        {
            // Has the previous job completed? If so, signal back to server.
            if (runningJob != null && jobManager.IsJobCompleted(runningJob))
                UpdateServerForCompletedJob(jobManager, runningJob as IJob);

            //If there is no YP job running, then add a new job.  Once a YP job is running don't add any more jobs (regardless of type).
            if (runningJob == null || jobManager.IsJobCompleted(runningJob))
            {
                // Remove completed jobs if nothing is running. Otherwise, completedjobs will
                // grow and grow.
                jobManager.ClearCompletedJobs();
                runningJob = null;

                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    runningJobDescription = jobsClient.GetNextToRun();

                    if (runningJobDescription != null)
                    {
                        string jobXML = jobsClient.GetJobXML(runningJobDescription.Name);

                        if (IsF4PJob(runningJobDescription.Name) == true)
                            runningJob = new RunnableJobs.RunF4PJob(jobXML, runningJobDescription.Name);
                        else
                            runningJob = new RunnableJobs.RunYPJob(jobXML);
                        if (runningJob != null)
                            jobManager.AddJob(runningJob);
                    }
                    else
                    {
                        // No jobs to run so wait a bit.
                        Thread.Sleep(5 * 1000); // 5 sec.
                    }
                }
            }
        }

        private static bool IsF4PJob(string jobName)
        {
            return jobName.EndsWith("_F4P", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// The current job has completed - update server.
        /// </summary>
        /// <param name="jobManager"></param>
        private void UpdateServerForCompletedJob(JobManager jobManager, IJob job)
        {
            string errorMessage = null;

            try
            {
                string pwdFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ftpuserpwd.txt");
                if (!File.Exists(pwdFile))
                    throw new Exception("Cannot find file: " + pwdFile);

                string[] usernamepwd = File.ReadAllText(pwdFile).Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                string zipFileName = Path.GetTempFileName();
                using (var s = File.Create(zipFileName))
                {
                    job.AllFilesZipped.Seek(0, SeekOrigin.Begin);
                    job.AllFilesZipped.CopyTo(s);
                }

                string archiveLocation = @"ftp://bob.apsim.info/APSIM.Cloud.Archive";
                FTPClient.Upload(zipFileName, archiveLocation + "/" + runningJobDescription.Name + ".zip", usernamepwd[0], usernamepwd[1]);
                File.Delete(zipFileName);

                bool callStoreReport = runningJob is RunF4PJob ||
                    (runningJob is RunYPJob && (runningJob as RunYPJob).Spec.CallYPServerOnComplete);

                if (callStoreReport)
                {
                    // YieldProphet - StoreReport
                    // validation runs have a report name of the year e.g. 2015. 
                    // Don't need to call StoreReport for them.
                    using (YPReporting.ReportingClient ypClient = new YPReporting.ReportingClient())
                    {
                        try
                        {
                            ypClient.StoreReport(runningJobDescription.Name, job.Outputs);
                        }
                        catch (Exception)
                        {
                            throw new Exception("Cannot call YP StoreReport web service method");
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
                foreach (Exception err in jobManager.Errors(runningJob))
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
