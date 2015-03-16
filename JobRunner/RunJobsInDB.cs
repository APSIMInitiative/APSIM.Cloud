// -----------------------------------------------------------------------
// <copyright file="CheckDBForJobs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.JobRunner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using APSIM.Cloud.Services;
    using APSIM.Cloud.Services.Specification;

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : Utility.JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }

        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>List of running jobs (name/job pairs).</summary>
        private List<KeyValuePair<string, Utility.JobManager.IRunnable>> runningJobs = new List<KeyValuePair<string, Utility.JobManager.IRunnable>>();

        /// <summary>The jobs database</summary>
        private JobsDB jobsDB = new JobsDB();

        /// <summary>Entry point for this job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Get our job manager.
            Utility.JobManager jobManager = (Utility.JobManager)e.Argument;

            // Open the DB.
            jobsDB.Open();

            // There could be jobs that have a status of running if there was a 
            // crash of this program while they were running. If jobs like this
            // are found then change their status to Added so that they are re-run.
            foreach (JobsDB.JobDB existingJob in jobsDB.Get(JobsDB.StatusEnum.Running))
                jobsDB.SetJobStatus(existingJob.Name, JobsDB.StatusEnum.Added);

            while (!e.Cancel)
            {
                try
                {
                    // Check for jobs to delete.
                    foreach (JobsDB.JobDB existingJob in jobsDB.Get(JobsDB.StatusEnum.Deleting))
                        jobsDB.Delete(existingJob.Name);

                    // Process jobs that have finished.
                    ProcessCompletedJobs();

                    // Process that have been added.
                    ProcessAddedJobs(jobManager);

                    Thread.Sleep(1000); // 1 seconds
                }
                catch (Exception err)
                {
                    do
                    {
                        if (jobsDB.IsOpen)
                        {
                            jobsDB.AddLogMessage(err.Message, true);
                            Thread.Sleep(1000); // 1 seconds
                        }
                        else
                        {
                            Thread.Sleep(1000 * 60 * 5); // 5 minutes
                            try
                            {
                                jobsDB.Open();
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    while (!jobsDB.IsOpen);                    
                }
            }
        }

        /// <summary>Processes any jobs that have been added.</summary>
        /// <param name="jobManager">The job manager.</param>
        private void ProcessAddedJobs(Utility.JobManager jobManager)
        {
            foreach (JobsDB.JobDB newJob in jobsDB.Get(JobsDB.StatusEnum.Added))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(newJob.XML);

                Utility.JobManager.IRunnable job = GetRunnableJob(newJob.Name, doc.DocumentElement);

                if (job == null)
                {
                    jobsDB.AddLogMessage("Invalid job XML found. Job: " + newJob.Name + ". Job - ignored", true);
                    jobsDB.SetJobStatus(newJob.Name, JobsDB.StatusEnum.Error);
                    jobsDB.SetJobErrorText(newJob.Name, "Invalid job XML found.");
                }
                else
                {
                    runningJobs.Add(new KeyValuePair<string, Utility.JobManager.IRunnable>(newJob.Name, job));
                    jobManager.AddJob(job);
                    jobsDB.SetJobStatus(newJob.Name, JobsDB.StatusEnum.Running);
                }
            }
        }

        /// <summary>Processes all completed jobs.</summary>
        private void ProcessCompletedJobs()
        {
            for (int i = 0; i < runningJobs.Count; i++)
            {
                if (runningJobs[i].Value.IsCompleted)
                {
                    if (runningJobs[i].Value.ErrorMessage != null)
                    {
                        jobsDB.SetJobStatus(runningJobs[i].Key, JobsDB.StatusEnum.Error);
                        jobsDB.SetJobErrorText(runningJobs[i].Key, runningJobs[i].Value.ErrorMessage);
                    }
                    else
                        jobsDB.SetJobStatus(runningJobs[i].Key, JobsDB.StatusEnum.Completed);

                    // remove item from runningjobs.
                    runningJobs.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>Gets a runnable job from the specified XML.</summary>
        /// <param name="name">The name of the job.</param>
        /// <param name="node">The job XML node</param>
        /// <returns>The newly created runnable job or NULL if not valid job XML.</returns>
        private static Utility.JobManager.IRunnable GetRunnableJob(string name, XmlNode node)
        {
            if (node.Name == "YieldProphetSpec")
                return YieldProphetServices.Run(node.OuterXml, null);

            return null;
        }

    }
}
