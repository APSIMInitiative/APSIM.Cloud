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
    using APSIM.Cloud.Shared;
    using APSIM.Shared.Utilities;

    /// <summary>
    /// A runnable class for Yield Prophet cleanup
    /// </summary>
    public class YPPostSimulationJob : JobManager.IRunnable
    {
        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return true; } }
        
        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }
            
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
            YieldProphet yieldProphet = YieldProphetUtility.YieldProphetFromXML(reader.ReadToEnd());
            reader.Close();

            // copy in the report file.
            string reportFileName = Path.Combine(workingDirectory, yieldProphet.ReportType + ".report");
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Runner.Resources." + yieldProphet.ReportType + ".report");
            if (s != null)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(s);
                doc.Save(reportFileName);

                // run ApsimReport to generate .GIF files and a .PDF
                string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                string archiveBaseFileName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(binDirectory, @"ApsimReport\ApsimReport.exe");
                startInfo.Arguments = StringUtilities.DQuote(reportFileName) + " " +
                                      StringUtilities.DQuote(archiveBaseFileName + ".gif");
                startInfo.WorkingDirectory = workingDirectory;
                Process process = Process.Start(startInfo);
                process.WaitForExit();
                startInfo.Arguments = startInfo.Arguments.Replace(".gif", ".pdf");
                process = Process.Start(startInfo);
                process.WaitForExit();
            }

            // Call the YP reporting webservice.
            DataSet dataSet = new DataSet("ReportData");
            foreach (string outFileName in Directory.GetFiles(workingDirectory, "*.out"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }


            if (yieldProphet.ReportType == YieldProphet.ReportTypeEnum.F4P)
            {
                // Farm 4 Prophet - StoreReport
                using (F4P.F4PClient f4pClient = new F4P.F4PClient())
                {
                    try
                    {
                        f4pClient.StoreReport(reportName, dataSet);
                    }
                    catch (Exception)
                    {
                        throw new Exception("Cannot call F4P StoreReport web service method");
                    }
                }
            }
            else
            {
                // YieldProphet - StoreReport
                using (YPReporting.ReportingClient ypClient = new YPReporting.ReportingClient())
                {
                    try
                    {
                        ypClient.StoreReport(reportName, dataSet);
                    }
                    catch (Exception)
                    {
                        throw new Exception("Cannot call YP StoreReport web service method");
                    }
                }
            }
        }


    }
}
