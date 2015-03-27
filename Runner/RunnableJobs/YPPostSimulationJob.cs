// -----------------------------------------------------------------------
// <copyright file="YPPostSimulationJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.IO;
    using System.Reflection;
    using System.Diagnostics;
    using System.Data;

    /// <summary>
    /// A runnable class for Yield Prophet cleanup
    /// </summary>
    public class YPPostSimulationJob : Utility.JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return true; } }
        
        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>The apsim report executable path.</summary>
        private static string apsimReport = @"D:\ApsimReport\ApsimReport.exe";

        /// <summary>The apsim report executable path.</summary>
        private static string archiveLocation = @"ftp://www.apsim.info/YP/Archive";
                
        /// <summary>Gets or sets the working directory.</summary>
        private string workingDirectory;

        /// <summary>The now date for generating reports.</summary>
        private DateTime nowDate;

        /// <summary>The report name as known by the jobs database.</summary>
        private string reportName;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the apsim file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public YPPostSimulationJob(string reportName, DateTime nowDate, string workingDirectory)
        {
            this.reportName = reportName;
            this.nowDate = nowDate;
            this.workingDirectory = workingDirectory;
        }

        /// <summary>Called to start the job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Read in the yield prophet specification.
            StreamReader reader = new StreamReader(Path.Combine(workingDirectory, "YieldProphet.xml"));
            JobsService.YieldProphet yieldProphet;
            using (JobsService.JobsClient jobClient = new JobsService.JobsClient())
            {
                yieldProphet = jobClient.YieldProphetFromXML(reader.ReadToEnd());
                reader.Close();
            }

            // Call the YP reporting webservice.
            DataSet dataSet = new DataSet("ReportData");
            foreach (string outFileName in Directory.GetFiles(workingDirectory, "*.out"))
                try
                {
                    dataSet.Tables.Add(Utility.ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {

                }

            // Call StoreReport
            using (YPReporting.ReportingClient reportingClient = new YPReporting.ReportingClient())
            {
                reportingClient.StoreReport(reportName, dataSet);
            }

            // copy in the report file.
            string reportFileName = Path.Combine(workingDirectory, yieldProphet.ReportType + ".report");
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Runner.Resources." + yieldProphet.ReportType + ".report");
            XmlDocument doc = new XmlDocument(); 
            doc.Load(s);
            doc.Save(reportFileName);

            // run ApsimReport to generate .GIF files and a .PDF
            string archiveBaseFileName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = apsimReport;
            startInfo.Arguments = Utility.String.DQuote(reportFileName) + " " +
                                  Utility.String.DQuote(archiveBaseFileName + ".gif");
            startInfo.WorkingDirectory = workingDirectory;
            Process process = Process.Start(startInfo);
            process.WaitForExit();
            startInfo.Arguments = startInfo.Arguments.Replace(".gif", ".pdf");
            process = Process.Start(startInfo);
            process.WaitForExit();

            // Zip the temporary directory and send to archive.
            string zipFileName = Path.Combine(workingDirectory, archiveBaseFileName + ".zip");
            Utility.Zip.ZipFiles(Directory.GetFiles(workingDirectory), zipFileName, null);
            Utility.FTPClient.Upload(zipFileName, archiveLocation + "/" + archiveBaseFileName + ".zip", "Administrator", "CsiroDMZ!");

            // Get rid of our temporary directory.
            Directory.Delete(workingDirectory, true);
        }


    }
}
