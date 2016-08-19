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
    using System.ComponentModel;

    /// <summary>
    /// A runnable class for Yield Prophet cleanup
    /// </summary>
    public class YPPostSimulationJob : JobManager.IRunnable, JobManager.IComputationalyTimeConsuming
    {
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
        /// <param name="jobManager">Job manager</param>
        /// <param name="worker">Background worker</param>
        public void Run(JobManager jobManager, BackgroundWorker worker)
        {
            // Read in the yield prophet specification.
            string[] xmlFiles = Directory.GetFiles(workingDirectory, "*.xml");
            if (xmlFiles.Length == 0)
                throw new Exception("Cannot find yieldprophet xml file in working directory.");

            string yieldProphetFileName = xmlFiles[0];
            StreamReader reader = new StreamReader(yieldProphetFileName);
            YieldProphet yieldProphet = YieldProphetUtility.YieldProphetFromXML(reader.ReadToEnd(), workingDirectory);
            reader.Close();

            // Call the YP reporting webservice.
            DataSet dataSet = new DataSet("ReportData");
            foreach (string outFileName in Directory.GetFiles(workingDirectory, "*.csv"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }
            foreach (string outFileName in Directory.GetFiles(workingDirectory, "*.out"))
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
            else if (yieldProphet.ReportName != null && yieldProphet.ReportName.Length > 4)
            {
                // YieldProphet - StoreReport
                // validation runs have a report name of the year e.g. 2015. 
                // Don't need to call StoreReport for them.
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
