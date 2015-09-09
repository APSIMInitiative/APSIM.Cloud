// -----------------------------------------------------------------------
// <copyright file="APSIMPostSimulationJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using System.IO;
    using System.Collections.Generic;
    using System.Xml;
    using APSIM.Shared.Utilities;
    using System.Data;

    /// <summary>
    /// A runnable class to run a series of post simulation cleanup functions.
    /// </summary>
    public class APSIMPostSimulationJob : JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return true; } }
        
        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>The working folder</summary>
        private string workingFolder;

        /// <summary>Initializes a new instance of the <see cref="APSIMPostSimulationJob"/> class.</summary>
        /// <param name="workingFolder">The working folder.</param>
        public APSIMPostSimulationJob(string workingFolder)
        {
            this.workingFolder = workingFolder;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Delete the .sim files.
            string[] simFiles = Directory.GetFiles(workingFolder, "*.sim");
            foreach (string simFile in simFiles)
                File.Delete(simFile);

            bool longtermOutputsFound = false;

            // Cleanup longterm out and sum files. They are named XXX_01 Yearly.out
            // where XXX is the name of the simulation.
            foreach (string apsimFileName in Directory.GetFiles(workingFolder, "*.apsim"))
            {
                List<XmlNode> simulationNodes = new List<XmlNode>();
                XmlDocument doc = new XmlDocument();
                doc.Load(apsimFileName);
                XmlUtilities.FindAllRecursivelyByType(doc.DocumentElement, "simulation", ref simulationNodes);

                // Concatenate summary files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = XmlUtilities.NameAttr(simNode);
                    string[] sumFiles = Directory.GetFiles(workingFolder, simName + "_*.sum");
                    if (sumFiles.Length > 0)
                    {
                        longtermOutputsFound = true;
                        ConcatenateSummaryFiles(sumFiles, simName + ".sum");
                    }
                }

                // Concatenate output files.
                SortedSet<string> outputTypes = new SortedSet<string>();
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simulationName = XmlUtilities.NameAttr(simNode);
                    foreach (string outputType in Directory.GetFiles(workingFolder, simulationName + "_01*.out"))
                    {
                        string outputFileType = Path.GetFileNameWithoutExtension(outputType.Replace(simulationName, ""));
                        outputFileType = " " + StringUtilities.SplitOffAfterDelimiter(ref outputFileType, " ");
                        outputTypes.Add(outputFileType);
                    }
                }

                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = XmlUtilities.NameAttr(simNode);
                    foreach (string outputFileType in outputTypes)
                    {
                        string wildcard = simName + "_*" + outputFileType + ".out";
                        string[] outFiles = Directory.GetFiles(workingFolder, wildcard);
                        string fileNameToWrite = simName + outputFileType + ".csv";
                        ConcatenateOutputFiles(outFiles, fileNameToWrite, outputFileType);
                    }
                }
            }

            if (!longtermOutputsFound)
            {
                // By now the longterm .out and .sum files have been concatenated. Assume
                // all simulations are the same; get the different types of reports for each simulation
                // and concatenate.
                string apsimFileName1 = Directory.GetFiles(workingFolder, "*.apsim")[0];
                string[] allSumFiles = Directory.GetFiles(workingFolder, "*.sum");
                ConcatenateSummaryFiles(allSumFiles, Path.ChangeExtension(apsimFileName1, ".sum"));

                XmlDocument doc1 = new XmlDocument();
                doc1.Load(apsimFileName1);

                foreach (XmlNode simulationNode in XmlUtilities.ChildNodes(doc1.DocumentElement, "simulation"))
                {
                    if (simulationNode != null)
                    {
                        string simulationName = XmlUtilities.NameAttr(simulationNode);
                        string[] outFileTypes = Directory.GetFiles(workingFolder, simulationName + "*.out");
                        foreach (string outputfileName in outFileTypes)
                        {
                            string outputFileType = Path.GetFileNameWithoutExtension(outputfileName.Replace(simulationName, ""));
                            string wildcard = "*" + outputFileType + ".out";
                            string[] outFiles = Directory.GetFiles(workingFolder, wildcard);
                            string fileNameToWrite = Path.GetFileNameWithoutExtension(apsimFileName1) + outputFileType + ".csv";
                            ConcatenateOutputFiles(outFiles, fileNameToWrite, outputFileType);
                        }
                    }
                }
            }

            // zip up the met files.
            string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
            ZipFiles(metFiles, Path.Combine(workingFolder, "MetFiles.zip"));
        }

        /// <summary>Concatenates the specified output files into one file.</summary>
        /// <param name="outFiles">The out files.</param>
        private static void ConcatenateOutputFiles(string[] outFiles, string fileName, string outputFileType)
        {
            if (outFiles.Length > 0)
            {
                // Assume they are all structured the same i.e. same headings and units.
                // Read in data from all files.
                DataTable allData = null;
                foreach (string outputFileName in outFiles)
                {
                    ApsimTextFile reader = new ApsimTextFile();
                    reader.Open(outputFileName);

                    List<string> constantsToAdd = new List<string>();
                    constantsToAdd.Add("Title");
                    DataTable data = reader.ToTable(constantsToAdd);
                    reader.Close();

                    if (data.Columns.Count > 0 && data.Rows.Count > 0)
                    {
                        if (allData == null)
                            allData = data;
                        else
                            DataTableUtilities.CopyRows(data, allData);
                    }
                }

                if (allData != null)
                {
                    // Move the title column to be first.
                    allData.Columns["Title"].SetOrdinal(0);

                    // Strip off the outputFileType (e.g. Yearly) from the titles.
                    foreach (DataRow row in allData.Rows)
                        row["Title"] = row["Title"].ToString().Replace(outputFileType, "");

                    // Write data.
                    string workingFolder = Path.GetDirectoryName(outFiles[0]);
                    string singleOutputFileName = Path.Combine(workingFolder, fileName);
                    StreamWriter outWriter = new StreamWriter(singleOutputFileName);

                    outWriter.Write(DataTableUtilities.DataTableToText(allData, 0, ",  ", true));

                    outWriter.Close();
                }

                // Delete the .out files.
                foreach (string outputFileName in outFiles)
                    File.Delete(outputFileName);
            }
        }

        /// <summary>Concatenates the summary files.</summary>
        /// <param name="sumFiles">The sum files to concatenate</param>
        private static void ConcatenateSummaryFiles(string[] sumFiles, string fileName)
        {
            if (sumFiles.Length > 0)
            {
                string workingFolder = Path.GetDirectoryName(sumFiles[0]);
                string singleSummaryFileName = Path.Combine(workingFolder, fileName);
                StreamWriter sumWriter = new StreamWriter(singleSummaryFileName);

                foreach (string summaryFileName in sumFiles)
                {
                    StreamReader sumReader = new StreamReader(summaryFileName);
                    sumWriter.Write(sumReader.ReadToEnd());
                    sumReader.Close();
                }

                sumWriter.Close();

                // Delete the .sum files.
                foreach (string summaryFileName in sumFiles)
                    File.Delete(summaryFileName);
            }
        }

        /// <summary>Zips the files.</summary>
        /// <param name="intoFileName">The name of the file to create.</param>
        /// <param name="fileNames">The file names to zip.</param>
        private static void ZipFiles(string[] fileNames, string intoFileName)
        {
            // Zip up files.
            ZipUtilities.ZipFiles(fileNames, intoFileName, null);

            // Delete the .met files.
            foreach (string fileName in fileNames)
                File.Delete(fileName);
        }

    }
}
