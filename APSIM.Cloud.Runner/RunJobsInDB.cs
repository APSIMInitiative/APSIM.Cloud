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

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }

        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>The current running job description.</summary>
        private JobsService.Job runningJobDescription = null;

        /// <summary>The current running job.</summary>
        private JobManager.IRunnable runningJob = null;

        /// <summary>Entry point for this job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Get our job manager.
            JobManager jobManager = (JobManager)e.Argument;

            while (!e.Cancel)
            {
                try
                {
                    // Process jobs that have finished.
                    ProcessCompletedJobs();

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
            if (runningJobDescription == null)
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    runningJobDescription = jobsClient.GetNextToRun();
                }

                if (runningJobDescription != null)
                {
                    runningJob = new RunnableJobs.ProcessYPJob(true) { JobName = runningJobDescription.Name };
                    jobManager.AddJob(runningJob);
                }
                else
                {
                    // No jobs to run so wait a bit.
                    Thread.Sleep(30 * 1000); // 30 sec.
                }
            }
        }

        /// <summary>Processes all completed jobs.</summary>
        private void ProcessCompletedJobs()
        {
            if (runningJob != null && runningJob.IsCompleted)
            {
                runningJob = null;
                runningJobDescription = null;
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
