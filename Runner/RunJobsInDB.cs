// -----------------------------------------------------------------------
// <copyright file="CheckDBForJobs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Runner
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using APSIM.Cloud;
    using APSIM.Cloud.Specification;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Diagnostics;
    using ApsimFile;

    /// <summary>
    /// This runnable job will periodically check the DB for new jobs that
    /// have been added. When found, they are added to the job manager queue.
    /// </summary>
    public class RunJobsInDB : Utility.JobManager.IRunnable
    {
        private static string ApsimReport = @"D:\ApsimReport\ApsimReport.exe";

        /// <summary>Gets a value indicating whether this instance is computationally time consuming.</summary>
        public bool IsComputationallyTimeConsuming { get { return false; } }

        /// <summary>Gets or sets the error message. Set by the JobManager.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets a value indicating whether this job is completed. Set by the JobManager.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>The current running job description.</summary>
        private JobsService.Job runningJobDescription = null;

        /// <summary>The current running job.</summary>
        private Utility.JobManager.IRunnable runningJob = null;

        /// <summary>Entry point for this job.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        public void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Get our job manager.
            Utility.JobManager jobManager = (Utility.JobManager)e.Argument;

            while (!e.Cancel)
            {
                try
                {
                    // Process jobs that have finished.
                    ProcessCompletedJobs();

                    // Process that have been added.
                    ProcessAddedJobs(jobManager);

                    Thread.Sleep(1000); // 1 seconds
                }
                catch (Exception err)
                {
                    WriteToLog(err.Message);
                }
            }
        }

        /// <summary>Processes any jobs that have been added.</summary>
        /// <param name="jobManager">The job manager.</param>
        private void ProcessAddedJobs(Utility.JobManager jobManager)
        {
            if (runningJobDescription == null)
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    runningJobDescription = jobsClient.GetNextToRun();
                    if (runningJobDescription != null)
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(jobsClient.GetJobXML(runningJobDescription.Name));

                        runningJob = GetRunnableJob(runningJobDescription.Name, doc.DocumentElement, jobsClient);

                        if (runningJob == null)
                        {
                            jobsClient.AddLogMessage("Invalid job XML found. Job: " + runningJobDescription.Name + ". Job - ignored", true);
                            jobsClient.SetCompleted(runningJobDescription.Name, "Invalid job XML found.");
                        }
                        else
                            jobManager.AddJob(runningJob);
                    }
                }

                if (runningJobDescription != null)
                {
                    // No jobs to run so wait a bit.
                    Thread.Sleep(30 * 1000); // 30 sec.
                }
            }
        }

        /// <summary>Processes all completed jobs.</summary>
        private void ProcessCompletedJobs()
        {
            if (runningJob != null && runningJob.IsCompleted)
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    // Tell the job system that job is complete.
                    jobsClient.SetCompleted(runningJobDescription.Name, runningJob.ErrorMessage);
                    runningJob = null;
                    runningJobDescription = null;
                }
            }
        }

        /// <summary>
        /// Writes to log.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        private void WriteToLog(string errorMessage)
        {
            try
            {
                using (JobsService.JobsClient jobsClient = new JobsService.JobsClient())
                {
                    jobsClient.AddLogMessage(errorMessage, true);
                }
                Thread.Sleep(1000);  // 1 sec.
            }
            catch (Exception)
            {
                // Network must be down - wait 5 minutes
                Thread.Sleep(1000 * 60 * 5);
            }
        }



        /// <summary>Gets a runnable job from the specified XML.</summary>
        /// <param name="name">The name of the job.</param>
        /// <param name="node">The job XML node</param>
        /// <returns>The newly created runnable job or NULL if not valid job XML.</returns>
        private Utility.JobManager.IRunnable GetRunnableJob(string name, XmlNode node, JobsService.JobsClient jobsClient)
        {
            if (node.Name == "YieldProphet")
                return CreateRunnableJob(name, node, jobsClient);

            return null;
        }

        /// <summary>Create a runnable job for the APSIM simulations</summary>
        /// <param name="FilesToRun">The files to run.</param>
        /// <returns>A runnable job for all simulations</returns>
        private Utility.JobManager.IRunnable CreateRunnableJob(string reportName, XmlNode node, JobsService.JobsClient jobsClient)
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            // Create a YieldProphet object from our YP xml file
            JobsService.YieldProphet spec = jobsClient.YieldProphetFromXML(node.OuterXml);

            // Convert YieldProphet spec into a simulation set.
            List<APSIM.Cloud.Specification.APSIMSpec> simulations = ToAPSIM(spec);

            // Create all the files needed to run APSIM.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory);

            // Fill in calculated fields.
            foreach (JobsService.Paddock paddock in spec.Paddock)
                FillInCalculatedFields(paddock, paddock.ObservedData, workingDirectory);

            // Save YieldProphet.xml to working folder.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(jobsClient.YieldProphetToXML(spec));
            doc.Save(Path.Combine(workingDirectory, "YieldProphet.xml"));
            
            string apsimHomeDirectory = Path.GetDirectoryName(RunnableJobs.APSIMJob.localAPSIMExe);
            apsimHomeDirectory = Path.GetDirectoryName(apsimHomeDirectory); // parent.
            Configuration.SetApsimDir(apsimHomeDirectory);
            PlugIns.LoadAll();

            List<Utility.JobManager.IRunnable> jobs = new List<Utility.JobManager.IRunnable>();
            XmlDocument Doc = new XmlDocument();
            Doc.Load(apsimFileName);

            List<XmlNode> simulationNodes = new List<XmlNode>();
            Utility.Xml.FindAllRecursivelyByType(Doc.DocumentElement, "simulation", ref simulationNodes);

            foreach (XmlNode simulation in simulationNodes)
            {
                if (Utility.Xml.Attribute(simulation, "enabled") != "no")
                {
                    XmlNode factorialNode = Utility.Xml.FindByType(Doc.DocumentElement, "factorial");
                    if (factorialNode == null)
                    {
                        RunnableJobs.APSIMJob job = new RunnableJobs.APSIMJob(
                           fileName: apsimFileName,
                           arguments: "Simulation=" + Utility.Xml.FullPath(simulation));
                        jobs.Add(job);
                    }
                    else
                    {
                        ApsimFile F = new ApsimFile();

                        // The OpenFile method below changes the current directory: We don't want
                        // this. Restore the current directory after calling it.
                        string cwd = Directory.GetCurrentDirectory();
                        F.OpenFile(apsimFileName);
                        Directory.SetCurrentDirectory(cwd);

                        FactorBuilder builder = new FactorBuilder();
                        string path = "/" + Utility.Xml.FullPath(simulation);
                        path = path.Remove(path.LastIndexOf('/'));
                        List<string> simsToRun = new List<string>();
                        foreach (FactorItem item in builder.BuildFactorItems(F.FactorComponent, path))
                        {
                            List<String> factorials = new List<string>();
                            item.CalcFactorialList(factorials);
                            foreach (string factorial in factorials)
                                simsToRun.Add(path + "@factorial='" + factorial + "'");
                        }
                        List<SimFactorItem> simFactorItems = Factor.CreateSimFiles(F, simsToRun.ToArray(), workingDirectory);

                        foreach (SimFactorItem simFactorItem in simFactorItems)
                        {

                            RunnableJobs.APSIMJob job = new RunnableJobs.APSIMJob(
                                fileName: simFactorItem.SimFileName);
                            jobs.Add(job);
                        }
                    }
                }
            }

            // Create a sequential job.
            Utility.JobSequence completeJob = new Utility.JobSequence();
            completeJob.Jobs = new List<Utility.JobManager.IRunnable>();
            completeJob.Jobs.Add(new Utility.JobParallel() { Jobs = jobs });
            completeJob.Jobs.Add(new RunnableJobs.APSIMPostSimulationJob(workingDirectory));
            completeJob.Jobs.Add(new RunnableJobs.YPPostSimulationJob(reportName, spec.Paddock[0].NowDate, workingDirectory));

            return completeJob;
        }

        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="yieldProphet">The yield prophet spec.</param>
        /// <param name="endDate">The end date for using any observed data.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <returns>The name of the created .apsim file.</returns>
        private static List<Specification.APSIMSpec> ToAPSIM(JobsService.YieldProphet yieldProphet)
        {
            if (yieldProphet.ReportType == JobsService.YieldProphet.ReportTypeEnum.Crop)
                return CropReport(yieldProphet);
            else if (yieldProphet.ReportType == JobsService.YieldProphet.ReportTypeEnum.SowingOpportunity)
                return SowingOpportunityReport(yieldProphet);

            return null;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static Specification.APSIMSpec CreateBaseSimulation(JobsService.Paddock paddock)
        {
            JobsService.Paddock copyOfPaddock = paddock; // Utility.Xml.Clone(paddock) as JobsService.Paddock;
            copyOfPaddock.ObservedData = paddock.ObservedData;

            Specification.APSIMSpec shortSimulation = new Specification.APSIMSpec();
            shortSimulation.Name = "Base";

            shortSimulation.StartDate = new DateTime(copyOfPaddock.SoilWaterSampleDate.Year, 4, 1);
            APSIM.Cloud.Runner.JobsService.Sow sow = Specification.Utils.GetCropBeingSown(paddock.Management);
            if (sow != null && sow.Date < shortSimulation.StartDate)
                shortSimulation.StartDate = sow.Date;

            shortSimulation.EndDate = copyOfPaddock.NowDate;
            shortSimulation.NowDate = copyOfPaddock.NowDate;
            if (shortSimulation.NowDate == DateTime.MinValue)
                shortSimulation.NowDate = DateTime.Now;
            shortSimulation.DailyOutput = true;
            shortSimulation.ObservedData = copyOfPaddock.ObservedData;
            shortSimulation.Soil = copyOfPaddock.Soil;
            shortSimulation.SoilPath = copyOfPaddock.SoilPath;
            shortSimulation.Samples = new List<JobsService.Sample>();
            shortSimulation.Samples.AddRange(copyOfPaddock.Samples);
            shortSimulation.StationNumber = copyOfPaddock.StationNumber;
            shortSimulation.StubbleMass = copyOfPaddock.StubbleMass;
            shortSimulation.StubbleType = copyOfPaddock.StubbleType;
            shortSimulation.Management = new List<JobsService.Management>();
            shortSimulation.Management.AddRange(copyOfPaddock.Management);
            AddResetDatesToManagement(copyOfPaddock, shortSimulation);
            return shortSimulation;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static List<Specification.APSIMSpec> CropReport(JobsService.YieldProphet yieldProphet)
        {
            List<Specification.APSIMSpec> simulations = new List<Specification.APSIMSpec>();
            JobsService.Paddock paddock = yieldProphet.Paddock[0];

            Specification.APSIMSpec thisYear = CreateBaseSimulation(paddock);
            thisYear.Name = "ThisYear";
            thisYear.WriteDepthFile = true;
            simulations.Add(thisYear);

            Specification.APSIMSpec seasonSimulation = CreateBaseSimulation(paddock);
            seasonSimulation.Name = "Base";
            seasonSimulation.DailyOutput = false;
            seasonSimulation.YearlyOutput = true;
            seasonSimulation.EndDate = seasonSimulation.StartDate.AddDays(300);
            simulations.Add(seasonSimulation);

            Specification.APSIMSpec NUnlimitedSimulation = CreateBaseSimulation(paddock);
            NUnlimitedSimulation.Name = "NUnlimited";
            NUnlimitedSimulation.DailyOutput = false;
            NUnlimitedSimulation.YearlyOutput = true;
            NUnlimitedSimulation.EndDate = NUnlimitedSimulation.StartDate.AddDays(300);
            NUnlimitedSimulation.NUnlimited = true;
            simulations.Add(NUnlimitedSimulation);

            Specification.APSIMSpec NUnlimitedFromTodaySimulation = CreateBaseSimulation(paddock);
            NUnlimitedFromTodaySimulation.Name = "NUnlimitedFromToday";
            NUnlimitedFromTodaySimulation.DailyOutput = false;
            NUnlimitedFromTodaySimulation.YearlyOutput = true;
            NUnlimitedFromTodaySimulation.EndDate = NUnlimitedFromTodaySimulation.StartDate.AddDays(300);
            NUnlimitedFromTodaySimulation.NUnlimitedFromToday = true;
            simulations.Add(NUnlimitedFromTodaySimulation);

            Specification.APSIMSpec Next10DaysDry = CreateBaseSimulation(paddock);
            Next10DaysDry.Name = "Next10DaysDry";
            Next10DaysDry.DailyOutput = false;
            Next10DaysDry.YearlyOutput = true;
            Next10DaysDry.EndDate = Next10DaysDry.StartDate.AddDays(300);
            Next10DaysDry.Next10DaysDry = true;
            simulations.Add(Next10DaysDry);
            return simulations;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static List<Specification.APSIMSpec> SowingOpportunityReport(JobsService.YieldProphet yieldProphet)
        {
            List<Specification.APSIMSpec> simulations = new List<Specification.APSIMSpec>();
            JobsService.Paddock paddock = yieldProphet.Paddock[0];

            DateTime sowingDate = new DateTime(paddock.StartSeasonDate.Year, 3, 15);
            DateTime lastSowingDate = new DateTime(paddock.StartSeasonDate.Year, 7, 5);
            while (sowingDate <= lastSowingDate)
            {
                Specification.APSIMSpec sim = CreateBaseSimulation(paddock);
                sim.Name = sowingDate.ToString("ddMMM");
                sim.DailyOutput = false;
                sim.YearlyOutput = true;
                sim.WriteDepthFile = false;
                sim.StartDate = sowingDate;
                sim.EndDate = sim.StartDate.AddDays(300);

                JobsService.Sow simSowing = Specification.Utils.GetCropBeingSown(sim.Management);
                simSowing.Date = sowingDate;
                simulations.Add(sim);

                sowingDate = sowingDate.AddDays(5);
            }

            return simulations;
        }

        /// <summary>Converts reset dates to management operations in in the simulation.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="simulation">The simulation to add the management operations to.</param>
        /// <exception cref="System.Exception">Cannot find soil water reset date</exception>
        /// <exception cref="Exception">Cannot find soil water reset date</exception>
        private static void AddResetDatesToManagement(JobsService.Paddock paddock, Specification.APSIMSpec simulation)
        {
            // Reset
            if (paddock.SoilWaterSampleDate == DateTime.MinValue)
                throw new Exception("Cannot find soil water reset date");

            if (paddock.SoilNitrogenSampleDate == DateTime.MinValue)
                paddock.SoilNitrogenSampleDate = paddock.SoilWaterSampleDate;

            JobsService.Sow sowing = Specification.Utils.GetCropBeingSown(paddock.Management);
            if (sowing != null && sowing.Date != DateTime.MinValue)
            {
                // reset at sowing if the sample dates are after sowing.
                if (paddock.SoilWaterSampleDate > sowing.Date)
                {
                    simulation.Management.Add(new JobsService.ResetWater() { Date = sowing.Date });
                    simulation.Management.Add(new JobsService.ResetSurfaceOrganicMatter() { Date = sowing.Date });
                }
                if (paddock.SoilNitrogenSampleDate > sowing.Date)
                    simulation.Management.Add(new JobsService.ResetNitrogen() { Date = sowing.Date });

                // reset on the sample dates.
                simulation.Management.Add(new JobsService.ResetWater() { Date = paddock.SoilWaterSampleDate });
                simulation.Management.Add(new JobsService.ResetSurfaceOrganicMatter() { Date = paddock.SoilWaterSampleDate });
                simulation.Management.Add(new JobsService.ResetNitrogen() { Date = paddock.SoilNitrogenSampleDate });
            }
        }

        #region Calculated fields

        /// <summary>Fills the auto-calculated fields.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="observedData">The observed data.</param>
        /// <param name="weatherData">The weather data.</param>
        private static void FillInCalculatedFields(JobsService.Paddock paddock, DataTable observedData, string workingFolder)
        {
            IEnumerable<JobsService.Tillage> tillages = paddock.Management.OfType<JobsService.Tillage>();
            if (tillages.Count() > 0)
                paddock.StubbleIncorporatedPercent = APSIM.Cloud.Specification.Utils.CalculateAverageTillagePercent(tillages);

            DateTime lastRainfallDate = GetLastRainfallDate(observedData);
            if (lastRainfallDate != DateTime.MinValue)
                paddock.DateOfLastRainfallEntry = lastRainfallDate.ToString("dd/MM/yyyy");

            string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
            if (metFiles.Length > 0)
            {
                string firstMetFile = Path.Combine(workingFolder, metFiles[0]);
                Utility.ApsimTextFile textFile = new Utility.ApsimTextFile();
                textFile.Open(firstMetFile);
                DataTable data = textFile.ToTable();
                textFile.Close();
                paddock.RainfallSinceSoilWaterSampleDate = SumTableAfterDate(data, "Rain", paddock.SoilWaterSampleDate);
                if (data.Rows.Count > 0)
                {
                    DataRow lastweatherRow = data.Rows[data.Rows.Count - 1];
                    paddock.LastClimateDate = Utility.DataTable.GetDateFromRow(lastweatherRow);
                }
            }
        }

        /// <summary>Gets the last rainfall date in the observed data.</summary>
        /// <param name="observedData">The observed data.</param>
        /// <returns>The date of the last rainfall row or DateTime.MinValue if no data.</returns>
        private static DateTime GetLastRainfallDate(DataTable observedData)
        {
            if (observedData == null || observedData.Rows.Count == 0)
                return DateTime.MinValue;

            int lastRowIndex = observedData.Rows.Count - 1;
            return Utility.DataTable.GetDateFromRow(observedData.Rows[lastRowIndex]);
        }

        /// <summary>Sums a column of a data table between dates.</summary>
        /// <param name="observedData">The data table.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="date1">The date1.</param>
        /// <param name="date2">The date2.</param>
        /// <returns>The sum of rainfall.</returns>
        private static double SumTableAfterDate(DataTable data, string columnName, DateTime date1)
        {
            double sum = 0;
            if (data.Columns.Contains("Rain"))
            {
                foreach (DataRow row in data.Rows)
                {
                    DateTime rowDate = Utility.DataTable.GetDateFromRow(row);
                    if (rowDate >= date1)
                        sum += Convert.ToDouble(row[columnName]);
                }
            }

            return sum;
        }
        #endregion
    }
}
