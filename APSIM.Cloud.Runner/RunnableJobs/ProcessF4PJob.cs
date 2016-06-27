// -----------------------------------------------------------------------
// <copyright file="ProcessYPJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.IO;
    using System.Xml;
    using System.Reflection;
    using System.Data;
    using System.Threading;
    using APSIM.Cloud.Shared;
    using APSIM.Cloud.Shared.AusFarm;
    using APSIM.Shared.Utilities;
    using APSIM.Shared.OldAPSIM;
    //using ApsimFile;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class ProcessF4PJob : ProcessJob
    {
       
        /// <summary>Constructor</summary>
        /// <param name="runAPSIM">Run APSIM?</param>
        public ProcessF4PJob(bool runAPSIM)
            : base(runAPSIM)
        {
        }

        /// <summary>Create a runnable job for the F4P simulations</summary>
        /// <param name="FilesToRun">The files to run.</param>
        /// <param name="ApsimExecutable">APSIM.exe path. Can be null.</param>
        /// <returns>A runnable job for all simulations</returns>
        protected override JobManager.IRunnable CreateRunnableJob(string jobName, string jobXML, string workingDirectory, string ApsimExecutable)
        {
            // Create a sequential job.
            JobSequence completeJob = new JobSequence();
            completeJob.Jobs = new List<JobManager.IRunnable>();

            List<JobManager.IRunnable> jobs = new List<JobManager.IRunnable>();

            // Save f4p.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(jobXML);
            doc.Save(Path.Combine(workingDirectory, "f4p.xml"));

            Farm4Prophet spec = Farm4ProphetUtility.Farm4ProphetFromXML(jobXML);
            List<AusFarmSpec> simulations = Farm4ProphetToAusFarm.ToAusFarm(spec);

            //writes the sdml files to the workingDirectory and returns a list of names
            string[] files = AusFarmFiles.Create(simulations, workingDirectory);

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            //for each simulation
            for (int i = 0; i < files.Length; i++)
            {
                RunnableJobs.AusFarmJob job = new RunnableJobs.AusFarmJob(
                 jobName: jobName,
                 fileName: Path.Combine(workingDirectory, files[i]),
                 arguments: "");
                jobs.Add(job);
            }

            completeJob.Jobs.Add(new JobParallel() { Jobs = jobs });

            return completeJob;
        }

    }
}
