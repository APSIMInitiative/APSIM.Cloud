// -----------------------------------------------------------------------
// <copyright file="RunF4PJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using APSIM.Cloud.Shared.AusFarm;
    using APSIM.Shared.Utilities;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Xml;

    /// <summary>
    /// Run a Farm4Prophet job
    /// </summary>
    public class RunF4PJob : IJobManager
    {
        private List<string> files;
        private string workingDirectory;
        private string binFolder;

        /// <summary>Constructor</summary>
        /// <param name="xml">Job specification xml.</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        public RunF4PJob(string xml, RuntimeEnvironment environment)
        {
            // Save f4p.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            doc.Save(Path.Combine(workingDirectory, "f4p.xml"));

            Farm4Prophet spec = Farm4ProphetUtility.Farm4ProphetFromXML(xml);
            Initialise(spec, environment);
        }

        /// <summary>Constructor</summary>
        /// <param name="f4p">Job specification</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        public RunF4PJob(Farm4Prophet f4p, RuntimeEnvironment environment)
        {
            Initialise(f4p, environment);
        }

        /// <summary>Initialise the job</summary>
        /// <param name="f4p">Job specification</param>
        /// <param name="environment">The runtime environment to use for the run</param>
        private void Initialise(Farm4Prophet f4p, RuntimeEnvironment environment)
        {
            List<AusFarmSpec> simulations = Farm4ProphetToAusFarm.ToAusFarm(f4p);

            // Create a working directory.
            workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            // Writes the sdml files to the workingDirectory and returns a list of names
            files = AusFarmFiles.Create(simulations, workingDirectory).ToList();
            
            // Setup the runtime environment.
            binFolder = RunYPJob.SetupRunTimeEnvironment(environment);

            // Copy in the .prm files.
            foreach (string prmFileName in Directory.GetFiles(binFolder, "*.prm"))
                File.Copy(prmFileName, Path.Combine(workingDirectory, Path.GetFileName(prmFileName)));
            
        }

        /// <summary>Get all errors encountered</summary>
        public List<string> Errors { get; private set; }

        /// <summary>Get all generated outputs</summary>
        public DataSet Outputs { get; private set; }

        /// <summary>Output zip file.</summary>
        public Stream AllFilesZipped { get; private set; }

        /// <summary>Called to get the next job</summary>
        public IRunnable GetNextJobToRun()
        {
            if (files.Count == 0)
                return null;

            string fileToRun = files[0];
            files.RemoveAt(0);

            string sdmlFileName = Path.Combine(workingDirectory, fileToRun);
            return new RunExternalProcess(
                executable: Path.Combine(binFolder, "auscmd32.exe"),
                arguments: sdmlFileName,
                workingDirectory: workingDirectory,
                stdoutFileName:Path.ChangeExtension(sdmlFileName, ".sum"),
                stderrFileName: Path.ChangeExtension(sdmlFileName, ".sum"));
        }

        /// <summary>Called when all jobs completed</summary>
        public void Completed()
        {
            // Look for an error log file. Seems the error file gets written to the AusFarm
            // directory rather than the same place as the .sdml.
            Errors = new List<string>();
            foreach (string errorFile in Directory.GetFiles(binFolder, "*_errors.log"))
            {
                Errors.Add(File.ReadAllText(errorFile));
                File.Delete(errorFile);
            }

            // Zip the temporary directory
            AllFilesZipped = new MemoryStream();
            ZipUtilities.ZipFiles(Directory.GetFiles(workingDirectory), null, AllFilesZipped);

            // Get rid of our temporary directory.
            Directory.Delete(workingDirectory, true);
        }


    }
}
