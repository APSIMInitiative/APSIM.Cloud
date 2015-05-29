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

        /// <summary>The summary file</summary>
        private StreamWriter summaryFile;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the apsim file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public APSIMJob(string fileName)
        {
            this.fileName = fileName;
            this.workingDirectory = Path.GetDirectoryName(fileName);
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Open the summary file for writing.
            string summaryFileName = Path.ChangeExtension(fileName, ".sum");
            summaryFile = new StreamWriter(summaryFileName);

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Start the external process to run APSIM and wait for it to finish.
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = Path.Combine(binDirectory, @"Temp\Model\ApsimModel.exe");
            p.StartInfo.Arguments = StringUtilities.DQuote(fileName);
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.CreateNoWindow = true;
            p.OutputDataReceived += OnStdOutWrite;
            p.EnableRaisingEvents = true;
            p.Start();
            p.BeginOutputReadLine();
            string StdErr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            // Close the summary file.
            if (StdErr != null)
                summaryFile.WriteLine(StdErr);
            summaryFile.Close();
        }

        /// <summary>Called when APSIM writes something to the STDOUT.</summary>
        /// <param name="sendingProcess">The sending process.</param>
        /// <param name="outLine">The <see cref="DataReceivedEventArgs"/> instance containing the event data.</param>
        private void OnStdOutWrite(object sendingProcess,
              DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data))
                summaryFile.WriteLine(outLine.Data);
        }

    }
}
