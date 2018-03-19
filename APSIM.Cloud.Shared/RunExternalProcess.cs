// -----------------------------------------------------------------------
// <copyright file="RunExternalProcess.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using APSIM.Shared.Utilities;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// A runnable class for a single APSIM simulation run.
    /// </summary>
    public class RunExternalProcess : IRunnable, IComputationalyTimeConsuming
    {
        /// <summary>Path to apsim.exe.</summary>
        private string executable;

        /// <summary>Arguments for job</summary>
        private string arguments;

        /// <summary>Gets or sets the working directory.</summary>
        private string workingFolder;

        /// <summary>The stdout filename</summary>
        private string stdoutFileName;

        /// <summary>The stderr filename</summary>
        private string stderrFileName;

        /// <summary>The summary file</summary>
        private StreamWriter stdoutWriter;

        /// <summary>Initializes a new instance of the <see cref="RunExternalProcess"/> class.</summary>
        /// <param name="executable">The executable to run</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        /// <param name="stdoutFileName">The stdout filename</param>
        /// <param name="stderrFileName">The stderr filename</param>
        public RunExternalProcess(string executable, string arguments, string workingDirectory, string stdoutFileName, string stderrFileName)
        {
            this.workingFolder = workingDirectory;
            this.executable = executable;
            this.arguments = arguments;
            this.stdoutFileName = stdoutFileName;
            this.stderrFileName = stderrFileName;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="cancelToken">Cancellation token</param>
        public void Run(CancellationTokenSource cancelToken)
        {
            string stdErr;
            try
            {
                // Open the stdout for writing.
                if (stdoutFileName != null)
                    stdoutWriter = new StreamWriter(Path.Combine(workingFolder, stdoutFileName));

                // Start the external process to run AusFarm and wait for it to finish.
                Process p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = executable;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WorkingDirectory = workingFolder;
                p.StartInfo.CreateNoWindow = true;
                p.OutputDataReceived += OnStdOutWrite;
                p.EnableRaisingEvents = true;
                p.Start();
                p.BeginOutputReadLine();
                stdErr = p.StandardError.ReadToEnd();
                p.WaitForExit();
            }
            finally
            {
                // Close the stdout file.
                if (stdoutWriter != null)
                    stdoutWriter.Close();
            }

            if (stdErr != string.Empty)
                File.AppendAllText(Path.Combine(workingFolder, stderrFileName), stdErr);
        }

        /// <summary>Called when APSIM writes something to the STDOUT.</summary>
        /// <param name="sendingProcess">The sending process.</param>
        /// <param name="outLine">The <see cref="DataReceivedEventArgs"/> instance containing the event data.</param>
        private void OnStdOutWrite(object sendingProcess,
              DataReceivedEventArgs outLine)
        {
            if (stdoutWriter != null && !string.IsNullOrEmpty(outLine.Data))
                stdoutWriter.WriteLine(outLine.Data);
        }
    }
}
