// -----------------------------------------------------------------------
// <copyright file="RunYPJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using APSIM.Cloud.Shared;
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Xml;

    //using ApsimFile;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class RunYPJob : JobManager.IRunnable, IJob
    {
        private YieldProphet specToRun;
        private string specXMLToRun;
        private List<APSIMSpec> simulationsToRun;
        private bool useApsimX;

        /// <summary>The APSIMx executable to use.</summary>
        public string apsimXExecutable { get; set; }

        /// <summary>Constructor</summary>
        /// <param name="yp">The YieldProphet spec to run</param>
        /// <param name="useApsimX">Use APSIMX?</param>
        public RunYPJob(YieldProphet yp, bool useApsimX = false)
        {
            specToRun = yp;
            this.useApsimX = useApsimX;
        }

        /// <summary>Constructor</summary>
        /// <param name="xml">The YieldProphet xml to run</param>
        /// <param name="useApsimX">Use APSIMX?</param>
        public RunYPJob(string xml, bool useApsimX = false)
        {
            specXMLToRun = xml;
            this.useApsimX = useApsimX;
        }

        /// <summary>Constructor</summary>
        /// <param name="sims">The list of APSIM simulations to run</param>
        /// <param name="useApsimX">Use APSIMX?</param>
        public RunYPJob(List<APSIMSpec> sims, bool useApsimX = false)
        {
            simulationsToRun = sims;
            this.useApsimX = useApsimX;
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
                    if (specToRun.ReportName != null)
                        fileBaseToWrite = specToRun.ReportName;

                    // Fill in calculated fields.
                    foreach (Paddock paddock in specToRun.Paddock)
                        YieldProphetUtility.FillInCalculatedFields(paddock, paddock.ObservedData, workingDirectory);
                }
                else if (specToRun != null)
                    simulationsToRun = YieldProphetToAPSIM.ToAPSIM(specToRun);

                // Create all the files needed to run APSIM.
                string fileToWrite;
                if (useApsimX)
                    fileToWrite = fileBaseToWrite + ".apsimx";
                else
                    fileToWrite = fileBaseToWrite + ".apsim";
                string apsimFileName = APSIMFiles.Create(simulationsToRun, workingDirectory, fileToWrite);

                // Save YieldProphet.xml to working folder.
                if (specToRun != null)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(YieldProphetUtility.YieldProphetToXML(specToRun));
                    doc.Save(Path.Combine(workingDirectory, fileBaseToWrite + ".xml"));
                }

                // Go find APSIM executable
                JobManager.IRunnable job;
                if (useApsimX)
                    job = GetJobToRunAPSIMX(apsimFileName);
                else
                    job = GetJobToRunAPSIMClassic(apsimFileName);

                jobManager.AddChildJob(this, job);

                // Wait for it to be completed.
                while (!jobManager.IsJobCompleted(job))
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

        /// <summary>
        /// Create and return a job to run APSIM classic.
        /// </summary>
        /// <param name="apsimFileName">The name of the simulation file.</param>
        private static JobParallel GetJobToRunAPSIMClassic(string apsimFileName)
        {
            string workingDirectory = Path.GetDirectoryName(apsimFileName);
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

            return simJobs;
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
                                                                  true, null);
            job.Run(null, null);

            string sumFileName = Path.ChangeExtension(apsimFileName, ".sum");
            if (File.Exists(sumFileName))
            {
                string msg = File.ReadAllText(sumFileName);
                if (msg != null && msg != string.Empty)
                    throw new Exception("ApsimToSim Error:\r\n" + msg);
            }
            return Directory.GetFiles(workingDirectory, "*.sim");
        }

        /// <summary>
        /// Create and return a job to run APSIMx.
        /// </summary>
        /// <param name="apsimFileName">The name of the simulation file.</param>
        private JobManager.IRunnable GetJobToRunAPSIMX(string apsimFileName)
        {
            string workingDirectory = Path.GetDirectoryName(apsimFileName);
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string apsimExecutable;
            if (apsimXExecutable == null)
                apsimExecutable = DirectoryUtilities.FindFileInDirectoryStructure("Models.exe", path);
            else
                apsimExecutable = apsimXExecutable;

            return new APSIMJob(apsimFileName, workingDirectory, apsimExecutable, false, null);
        }

    }
}
