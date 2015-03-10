// -----------------------------------------------------------------------
// <copyright file="RunAPSIMJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.JobRunner
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using ApsimFile;
    using System.Text;
    using System.Data;
    using APSIM.Cloud.Services.Specification;
    using APSIM.Cloud.Services;

    public class RunAPSIMJob : Utility.JobManager.IRunnable
    {
        
        /// <summary>Gets or sets the yield prophet spec.</summary>
        public YieldProphet YieldProphetSpec { get; set; }

        /// <summary>Gets or sets the rainfall file contents.</summary>
        public string RainfallFileContents { get; set; }

        // The name of the job.
        public string Name { get; set; }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            JobsDB DB = new JobsDB();
            DB.Open();
            DB.SetJobStatus(Name, JobsDB.StatusEnum.Running);

            // Create a working directory where we can put our files into.
            string workingFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingFolder);
            try
            {
                MemoryStream observedDataStream = new MemoryStream(Encoding.ASCII.GetBytes(RainfallFileContents));
                Utility.ApsimTextFile observedDataFile = new Utility.ApsimTextFile();
                observedDataFile.Open(observedDataStream);
                DataTable observedData = observedDataFile.ToTable();
                observedDataFile.Close();
                YieldProphetSpec.PaddockList[0].ObservedData = observedData;

                // Create the .apsim file and other necessary files
                List<APSIM> simulations = YieldProphetServices.ToAPSIM(YieldProphetSpec, workingFolder);

                string apsimFileName = APSIMFiles.Create(simulations, workingFolder);

                // Run the .apsim file on the cluster and get the outputs back
                SendToCluster(apsimFileName, workingFolder);

                DB.SetJobStatus(Name, JobsDB.StatusEnum.Completed);
            }
            catch (Exception err)
            {
                DB.SetJobStatus(Name, JobsDB.StatusEnum.Error);
                DB.AddLogMessage(Name + ": " + err.Message, true);
            }

            // Zip the temporary directory and send to archive.
            string archiveFileName = Path.Combine(workingFolder, Name + ".zip");
            Utility.Zip.ZipFiles(Directory.GetFiles(workingFolder), archiveFileName, null);
            Utility.FTPClient.Upload(archiveFileName, "ftp://www.apsim.info/YP/Archive/" + Name + ".zip", "Administrator", "CsiroDMZ!");
            DB.SetJobURL(Name, "http://www.apsim.info/YP/Archive/" + Name + ".zip");

            // Get rid of our temporary directory.
            Directory.Delete(workingFolder, true);
        }

        /// <summary>
        /// Send the file to the cluster.
        /// </summary>
        /// <param name="apsimFileName"></param>
        /// <param name="workingFolder"></param>
        /// <returns></returns>
        private void SendToCluster(string apsimFileName, string workingFolder)
        {
            CondorJob c = new CondorJob();
            c.NiceUser = false;
            c.username = "hol353";
            c.password = "2144mcr!";
            c.doUpload = true;
            c.arch = Configuration.architecture.win32;// | Configuration.architecture.unix;
            c.DestinationFolder = workingFolder;
            c.SelfExtractingExecutableLocation = @"http://bob.apsim.info/files/Apsim7.6-r3587.binaries.$$(OpSys).$$(Arch).exe";
            c.numberSimsPerJob = 1;

            List<string> filesToRun = new List<string>();
            filesToRun.Add(apsimFileName);
            string clusterID = c.Go(filesToRun, OnUpdateProgress);

            // The cluster zips up it's files so we want to unzip them now.
            foreach (string zipFileName in Directory.GetFiles(workingFolder, "*.zip"))
            {
                Zip.UnZipFiles(zipFileName, workingFolder, null);
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
            Zip.UnZipFiles(s, workingFolder, null);

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
    }
}