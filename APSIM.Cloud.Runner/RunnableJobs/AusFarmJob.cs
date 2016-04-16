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
    using System.Data;
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// A runnable class for a single AusFarm simulation run.
    /// </summary>
    class AusFarmJob: JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }
        
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

        /// <summary>The job name</summary>
        private string jobName;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="jobName">The name of the job.</param>
        /// <param name="fileName">Name of the ausfarm file.</param>
        /// <param name="arguments">The arguments.</param>
        public AusFarmJob(string jobName, string fileName, string arguments = null)
        {
            this.jobName = jobName;
            this.fileName = fileName;
            this.arguments = arguments;
            this.workingDirectory = Path.GetDirectoryName(fileName);
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {

            // Sort out the command arguments.
            string args = Path.GetFileName(fileName);
            if (arguments != null)
                args += " " + arguments;

            // Open the summary file for writing.
            summaryFile = new StreamWriter(Path.ChangeExtension(fileName, ".sum"));

            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ausfarmBin = Path.Combine(binDirectory, "AusFarm");

            // Copy in the .prm files.
            foreach (string prmFileName in Directory.GetFiles(ausfarmBin, "*.prm"))
            {
                File.Copy(prmFileName, Path.Combine(workingDirectory, Path.GetFileName(prmFileName)));
            }


            // Start the external process to run AusFarm and wait for it to finish.
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = Path.Combine(ausfarmBin, "auscmd32.exe");
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

            // Look for an error log file. Seems the error file gets written to the AusFarm
            // directory rather than the same place as the .sdml.
            // If found then copy into the working directory.
            string errorFile = Path.Combine(binDirectory, "AusFarm", Path.GetFileName(fileName) + "_errors.log");
            if (File.Exists(errorFile))
            {
                File.Move(errorFile, Path.Combine(workingDirectory, Path.GetFileName(errorFile)));
            }

            // Do post simulation stuff.
            DoPostSimulation();
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

        /// <summary>Does the post simulation stuff.</summary>
        private void DoPostSimulation()
        {
            DataSet dataSet = new DataSet("ReportData");
            foreach (string outFileName in Directory.GetFiles(workingDirectory, "*.txt"))
            {
                try
                {
                    dataSet.Tables.Add(TxtToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .txt files are empty - ignore them..
                }
            }

            // Call Farm4Prophet web service.
            using (F4P.F4PClient f4pClient = new F4P.F4PClient())
            {
                try
                {
                    f4pClient.StoreReport(jobName, dataSet);
                }
                catch (Exception)
                {
                    throw new Exception("Cannot call F4P StoreReport web service method");
                }
            }

        }

        /// <summary>Creates a datatable from a .txt file.</summary>
        /// <param name="txtFileName">Name of the txt file.</param>
        /// <returns>The newly created DataTable.</returns>
        private DataTable TxtToTable(string txtFileName)
        {
            DataTable table = new DataTable();

            // Open the .txt file.
            StreamReader reader = new StreamReader(txtFileName);

            char[] delimiter = new char[] { '\t' };

            // read headings.
            string[] headings = reader.ReadLine().Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

            // read and ignore units.
            reader.ReadLine();

            // read data.
            while (!reader.EndOfStream)
            {
                // read line
                string line = reader.ReadLine();

                // split line into words
                string[] lineWords = line.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                // make sure we have the right number of words i.e. matches headings.
                if (lineWords.Length > 0)
                {
                    if (lineWords.Length != headings.Length)
                        throw new Exception("A line in " + txtFileName + " doesn't have the right number of values. Line = " + line);

                    // Convert the words into a list of objects (doubles or DateTimes)
                    List<object> data = ConvertWordsToObjects(lineWords);

                    // If we haven't set up the table columns yet then do so now.
                    if (table.Columns.Count == 0)
                        for (int i = 0; i < headings.Length; i++)
                            table.Columns.Add(headings[i], data[i].GetType());

                    // Store a new row in table.
                    DataRow newRow = table.NewRow();
                    for (int i = 0; i < data.Count; i++)
                        newRow[i] = data[i];
                    table.Rows.Add(newRow);
                }
            }

            reader.Close();
            return table;
        }

        /// <summary>Converts the words to objects.</summary>
        /// <param name="lineWords">The line words.</param>
        /// <returns>A list of objects.</returns>
        private List<object> ConvertWordsToObjects(string[] lineWords)
        {
            List<object> data = new List<object>();
            foreach (string word in lineWords)
            {
                if (word == "-")
                    data.Add("-");
                else if (word.Contains("-"))
                    data.Add(DateTime.ParseExact(word, "yyyy-MM-dd", CultureInfo.InvariantCulture));
                else
                {
                    double value;
                    if (Double.TryParse(word, out value))
                        data.Add(Convert.ToDouble(word));
                    else
                        data.Add(word);
                }
            }
            return data;
        }


    }
}
