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

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : JobManager.IRunnable
    {
        private JobManager.IRunnable runningJob = null;

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
            JobsService.Job runningJobDescription = null;

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
                }

                if (runningJobDescription != null)
                {
                    if (RunnableJobs.ProcessYPJob.IsF4PJob(runningJobDescription.Name) == true)
                        runningJob = new RunnableJobs.ProcessF4PJob(true) { JobName = runningJobDescription.Name };
                    else
                        runningJob = new RunnableJobs.ProcessYPJob(true) { JobName = runningJobDescription.Name };
                    if (runningJob != null)
                        jobManager.AddJob(runningJob);
                }
                else
                {
                    // No jobs to run so wait a bit.
                    Thread.Sleep(15 * 1000); // 15 sec.
                }
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
