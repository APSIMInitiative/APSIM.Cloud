// -----------------------------------------------------------------------
// <copyright file="Run.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ApsimFile;
    using System.IO;
    using System.Threading;
    using System.Diagnostics;
    using System.Reflection;
    using System.Xml;

    /// <summary>
    /// Run services.
    /// </summary>
    public class APSIMRun
    {
        static string LocalAPSIMExe = @"C:\Users\hol353\APSIM\Model\Apsim.exe";


        /// <summary>
        /// Run the file locally. Control doesn't return until run is complete.
        /// </summary>
        /// <param name="simulations">The simulation set to run.</param>
        /// <param name="workingDirectory">The working directory</param>
        public static void Locally(IEnumerable<Specification.APSIM> simulations, 
                                   string workingDirectory)
        {
            // Create all necessary APSIM files.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = LocalAPSIMExe;
            info.Arguments = apsimFileName;
            info.WorkingDirectory = workingDirectory;
            Process processID = Process.Start(info);
            processID.WaitForExit();
            PostSimulation(simulations, workingDirectory);
        }

        /// <summary>
        /// Run the file on the cluster. Control doesn't return until run is complete.
        /// </summary>
        /// <param name="simulations">The simulation set to run.</param>
        /// <param name="workingDirectory">The working directory</param>
        public static void OnCluster(IEnumerable<Specification.APSIM> simulations,
                                     string workingDirectory)
        {
            // Create all necessary APSIM files.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory);

            CondorJob c = new CondorJob();
            c.NiceUser = false;
            c.username = "hol353";
            c.password = "2144mcr!";
            c.doUpload = true;
            c.arch = Configuration.architecture.win32;// | Configuration.architecture.unix;
            c.DestinationFolder = workingDirectory;
            c.SelfExtractingExecutableLocation = @"http://bob.apsim.info/files/Apsim7.6-r3587.binaries.$$(OpSys).$$(Arch).exe";
            c.numberSimsPerJob = 1;

            List<string> filesToRun = new List<string>();
            filesToRun.Add(apsimFileName);
            string clusterID = c.Go(filesToRun, OnUpdateProgress);

            // The cluster zips up it's files so we want to unzip them now.
            foreach (string zipFileName in Directory.GetFiles(workingDirectory, "*.zip"))
            {
                Zip.UnZipFiles(zipFileName, workingDirectory, null);
                File.Delete(zipFileName);
            }

            // Wait for the cluster to finish the job.
            ApsimCondor a = new ApsimCondor();
            a.Credentials = new System.Net.NetworkCredential("hol353", "2144mcr!");
            string status = a.GetField(clusterID, "status");
            while (status != "finished" && status != "error")
            {
                Thread.Sleep(15 * 1000); // 15 sec
                status = a.GetField(clusterID, "status");
            }

            // Get the outputs from the cluster into our temporary directory.
            string fileName = Path.GetFileName(a.GetField(clusterID, "output"));
            System.Net.WebClient webClient = new System.Net.WebClient();
            webClient.Credentials = new System.Net.NetworkCredential("hol353", "2144mcr!");
            byte[] data = webClient.DownloadData("https://apsrunet.apsim.info/condor/download.cgi?filedesc=" + fileName);
            MemoryStream s = new MemoryStream(data);
            Zip.UnZipFiles(s, workingDirectory, null);

            PostSimulation(simulations, workingDirectory);

            // Clean up server.
            //webClient.UploadString("https://apsrunet.apsim.info/condor/rm.cgi", "filedesc=" + fileName);
            //webClient.UploadString("https://apsrunet.apsim.info/condor/rm.cgi", "filedesc=" + fileName.Replace(".out", ""));
        }

        /// <summary>Dummy handler for send to cluster method above</summary>
        /// <param name="percent">The percent complete.</param>
        /// <param name="message">The message.</param>
        private static void OnUpdateProgress(int percent, string message)
        {
        }

        /// <summary>Runs the yield prophet specification, either locally or on the cluster.</summary>
        /// <param name="yieldProphet">The yield prophet.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static void PostSimulation(IEnumerable<Specification.APSIM> simulations, string workingFolder)
        {
            Thread.Sleep(1000); // 1 sec

            // Delete the .sim files.
            string[] simFiles = Directory.GetFiles(workingFolder, "*.sim");
            foreach (string simFile in simFiles)
                File.Delete(simFile);

            // Concatenate summary files.
            foreach (Specification.APSIM simulation in simulations)
            {
                string[] sumFiles = Directory.GetFiles(workingFolder, simulation.Name + "_*.sum");
                if (sumFiles.Length > 0)
                    ConcatenateSummaryFiles(sumFiles, simulation.Name + ".sum");
            }

            // Concatenate yearly output files.
            foreach (Specification.APSIM simulation in simulations)
            {
                string[] outFiles = Directory.GetFiles(workingFolder, simulation.Name + "_* Yearly.out");
                if (outFiles.Length > 0)
                    ConcatenateOutputFiles(outFiles, simulation.Name + " Yearly.out");
            }

            // Concatenate monthly output files.
            foreach (Specification.APSIM simulation in simulations)
            {
                string[] outFiles = Directory.GetFiles(workingFolder, simulation.Name + "_* Monthly.out");
                if (outFiles.Length > 0)
                    ConcatenateOutputFiles(outFiles, simulation.Name + " Monthly.out");
            }

            // Concatenate daily output files.
            foreach (Specification.APSIM simulation in simulations)
            {
                string[] outFiles = Directory.GetFiles(workingFolder, simulation.Name + "_* Daily.out");
                if (outFiles.Length > 0)
                    ConcatenateOutputFiles(outFiles, simulation.Name + " Daily.out");
            }

            // zip up the met files.
            string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
            ZipFiles(metFiles, Path.Combine(workingFolder, "MetFiles.zip"));
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
