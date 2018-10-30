// -----------------------------------------------------------------------
// <copyright file="RunYPJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using APSIM.Cloud.Shared;
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Xml;

    /// <summary>
    /// A job manager for a running a specific YieldProphet job.
    /// </summary>
    public class RunYPJob : IJobManager, IYPJob
    {
        private YieldProphet specToRun;
        private string specXMLToRun;
        private List<APSIMSpecification> allSimulations;
        private RuntimeEnvironment environment;
        private List<IRunnable> jobsToRun = null;
        private string binFolder;

        /// <summary>The APSIMx executable to use.</summary>
        public string ApsimXExecutable { get; set; }

        /// <summary>Constructor</summary>
        /// <param name="yp">The YieldProphet spec to run</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        public RunYPJob(YieldProphet yp, RuntimeEnvironment environment)
        {
            specToRun = yp;
            this.environment = environment;
            Initialise();
        }

        /// <summary>Constructor</summary>
        /// <param name="xml">The YieldProphet xml to run</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        public RunYPJob(string xml, RuntimeEnvironment environment, bool createSims = true)
        {
            specXMLToRun = xml;
            this.environment = environment;
            Initialise(createSims);
        }

        /// <summary>Constructor</summary>
        /// <param name="sims">The list of APSIM simulations to run</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        public RunYPJob(List<APSIMSpecification> sims, RuntimeEnvironment environment)
        {
            allSimulations = sims;
            this.environment = environment;
            Initialise();
        }

        /// <summary>
        /// Return the working directory.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>Initialise job manager</summary>
        private void Initialise(bool createSims = true)
        {
            Errors = new List<string>();
            try
            {
                // Create a working directory.
                WorkingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(WorkingDirectory);

                // Create a YieldProphet object if xml was provided
                string fileBaseToWrite = "YieldProphet";
                if (specXMLToRun != null)
                {
                    if (specXMLToRun.Contains("<YieldProphet>"))
                    {
                        specToRun = YieldProphetUtility.YieldProphetFromXML(specXMLToRun, WorkingDirectory);
                        allSimulations = YieldProphetToAPSIM.ToAPSIM(specToRun);
                        if (specToRun.ReportName != null)
                            fileBaseToWrite = specToRun.ReportName;
                    }
                    else
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(specXMLToRun);
                        allSimulations = XmlUtilities.Deserialise(doc.DocumentElement, typeof(List<APSIMSpecification>)) as List<APSIMSpecification>;
                    }
                }
                else if (specToRun != null)
                    allSimulations = YieldProphetToAPSIM.ToAPSIM(specToRun);

                // Create all the files needed to run APSIM.
                string fileToWrite;
                if (environment.APSIMxBuildNumber > 0)
                    fileToWrite = fileBaseToWrite + ".apsimx";
                else
                    fileToWrite = fileBaseToWrite + ".apsim";
                string apsimFileName = APSIMFiles.Create(allSimulations, WorkingDirectory, fileToWrite);

                // Save YieldProphet.xml to working folder.
                if (specToRun != null)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(YieldProphetUtility.YieldProphetToXML(specToRun));
                    doc.Save(Path.Combine(WorkingDirectory, fileBaseToWrite + ".xml"));
                }

                if (createSims)
                {
                    // Setup the runtime environment.
                    binFolder = SetupRunTimeEnvironment(environment);

                    // Go find APSIM executable
                    if (environment.APSIMxBuildNumber > 0)
                        jobsToRun = GetJobToRunAPSIMX(apsimFileName, binFolder);
                    else
                        jobsToRun = GetJobToRunAPSIMClassic(apsimFileName, binFolder);
                }

                // Copy all errors to our errors list.
                foreach (var sim in allSimulations.FindAll(sim => sim.ErrorMessage != null))
                    Errors.Add(sim.ErrorMessage);
            }
            catch (Exception err)
            {
                Errors.Add(err.ToString());
            }
        }

        /// <summary>Get all errors encountered</summary>
        public List<string> Errors { get; private set; }

        /// <summary>Get all generated outputs</summary>
        public DataSet Outputs { get; private set; }

        /// <summary>Output zip file.</summary>
        public Stream AllFilesZipped { get; private set; }

        /// <summary>Return the yield prophet specification</summary>
        public YieldProphet Spec { get { return specToRun; } }

        /// <summary>Called to get the next job</summary>
        public IRunnable GetNextJobToRun()
        {
            if (jobsToRun == null || jobsToRun.Count == 0)
                return null;

            IRunnable jobToRun = jobsToRun[0];
            jobsToRun.RemoveAt(0);
            return jobToRun;
        }

        /// <summary>Called when all jobs completed</summary>
        public void Completed()
        {
            // Perform cleanup and get outputs.
            Outputs = PerformCleanup(WorkingDirectory);
 
            // Look for error files - apsimx produces these.
            Errors = new List<string>();
            foreach (string errorFile in Directory.GetFiles(WorkingDirectory, "*.error"))
            {
                string error = string.Empty;
                foreach (string line in File.ReadAllLines(errorFile))
                {
                    if (!line.StartsWith("File:") && !line.StartsWith("Finished running simulations"))
                        error += line;
                }
                if (error != string.Empty)
                    Errors.Add(error);
            }

            // Look for error table - apsim classic produces these.
            if (Outputs.Tables.Contains("Error"))
            {
                DataTable errorTable = Outputs.Tables["Error"];
                foreach (DataRow errorRow in errorTable.Rows)
                    Errors.Add(errorRow["Text"].ToString());
            }

            // Zip the temporary directory
            AllFilesZipped = new MemoryStream();
            ZipUtilities.ZipFiles(Directory.GetFiles(WorkingDirectory), null, AllFilesZipped);

            // Get rid of our temporary directory.
            Directory.Delete(WorkingDirectory, true);
        }

        /// <summary>
        /// Ensure the correct runtime is ready to go. Return a path to the correct folder
        /// that contains the APSIM executables.
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        public static string SetupRunTimeEnvironment(RuntimeEnvironment environment)
        {
            string binFolder = null;
            string environmentFolder = null;
            string url = null;
            string commandLineArguments = null;
            if (environment.APSIMRevision != null)
            {
                environmentFolder = environment.APSIMRevision;
                if (environment.RuntimePackage != null)
                {
                    environmentFolder += "-";
                    environmentFolder += environment.RuntimePackage;
                }
                url = @"http://bob.apsim.info/files/" + environment.APSIMRevision + ".binaries.WINDOWS.INTEL.exe";
                binFolder = Path.Combine(environmentFolder, "Temp", "Model");
            }
            if (environment.APSIMxBuildNumber > 0)
            {
                url = WebUtilities.CallRESTService<string>("http://www.apsim.info/APSIM.Builds.Service/Builds.svc/GetURLOfVersionForIssue?issueid=" +
                                                           environment.APSIMxBuildNumber);
                environmentFolder = "ApsimX-" + environment.APSIMxBuildNumber;
                if (environment.RuntimePackage != null)
                {
                    environmentFolder += "-";
                    environmentFolder += environment.RuntimePackage;
                }
                commandLineArguments = "/SILENT /NOICONS /DIR=\".\"";
                binFolder = Path.Combine(environmentFolder, "Bin");
            }
            else if (environment.AusfarmRevision != null)
            {
                environmentFolder = environment.AusfarmRevision;
                if (environment.RuntimePackage != null)
                {
                    environmentFolder += "-";
                    environmentFolder += environment.RuntimePackage;
                }
                string packageFileName = Path.Combine("RuntimePackages", environment.AusfarmRevision + ".zip");
                ZipUtilities.UnZipFiles(packageFileName, environmentFolder, null);
                binFolder = Path.Combine(environmentFolder, "Ausfarm");
            }

            environmentFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), environmentFolder);
            if (!Directory.Exists(environmentFolder))
            {
                string downloadedFileName = Path.Combine(environmentFolder, "Temp.exe");

                // Download the file
                DownloadAndExecuteFile(environmentFolder, url, commandLineArguments, downloadedFileName);

                // Copy in the extra runtime packages.
                if (environment.RuntimePackage != null)
                {
                    string packageFileName = Path.Combine("RuntimePackages", environment.RuntimePackage + ".zip");
                    ZipUtilities.UnZipFiles(packageFileName, environmentFolder, null);
                }
            }

            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), binFolder);
        }

        /// <summary>
        /// Download and execute a file from a url.
        /// </summary>
        /// <param name="environmentFolder">The folder where the file should be downloaded to</param>
        /// <param name="url">The URL to get the file from</param>
        /// <param name="commandLineArguments">Any command line arguments to use when running file</param>
        /// <param name="downloadedFileName">The name of the file to download</param>
        private static void DownloadAndExecuteFile(string environmentFolder, string url, string commandLineArguments, string downloadedFileName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(downloadedFileName));

            if (url != null)
            {
                WebClient myWebClient = new WebClient();
                byte[] bytes = myWebClient.DownloadData(url);
                using (FileStream writer = File.Create(downloadedFileName))
                    writer.Write(bytes, 0, bytes.Length);
            }

            if (Path.GetExtension(downloadedFileName) == ".exe")
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = downloadedFileName,
                    Arguments = commandLineArguments,
                    WorkingDirectory = environmentFolder,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process p = Process.Start(startInfo);
                p.WaitForExit();
            }
            else
            {
                ZipUtilities.UnZipFiles(downloadedFileName, environmentFolder, null);
            }
            
            File.Delete(downloadedFileName);
        }

        /// <summary>
        /// Create and return a job to run APSIM classic.
        /// </summary>
        /// <param name="apsimFileName">The name of the simulation file.</param>
        /// <param name="binFolder">The binary folder where the executables reside</param>
        private static List<IRunnable> GetJobToRunAPSIMClassic(string apsimFileName, string binFolder)
        {
            string workingDirectory = Path.GetDirectoryName(apsimFileName);
            string apsimExecutable = Path.Combine(binFolder, "ApsimModel.exe");
            string binDirectory = Path.GetDirectoryName(apsimExecutable);

            // Convert .apsim file to sims so that we can run ApsimModel.exe rather than Apsim.exe
            // This will avoid using the old APSIM job runner. It assumes though that there are 
            // no other APSIMJob instances running in the workingDirectory. This is because it
            // looks and runs all .sim files it finds in the workingDirectory.
            List<IRunnable> jobs = new List<IRunnable>();
            string[] simFileNames = CreateSimFiles(apsimFileName, workingDirectory, binDirectory);
            foreach (string simFileName in simFileNames)
            {
                if (simFileName == null || simFileName.Trim() == string.Empty)
                    throw new Exception("Blank .sim file names found for apsim file: " + apsimFileName);
                jobs.Add(new RunExternalProcess(apsimExecutable, 
                                                StringUtilities.DQuote(simFileName), 
                                                workingDirectory, 
                                                Path.ChangeExtension(simFileName, ".sum"), 
                                                Path.ChangeExtension(simFileName, ".sum")));
            }

            return jobs;
        }

        /// <summary>Convert .apsim file to .sim files</summary>
        /// <param name="apsimFileName">The .apsim file name.</param>
        /// <param name="workingDirectory">The working directory</param>
        /// <param name="binDirectory">Directory where executables are located</param>
        /// <returns>A list of filenames for all created .sim files.</returns>
        private static string[] CreateSimFiles(string apsimFileName, string workingDirectory, string binDirectory)
        {
            string executable = Path.Combine(binDirectory, "ApsimToSim.exe");
            string sumFileName = Path.ChangeExtension(apsimFileName, ".sum");

            RunExternalProcess job = new RunExternalProcess(executable, 
                                                            StringUtilities.DQuote(apsimFileName),
                                                            workingDirectory,
                                        sumFileName, sumFileName);
            job.Run(null);

            if (File.Exists(sumFileName))
            {
                string msg = File.ReadAllText(sumFileName);
                if (msg != null && msg != string.Empty && !msg.StartsWith("Written "))
                    throw new Exception("ApsimToSim Error:\r\n" + msg);
                else
                    File.Delete(sumFileName);
            }
            return Directory.GetFiles(workingDirectory, "*.sim");
        }

        /// <summary>
        /// Create and return a job to run APSIMx.
        /// </summary>
        /// <param name="apsimFileName">The name of the simulation file.</param>
        /// <param name="binFolder">The binary folder where the executables reside</param>
        private List<IRunnable> GetJobToRunAPSIMX(string apsimFileName, string binFolder)
        {
            string workingDirectory = Path.GetDirectoryName(apsimFileName);
            string apsimExecutable;
            if (ApsimXExecutable == null)
                apsimExecutable = Path.Combine(binFolder, "Models.exe");
            else
                apsimExecutable = ApsimXExecutable;

            List<IRunnable> jobs = new List<IRunnable>
            {
                new RunExternalProcess(apsimExecutable, apsimFileName, workingDirectory,
                             Path.ChangeExtension(apsimFileName, ".error"), 
                             Path.ChangeExtension(apsimFileName, ".error"))
            };
            return jobs;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="workingFolder">Folder to work on</param>
        public DataSet PerformCleanup(string workingFolder)
        {
            // Delete the .sim files.
            string[] simFiles = Directory.GetFiles(workingFolder, "*.sim");
            foreach (string simFile in simFiles)
                File.Delete(simFile);

            bool longtermOutputsFound = false;
            DataSet dataSet = new DataSet("ReportData");

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
                        ConcatenateSummaryFiles(sumFiles, simName + ".sum", dataSet);
                    }
                }

                // Concatenate output files.
                SortedSet<string> outputTypes = new SortedSet<string>();
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simulationName = XmlUtilities.NameAttr(simNode);
                    foreach (string outputType in Directory.GetFiles(workingFolder, simulationName + "_*1*.out"))
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
                string[] apsimFiles = Directory.GetFiles(workingFolder, "*.apsim");
                if (apsimFiles.Length == 0)
                    apsimFiles = Directory.GetFiles(workingFolder, "*.apsimx");
                string apsimFileName1 = apsimFiles[0];
                string[] allSumFiles = Directory.GetFiles(workingFolder, "*.sum");
                ConcatenateSummaryFiles(allSumFiles, Path.ChangeExtension(apsimFileName1, ".sum"), dataSet);

                XmlDocument doc1 = new XmlDocument();
                doc1.Load(apsimFileName1);

                foreach (XmlNode simulationNode in XmlUtilities.ChildNodes(doc1.DocumentElement, "simulation"))
                {
                    if (simulationNode != null)
                    {
                        string simulationName = XmlUtilities.NameAttr(simulationNode);
                        string[] outFileTypes = Directory.GetFiles(workingFolder, simulationName + "*.out");
                        if (outFileTypes.Length == 0)
                            outFileTypes = Directory.GetFiles(workingFolder, simulationName + "*.csv");
                        foreach (string outputfileName in outFileTypes)
                        {
                            string outputFileType = Path.GetFileNameWithoutExtension(outputfileName.Replace(simulationName, ""));
                            string wildcard = "*" + outputFileType + Path.GetExtension(outputfileName);
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

            // Get all outputs
            foreach (string dbFileName in Directory.GetFiles(workingFolder, "*.db"))
            {
                GetDataFromDB(dbFileName, dataSet);
            }
            foreach (string outFileName in Directory.GetFiles(workingFolder, "*.csv"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }
            foreach (string outFileName in Directory.GetFiles(workingFolder, "*.out"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }

            // Clean the table names (no spaces or underscores)
            foreach (DataTable table in dataSet.Tables)
            {
                string tableName = table.TableName.Replace(" ", "");
                tableName = tableName.Replace("_", "");
                table.TableName = tableName;
            }
            return dataSet;
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
                    DataTable data = null;
                    ApsimTextFile reader = new ApsimTextFile();
                    try
                    {
                        reader.Open(outputFileName);

                        List<string> constantsToAdd = new List<string>();
                        constantsToAdd.Add("Title");
                        data = reader.ToTable(constantsToAdd);
                    }
                    finally
                    {
                        reader.Close();
                    }

                    if (data != null && data.Columns.Count > 0 && data.Rows.Count > 0)
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
                    using (StreamWriter outWriter = new StreamWriter(singleOutputFileName))
                        DataTableUtilities.DataTableToText(allData, 0, ",  ", true, outWriter);
                }

                // Delete the .out files.
                foreach (string outputFileName in outFiles)
                    File.Delete(outputFileName);
            }
        }

        /// <summary>Concatenates the summary files.</summary>
        /// <param name="sumFiles">The sum files to concatenate</param>
        /// <param name="fileName">Name of single summary file to create.</param>
        /// <param name="dataset">The dataset to store the summary contents into.</param>
        private static void ConcatenateSummaryFiles(string[] sumFiles, string fileName, DataSet dataset)
        {
            if (sumFiles.Length > 0)
            {
                string workingFolder = Path.GetDirectoryName(sumFiles[0]);
                string singleSummaryFileName = Path.Combine(workingFolder, fileName);
                using (StreamWriter sumWriter = new StreamWriter(singleSummaryFileName))
                {
                    foreach (string summaryFileName in sumFiles)
                    {
                        if (summaryFileName != singleSummaryFileName)
                            using (StreamReader sumReader = new StreamReader(summaryFileName))
                                sumWriter.Write(sumReader.ReadToEnd());
                    }
                }

                // add a summary table to the dataset.
                DataTable summaryTable;
                if (dataset.Tables.Contains("Summary"))
                    summaryTable = dataset.Tables["Summary"];
                else
                {
                    summaryTable = dataset.Tables.Add("Summary");
                    summaryTable.Columns.Add("Text", typeof(string));
                }
                DataRow summaryRow = summaryTable.NewRow();
                summaryRow["Text"] = File.ReadAllText(singleSummaryFileName);

                // add any errors found.
                SummaryFileParser summaryFile = new SummaryFileParser();
                summaryFile.Open(singleSummaryFileName);

                // Delete the .sum files.
                foreach (string summaryFileName in sumFiles)
                    File.Delete(summaryFileName);

                string error = summaryFile.GetAPSIMError();
                if (error != null)
                {
                    // add an error table to the dataset.
                    DataTable errorTable;
                    if (dataset.Tables.Contains("Error"))
                    {
                        errorTable = dataset.Tables["Error"];
                    }
                    else
                    {
                        errorTable = dataset.Tables.Add("Error");
                        errorTable.Columns.Add("Text", typeof(string));
                    }

                    DataRow errorRow = errorTable.NewRow();
                    errorRow["Text"] = error;
                    errorTable.Rows.Add(errorRow);
                }
            }
        }

        /// <summary>Zips the files.</summary>
        /// <param name="intoFileName">The name of the file to create.</param>
        /// <param name="fileNames">The file names to zip.</param>
        private static void ZipFiles(string[] fileNames, string intoFileName)
        {
            // Zip up files.
            ZipUtilities.ZipFiles(fileNames, null, intoFileName);

            // Delete the .met files.
            foreach (string fileName in fileNames)
                File.Delete(fileName);
        }

        /// <summary>Read in data tables from a .db file.</summary>
        private void GetDataFromDB(string dbFileName, DataSet dataSet)
        {
            // Need to change the working folder to the folder where sqlite3.dll is located.
            string savedWorkingFolder = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(binFolder);

            SQLite connection = new SQLite();
            try
            {
                connection.OpenDatabase(dbFileName, readOnly: true);
                foreach (string tableName in connection.GetTableNames())
                {
                    if (!tableName.StartsWith("_"))
                    {
                        DataTable data = connection.ExecuteQuery("SELECT * FROM " + tableName);
                        data.TableName = tableName;
                        dataSet.Tables.Add(data);
                    }
                }

            }
            finally
            {
                connection.CloseDatabase();
                Directory.SetCurrentDirectory(savedWorkingFolder);
            }
        }

    }
}
