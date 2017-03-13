// -----------------------------------------------------------------------
// <copyright file="APSIMJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using APSIM.Shared.Utilities;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// A runnable class for a single APSIM simulation run.
    /// </summary>
    public class APSIMJob : JobManager.IRunnable, JobManager.IComputationalyTimeConsuming
    {
        /// <summary>Gets or sets the name of the APSIM file.</summary>
        private string fileName;

        /// <summary>Gets or sets the working directory.</summary>
        private string workingDirectory;

        /// <summary>Path to apsim.exe.</summary>
        private string ApsimExecutable;

        /// <summary>Create a summary file?</summary>
        private bool createSumFile;

        /// <summary>Arguments for job</summary>
        private string arguments;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the apsim file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        /// <param name="createSumFile">Create a summary file?</param>
        public APSIMJob(string fileName, string workingDirectory, string apsimExecutable, bool createSumFile = false, string arguments = null)
        {
            this.fileName = fileName;
            this.workingDirectory = workingDirectory;
            this.createSumFile = createSumFile;
            this.ApsimExecutable = apsimExecutable;
            this.arguments = arguments;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="jobManager">Job manager</param>
        /// <param name="worker">Background worker</param>
        public void Run(JobManager jobManager, BackgroundWorker worker)
        {

            // Start the external process to run APSIM and wait for it to finish.
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = ApsimExecutable;
            if (!File.Exists(p.StartInfo.FileName))
                throw new Exception("Cannot find executable: " + p.StartInfo.FileName);
            p.StartInfo.Arguments = StringUtilities.DQuote(fileName) + " " + arguments;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.CreateNoWindow = true;
            if (createSumFile)
                p.StartInfo.RedirectStandardOutput = true;
            p.Start();

            if (createSumFile)
            {
                string sumFileName = Path.ChangeExtension(fileName, ".sum");
                using (FileStream str = new FileStream(sumFileName, FileMode.Create))
                {
                    p.StandardOutput.BaseStream.CopyTo(str);
                }
            }

            p.WaitForExit();
        }

    }
}
