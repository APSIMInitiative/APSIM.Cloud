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
    using System.ComponentModel;

    //using ApsimFile;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class RunF4PJob : JobManager.IRunnable, IJob
    {
        private string specXML;
        private string jobName;

        /// <summary>Constructor</summary>
        /// <param name="xml">Job specification xml.</param>
        /// <param name="nameOfJob">Name of the job</param>
        public RunF4PJob(string xml, string nameOfJob)
        {
            specXML = xml;
            jobName = nameOfJob;
        }

        /// <summary>Get all generated outputs</summary>
        public DataSet Outputs { get; private set; }

        /// <summary>Output zip file.</summary>
        public Stream AllFilesZipped { get; private set; }

        /// <summary>Called to start the job.</summary>
        /// <param name="jobManager">Job manager</param>
        /// <param name="worker">Background worker</param>
        public void Run(JobManager jobManager, BackgroundWorker worker)
        {
            // Create a working directory.
            string workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            // Create a sequential job.
            JobSequence completeJob = new JobSequence();
            completeJob.Jobs = new List<JobManager.IRunnable>();

            // Save f4p.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(specXML);
            doc.Save(Path.Combine(workingDirectory, "f4p.xml"));

            Farm4Prophet spec = Farm4ProphetUtility.Farm4ProphetFromXML(specXML);
            List<AusFarmSpec> simulations = Farm4ProphetToAusFarm.ToAusFarm(spec);

            //writes the sdml files to the workingDirectory and returns a list of names
            string[] files = AusFarmFiles.Create(simulations, workingDirectory);

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            //for each simulation
            JobParallel simJobs = new JobParallel();
            for (int i = 0; i < files.Length; i++)
            {
                RunnableJobs.AusFarmJob job = new RunnableJobs.AusFarmJob(
                 jobName: jobName,
                 fileName: Path.Combine(workingDirectory, files[i]),
                 arguments: "");
                simJobs.Jobs.Add(job);
            }

            jobManager.AddChildJob(this, simJobs);

            // Wait for it to be completed.
            while (!jobManager.IsJobCompleted(simJobs))
                Thread.Sleep(200);

            // Perform cleanup and get outputs.
            Outputs = APSIMPostSimulation.PerformCleanup(workingDirectory);

            // Zip the temporary directory
            AllFilesZipped = new MemoryStream();
            ZipUtilities.ZipFiles(Directory.GetFiles(workingDirectory), null, AllFilesZipped);

            // Get rid of our temporary directory.
            Directory.Delete(workingDirectory, true);
        }

    }
}
