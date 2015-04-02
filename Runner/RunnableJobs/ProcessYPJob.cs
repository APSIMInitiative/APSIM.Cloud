//// -----------------------------------------------------------------------
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
    using ApsimFile;
    using System.Data;
    using System.Threading;
    using APSIM.Cloud.Shared;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class ProcessYPJob : Utility.JobManager.IRunnable
    {
        /// <summary>The working directory</summary>
        private string workingDirectory;

        /// <summary>The apsim report executable path.</summary>
        private static string archiveLocation = @"ftp://www.apsim.info/YP/Archive";

        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }

        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>Gets or sets the name of the job.</summary>
        public string JobName { get; set; }

        /// <summary>
        /// Runs the YP job.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Get our job manager.
            Utility.JobManager jobManager = (Utility.JobManager)e.Argument;

            // Create a working directory.
            workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            string jobXML;
            using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
            {
                jobXML = jobsClient.GetJobXML(JobName);
            }

            // Create and run a job.
            string errorMessage = null;
            try
            {
                Utility.JobManager.IRunnable job = CreateRunnableJob(JobName, jobXML, workingDirectory);
                jobManager.AddJob(job);
                while (!job.IsCompleted)
                    Thread.Sleep(5 * 1000); // 5 sec
                errorMessage = job.ErrorMessage;
            }
            catch (Exception err)
            {
                errorMessage = err.Message + "\r\n" + err.StackTrace;
            }

            // Zip the temporary directory and send to archive.
            string zipFileName = Path.Combine(workingDirectory, JobName + ".zip");
            Utility.Zip.ZipFiles(Directory.GetFiles(workingDirectory), zipFileName, null);
            Utility.FTPClient.Upload(zipFileName, archiveLocation + "/" + JobName + ".zip", "Administrator", "CsiroDMZ!");

            // Get rid of our temporary directory.
            Directory.Delete(workingDirectory, true);

            // Tell the job system that job is complete.
            using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
            {
                jobsClient.SetCompleted(JobName, errorMessage);
            }
        }

        /// <summary>Create a runnable job for the APSIM simulations</summary>
        /// <param name="FilesToRun">The files to run.</param>
        /// <returns>A runnable job for all simulations</returns>
        private static Utility.JobManager.IRunnable CreateRunnableJob(string jobName, string jobXML, string workingDirectory)
        {
            // Create a YieldProphet object from our YP xml file
            YieldProphet spec = YieldProphetUtility.YieldProphetFromXML(jobXML);

            // Convert YieldProphet spec into a simulation set.
            List<APSIMSpec> simulations = YieldProphetToAPSIM.ToAPSIM(spec);

            // Create all the files needed to run APSIM.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory);

            // Fill in calculated fields.
            foreach (Paddock paddock in spec.Paddock)
                YieldProphetUtility.FillInCalculatedFields(paddock, paddock.ObservedData, workingDirectory);

            // Save YieldProphet.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(YieldProphetUtility.YieldProphetToXML(spec));
            doc.Save(Path.Combine(workingDirectory, "YieldProphet.xml"));

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string apsimHomeDirectory = Path.Combine(binDirectory, "Temp");
            Configuration.SetApsimDir(apsimHomeDirectory);
            PlugIns.LoadAll();

            List<Utility.JobManager.IRunnable> jobs = new List<Utility.JobManager.IRunnable>();
            XmlDocument Doc = new XmlDocument();
            Doc.Load(apsimFileName);

            List<XmlNode> simulationNodes = new List<XmlNode>();
            Utility.Xml.FindAllRecursivelyByType(Doc.DocumentElement, "simulation", ref simulationNodes);

            foreach (XmlNode simulation in simulationNodes)
            {
                if (Utility.Xml.Attribute(simulation, "enabled") != "no")
                {
                    XmlNode factorialNode = Utility.Xml.FindByType(Doc.DocumentElement, "factorial");
                    if (factorialNode == null)
                    {
                        RunnableJobs.APSIMJob job = new RunnableJobs.APSIMJob(
                           fileName: apsimFileName,
                           arguments: "Simulation=" + Utility.Xml.FullPath(simulation));
                        jobs.Add(job);
                    }
                    else
                    {
                        ApsimFile F = new ApsimFile();

                        // The OpenFile method below changes the current directory: We don't want
                        // this. Restore the current directory after calling it.
                        string cwd = Directory.GetCurrentDirectory();
                        F.OpenFile(apsimFileName);
                        Directory.SetCurrentDirectory(cwd);

                        FactorBuilder builder = new FactorBuilder();
                        string path = "/" + Utility.Xml.FullPath(simulation);
                        path = path.Remove(path.LastIndexOf('/'));
                        List<string> simsToRun = new List<string>();
                        foreach (FactorItem item in builder.BuildFactorItems(F.FactorComponent, path))
                        {
                            List<String> factorials = new List<string>();
                            item.CalcFactorialList(factorials);
                            foreach (string factorial in factorials)
                                simsToRun.Add(path + "@factorial='" + factorial + "'");
                        }
                        List<SimFactorItem> simFactorItems = Factor.CreateSimFiles(F, simsToRun.ToArray(), workingDirectory);

                        foreach (SimFactorItem simFactorItem in simFactorItems)
                        {

                            RunnableJobs.APSIMJob job = new RunnableJobs.APSIMJob(
                                fileName: simFactorItem.SimFileName);
                            jobs.Add(job);
                        }
                    }
                }
            }

            // Create a sequential job.
            Utility.JobSequence completeJob = new Utility.JobSequence();
            completeJob.Jobs = new List<Utility.JobManager.IRunnable>();
            completeJob.Jobs.Add(new Utility.JobParallel() { Jobs = jobs });
            completeJob.Jobs.Add(new RunnableJobs.APSIMPostSimulationJob(workingDirectory));
            completeJob.Jobs.Add(new RunnableJobs.YPPostSimulationJob(jobName, spec.Paddock[0].NowDate, workingDirectory));

            return completeJob;
        }
    }
}
