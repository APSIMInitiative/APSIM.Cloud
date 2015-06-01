// -----------------------------------------------------------------------
// <copyright file="AusFarmJob.cs" company="APSIM Initiative">
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
    /// A runnable class for a single AusFarm simulation run.
    /// </summary>
    class AusFarmJob: JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return true; } }
        
        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>Gets or sets the name of the APSIM file.</summary>
        private string fileName;

        /// <summary>The arguments</summary>
        private string arguments;

        /// <summary>Gets or sets the working directory.</summary>
        private string workingDirectory;

        /// <summary>The summary file</summary>
        private StreamWriter summaryFile;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the ausfarm file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public AusFarmJob(string fileName, string arguments = null)
        {
            this.fileName = fileName;
            this.arguments = arguments;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Sort out the command arguments.
            string args = fileName;
            if (arguments != null)
                args += " " + arguments;

            // Open the summary file for writing.
            summaryFile = new StreamWriter(Path.ChangeExtension(fileName, ".sum"));

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Start the external process to run AusFarm and wait for it to finish.
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = Path.Combine(binDirectory, @"F4P\auscmd32.exe");
            p.StartInfo.Arguments = args;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.CreateNoWindow = true;
            p.OutputDataReceived += OnStdOutWrite;
            p.EnableRaisingEvents = true;
            p.Start();
            p.BeginOutputReadLine();
            string StdErr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            // Close the summary file.
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
