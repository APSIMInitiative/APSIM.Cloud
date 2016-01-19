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

    /// <summary>
    /// A runnable class for a single APSIM simulation run.
    /// </summary>
    public class APSIMJob : JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return true; } }
        
        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>Gets or sets the name of the APSIM file.</summary>
        private string fileName;

        /// <summary>Gets or sets the working directory.</summary>
        private string workingDirectory;

        /// <summary>Path to apsim.exe.</summary>
        private string ApsimExecutable;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the apsim file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public APSIMJob(string fileName, string workingDirectory, string apsimExecutable)
        {
            this.fileName = fileName;
            this.workingDirectory = workingDirectory;
            if (apsimExecutable == null)
            {
                string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                this.ApsimExecutable = Path.Combine(binDirectory, @"APSIM\Model\Apsim.exe");
            }
            else
                this.ApsimExecutable = apsimExecutable;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {

            // Start the external process to run APSIM and wait for it to finish.
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = ApsimExecutable;
            if (!File.Exists(p.StartInfo.FileName))
                throw new Exception("Cannot find executable: " + p.StartInfo.FileName);
            p.StartInfo.Arguments = StringUtilities.DQuote(fileName);
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
        }
    }
}
