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
    public class RunYPJob : JobManager.IRunnable, IJob
    {
        private YieldProphet specToRun;
        private string specXMLToRun;
        private List<APSIMSpec> simulationsToRun;

        /// <summary>Constructor</summary>
        /// <param name="yp">The YieldProphet spec to run</param>
        public RunYPJob(YieldProphet yp)
        {
            specToRun = yp;
        }

        /// <summary>Constructor</summary>
        /// <param name="xml">The YieldProphet xml to run</param>
        public RunYPJob(string xml)
        {
            specXMLToRun = xml;
        }

        /// <summary>Constructor</summary>
        /// <param name="sims">The list of APSIM simulations to run</param>
        public RunYPJob(List<APSIMSpec> sims)
        {
            simulationsToRun = sims;
        }

        /// <summary>Get all generated outputs</summary>
        public DataSet Outputs { get; private set; }

        /// <summary>Output zip file.</summary>
        public Stream AllFilesZipped { get; private set; }

        /// <summary>Return the yield prophet specification</summary>
        public YieldProphet Spec { get { return specToRun; } }

        /// <summary>Called to start the job.</summary>
        /// <param name="jobManager">Job manager</param>
        /// <param name="worker">Background worker</param>
        public void Run(JobManager jobManager, BackgroundWorker worker)
        {
            // Create a working directory.
            string workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            try
            {
                // Create a YieldProphet object if xml was provided
                string fileBaseToWrite = "YieldProphet";
                if (specXMLToRun != null)
                {
                    specToRun = YieldProphetUtility.YieldProphetFromXML(specXMLToRun, workingDirectory);
                    simulationsToRun = YieldProphetToAPSIM.ToAPSIM(specToRun);
                    fileBaseToWrite = specToRun.ReportName;

                    // Fill in calculated fields.
                    foreach (Paddock paddock in specToRun.Paddock)
                        YieldProphetUtility.FillInCalculatedFields(paddock, paddock.ObservedData, workingDirectory);
                }
                else if (specToRun != null)
                    simulationsToRun = YieldProphetToAPSIM.ToAPSIM(specToRun);

                // Create all the files needed to run APSIM.
                string apsimFileName = APSIMFiles.Create(simulationsToRun, workingDirectory, fileBaseToWrite + ".apsim");

                // Save YieldProphet.xml to working folder.
                if (specToRun != null)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(YieldProphetUtility.YieldProphetToXML(specToRun));
                    doc.Save(Path.Combine(workingDirectory, fileBaseToWrite + ".xml"));
                }

                // Go find APSIM executable
                string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string apsimExecutable = DirectoryUtilities.FindFileInDirectoryStructure("ApsimModel.exe", path);
                string binDirectory = Path.GetDirectoryName(apsimExecutable);

                // Convert .apsim file to sims so that we can run ApsimModel.exe rather than Apsim.exe
                // This will avoid using the old APSIM job runner. It assumes though that there are 
                // no other APSIMJob instances running in the workingDirectory. This is because it
                // looks and runs all .sim files it finds in the workingDirectory.
                JobParallel simJobs = new JobParallel();
                simJobs.Jobs = new List<JobManager.IRunnable>();
                string[] simFileNames = CreateSimFiles(apsimFileName, workingDirectory, binDirectory);
                foreach (string simFileName in simFileNames)
                {
                    if (simFileName == null || simFileName.Trim() == string.Empty)
                        throw new Exception("Blank .sim file names found for apsim file: " + apsimFileName);
                    simJobs.Jobs.Add(new RunnableJobs.APSIMJob(simFileName, workingDirectory, apsimExecutable, true));
                }

                jobManager.AddChildJob(this, simJobs);

                // Wait for it to be completed.
                while (!jobManager.IsJobCompleted(simJobs))
                    Thread.Sleep(200);

                // Perform cleanup and get outputs.
                Outputs = APSIMPostSimulation.PerformCleanup(workingDirectory);

            }
            finally
            {
                // Zip the temporary directory
                AllFilesZipped = new MemoryStream();
                ZipUtilities.ZipFiles(Directory.GetFiles(workingDirectory), null, AllFilesZipped);

                // Get rid of our temporary directory.
                Directory.Delete(workingDirectory, true);
            }
        }

        /// <summary>Convert .apsim file to .sim files</summary>
        /// <param name="apsimFileName">The .apsim file name.</param>
        /// <param name="workingDirectory">The working directory</param>
        /// <param name="binDirectory">Directory where executables are located</param>
        /// <returns>A list of filenames for all created .sim files.</returns>
        private static string[] CreateSimFiles(string apsimFileName, string workingDirectory, string binDirectory)
        {
            string executable = Path.Combine(binDirectory, "ApsimToSim.exe");

            RunnableJobs.APSIMJob job = new RunnableJobs.APSIMJob(apsimFileName, workingDirectory, executable, 
                                                                  false, null);
            job.Run(null, null);

            return Directory.GetFiles(workingDirectory, "*.sim");
        }

    }
}
