// -----------------------------------------------------------------------
// <copyright file="APSIMPostSimulationJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services.RunnableJobs
{
    using System.IO;
    using System.Collections.Generic;
    using System.Xml;

    /// <summary>
    /// A runnable class to run a series of post simulation cleanup functions.
    /// </summary>
    public class APSIMPostSimulationJob : Utility.JobManager.IRunnable
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

            foreach (string apsimFileName in Directory.GetFiles(workingFolder, "*.apsim"))
            {
                List<XmlNode> simulationNodes = new List<XmlNode>();
                XmlDocument doc = new XmlDocument();
                doc.Load(apsimFileName);
                Utility.Xml.FindAllRecursivelyByType(doc.DocumentElement, "simulation", ref simulationNodes);

                // Concatenate summary files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = Utility.Xml.NameAttr(simNode);
                    string[] sumFiles = Directory.GetFiles(workingFolder, simName + "*.sum");
                    if (sumFiles.Length > 0)
                        ConcatenateSummaryFiles(sumFiles, simName + ".sum");
                }

                // Concatenate yearly output files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = Utility.Xml.NameAttr(simNode);
                    string[] outFiles = Directory.GetFiles(workingFolder, simName + "*Yearly.out");
                    if (outFiles.Length > 0)
                        ConcatenateOutputFiles(outFiles, simName + " Yearly.out");
                }

                // Concatenate monthly output files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = Utility.Xml.NameAttr(simNode);
                    string[] outFiles = Directory.GetFiles(workingFolder, simName + "*Monthly.out");
                    if (outFiles.Length > 0)
                        ConcatenateOutputFiles(outFiles, simName + " Monthly.out");
                }

                // Concatenate daily output files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = Utility.Xml.NameAttr(simNode);
                    string[] outFiles = Directory.GetFiles(workingFolder, simName + "*Daily.out");
                    if (outFiles.Length > 0)
                        ConcatenateOutputFiles(outFiles, simName + " Daily.out");
                }

                // zip up the met files.
                string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
                ZipFiles(metFiles, Path.Combine(workingFolder, "MetFiles.zip"));
            }
        }

        /// <summary>Concatenates the specified output files into one file.</summary>
        /// <param name="outFiles">The out files.</param>
        private static void ConcatenateOutputFiles(string[] outFiles, string fileName)
        {
            string workingFolder = Path.GetDirectoryName(outFiles[0]);
            string singleOutputFileName = Path.Combine(workingFolder, fileName);
            StreamWriter outWriter = null;

            // Assume they are all structured the same i.e. same headings and units.
            foreach (string outputFileName in outFiles)
            {
                StreamReader outReader = new StreamReader(outputFileName);

                if (outWriter == null)
                {
                    outWriter = new StreamWriter(singleOutputFileName);
                    outWriter.WriteLine(outReader.ReadLine());  // APSIM version number
                    outWriter.WriteLine("Title = Yearly");
                    outReader.ReadLine(); // ignore factors lines
                    outReader.ReadLine(); // ignore title line.
                    outWriter.WriteLine(outReader.ReadLine());  // headings
                    outWriter.WriteLine(outReader.ReadLine());  // units
                }
                else
                {
                    // ignore first 5 lines.
                    outReader.ReadLine();
                    outReader.ReadLine();
                    outReader.ReadLine();
                    outReader.ReadLine();
                    outReader.ReadLine();
                }
                outWriter.Write(outReader.ReadToEnd());
                outReader.Close();
            }

            outWriter.Close();

            // Delete the .out files.
            foreach (string outputFileName in outFiles)
                File.Delete(outputFileName);
        }

        /// <summary>Concatenates the summary files.</summary>
        /// <param name="sumFiles">The sum files to concatenate</param>
        private static void ConcatenateSummaryFiles(string[] sumFiles, string fileName)
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

        /// <summary>Zips the files.</summary>
        /// <param name="intoFileName">The name of the file to create.</param>
        /// <param name="fileNames">The file names to zip.</param>
        private static void ZipFiles(string[] fileNames, string intoFileName)
        {
            // Zip up files.
            Utility.Zip.ZipFiles(fileNames, intoFileName, null);

            // Delete the .met files.
            foreach (string fileName in fileNames)
                File.Delete(fileName);
        }

    }
}
