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
    public class ProcessYPJob : ProcessJob
    {
        
        /// <summary>Constructor</summary>
        /// <param name="runAPSIM">Run APSIM?</param>
        public ProcessYPJob(bool runAPSIM) : base(runAPSIM)
        {
        }

        /// <summary>Create a runnable job for the YP simulations</summary>
        /// <param name="FilesToRun">The files to run.</param>
        /// <param name="ApsimExecutable">APSIM.exe path. Can be null.</param>
        /// <returns>A runnable job for all simulations</returns>
        protected override JobManager.IRunnable CreateRunnableJob(string jobName, string jobXML, string workingDirectory, string ApsimExecutable)
        {
            // Create a sequential job.
            JobSequence completeJob = new JobSequence();
            completeJob.Jobs = new List<JobManager.IRunnable>();

            List<JobManager.IRunnable> jobs = new List<JobManager.IRunnable>();

            // Create a YieldProphet object from our YP xml file
            YieldProphet spec = YieldProphetUtility.YieldProphetFromXML(jobXML, workingDirectory);

            string fileBaseToWrite;
            if (spec.ReportType == YieldProphet.ReportTypeEnum.None && spec.ReportName != null)
                fileBaseToWrite = spec.ReportName;
            else
                fileBaseToWrite = "YieldProphet";

            // Convert YieldProphet spec into a simulation set.
            List<APSIMSpec> simulations = YieldProphetToAPSIM.ToAPSIM(spec);

            // Create all the files needed to run APSIM.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory, fileBaseToWrite + ".apsim");

            // Fill in calculated fields.
            foreach (Paddock paddock in spec.Paddock)
                YieldProphetUtility.FillInCalculatedFields(paddock, paddock.ObservedData, workingDirectory);

            // Save YieldProphet.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(YieldProphetUtility.YieldProphetToXML(spec));
            doc.Save(Path.Combine(workingDirectory, fileBaseToWrite + ".xml"));

            // Convert .apsim file to sims so that we can run ApsimModel.exe rather than Apsim.exe
            // This will avoid using the old APSIM job runner. It assumes though that there are 
            // no other APSIMJob instances running in the workingDirectory. This is because it
            // looks and runs all .sim files it finds in the workingDirectory.
            JobParallel simJobs = new JobParallel();
            simJobs.Jobs = new List<JobManager.IRunnable>();
            string[] simFileNames = CreateSimFiles(apsimFileName, workingDirectory);
            foreach (string simFileName in simFileNames)
            {
                if (simFileName == null || simFileName.Trim() == string.Empty)
                    throw new Exception("Blank .sim file names found for apsim file: " + apsimFileName);
                simJobs.Jobs.Add(new RunnableJobs.APSIMJob(simFileName, workingDirectory, ApsimExecutable, true));
            }
            completeJob.Jobs.Add(simJobs);
            completeJob.Jobs.Add(new RunnableJobs.APSIMPostSimulationJob(workingDirectory));
            if (spec.Paddock.Count > 0 && spec.Paddock[0].RunType != Paddock.RunTypeEnum.Validation)
                completeJob.Jobs.Add(new RunnableJobs.YPPostSimulationJob(jobName, spec.Paddock[0].NowDate, workingDirectory));

            return completeJob;
        }
    }
}
