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

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class CheckDBForJobs : Utility.JobManager.IRunnable
    {
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            JobsDB DB = new JobsDB();
            DB.Open();
            Utility.JobManager jobManager = (Utility.JobManager)e.Argument;

            // Check for previous jobs that have already been added to the queue
            // or are already running. Change their status to PendingAdd so that
            // they are resubmitted below.
            List<JobsDB.JobDB> existingJobs = new List<JobsDB.JobDB>();
            existingJobs.AddRange(DB.Get(JobsDB.StatusEnum.Added));
            existingJobs.AddRange(DB.Get(JobsDB.StatusEnum.Running));
            foreach (JobsDB.JobDB existingJob in existingJobs)
                DB.SetJobStatus(existingJob.Name, JobsDB.StatusEnum.PendingAdd);

            while (!e.Cancel)
            {

                try
                {
                    if (!DB.IsOpen)
                        DB.Open();

                    // Check for jobs to delete.
                    foreach (JobsDB.JobDB existingJob in DB.Get(JobsDB.StatusEnum.PendingDelete))
                        DB.Delete(existingJob.Name);

                    foreach (JobsDB.JobDB newJob in DB.Get(JobsDB.StatusEnum.PendingAdd))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(newJob.XML);

                        RunAPSIMJob yp = Utility.Xml.Deserialise(doc.DocumentElement) as RunAPSIMJob;
                        if (yp == null)
                        {
                            DB.AddLogMessage("Invalid job XML found. Job: " + newJob.Name + ". Job - ignored", true);
                            DB.SetJobStatus(newJob.Name, JobsDB.StatusEnum.Error);
                        }
                        else
                        {
                            yp.Name = newJob.Name;
                            jobManager.AddJob(yp);
                            DB.SetJobStatus(newJob.Name, JobsDB.StatusEnum.Added);
                        }
                    }                   
                }
                catch (Exception err)
                {
                    DB.AddLogMessage(err.Message, true);
                    Thread.Sleep(1000 * 60 * 5); // 5 minutes
                }
            }
        }
    }
}
