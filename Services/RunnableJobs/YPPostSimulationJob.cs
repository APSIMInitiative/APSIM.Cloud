// -----------------------------------------------------------------------
// <copyright file="YPPostSimulationJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services.RunnableJobs
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
        private static string apsimReport = @"C:\Users\hol353\ApsimReport\ApsimReport.exe";

        /// <summary>The apsim report executable path.</summary>
        private static string archiveLocation = @"ftp://www.apsim.info/YP/Archive";
                
        /// <summary>Gets or sets the working directory.</summary>
        private string workingDirectory;

        /// <summary>The now date for generating reports.</summary>
        private DateTime nowDate;

        /// <summary>Initializes a new instance of the <see cref="APSIMJob"/> class.</summary>
        /// <param name="apsimFileName">Name of the apsim file.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public YPPostSimulationJob(DateTime nowDate, string workingDirectory)
        {
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
            Specification.YieldProphetSpec yieldProphet = YieldProphetServices.Create(reader.ReadToEnd());
            reader.Close();

            // copy in the report file.
            string reportFileName = Path.Combine(workingDirectory, yieldProphet.ReportType + ".report");
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Services.Resources." + yieldProphet.ReportType + ".report");
            XmlDocument doc = new XmlDocument(); 
            doc.Load(s);
            doc.Save(reportFileName);

            // run ApsimReport to generate .GIF files and a .PDF
            string archiveBaseFileName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;

            Process process = Process.Start(apsimReport, Utility.String.DQuote(reportFileName) + " " +
                                            Utility.String.DQuote(archiveBaseFileName + ".gif"));
            process.WaitForExit();
            process = Process.Start(apsimReport, Utility.String.DQuote(reportFileName) + " " +
                                    Utility.String.DQuote(archiveBaseFileName + ".pdf"));
            process.WaitForExit();

            // Zip the temporary directory and send to archive.
            string zipFileName = Path.Combine(workingDirectory, archiveBaseFileName + ".zip");
            Utility.Zip.ZipFiles(Directory.GetFiles(workingDirectory), zipFileName, null);
            Utility.FTPClient.Upload(zipFileName, archiveLocation + "/" + archiveBaseFileName + ".zip", "Administrator", "CsiroDMZ!");

            // Get rid of our temporary directory.
            Directory.Delete(workingDirectory, true);
        }

        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="yieldProphet">The yield prophet spec.</param>
        /// <param name="endDate">The end date for using any observed data.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <returns>The name of the created .apsim file.</returns>
        private static List<Specification.APSIMSpec> ToAPSIM(Specification.YieldProphetSpec yieldProphet, string workingFolder)
        {
            if (yieldProphet.ReportType == Specification.YieldProphetSpec.ReportTypeEnum.Crop)
                return CropReport(yieldProphet, workingFolder);
            else if (yieldProphet.ReportType == Specification.YieldProphetSpec.ReportTypeEnum.SowingOpportunity)
                return SowingOpportunityReport(yieldProphet, workingFolder);

            return null;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static Specification.APSIMSpec CreateBaseSimulation(Specification.Paddock paddock, string workingFolder)
        {
            Specification.Paddock copyOfPaddock = Utility.Xml.Clone(paddock) as Specification.Paddock;
            copyOfPaddock.ObservedData = paddock.ObservedData;

            Specification.APSIMSpec shortSimulation = new Specification.APSIMSpec();
            shortSimulation.Name = "Base";

            shortSimulation.StartDate = new DateTime(copyOfPaddock.StartSeasonDate.Year, 4, 1);
            shortSimulation.EndDate = copyOfPaddock.NowDate;
            shortSimulation.NowDate = copyOfPaddock.NowDate;
            if (shortSimulation.NowDate == DateTime.MinValue)
                shortSimulation.NowDate = DateTime.Now;
            shortSimulation.DailyOutput = true;
            shortSimulation.ObservedData = GetObservedDataForPaddock(copyOfPaddock, workingFolder);
            shortSimulation.Soil = copyOfPaddock.Soil;
            shortSimulation.SoilPath = copyOfPaddock.SoilPath;
            shortSimulation.Samples = copyOfPaddock.Samples;
            shortSimulation.StationNumber = copyOfPaddock.StationNumber;
            shortSimulation.StubbleMass = copyOfPaddock.StubbleMass;
            shortSimulation.StubbleType = copyOfPaddock.StubbleType;

            shortSimulation.Management = copyOfPaddock.Management;
            AddResetDatesToManagement(copyOfPaddock, shortSimulation);
            return shortSimulation;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static List<Specification.APSIMSpec> CropReport(Specification.YieldProphetSpec yieldProphet, string workingFolder)
        {
            List<Specification.APSIMSpec> simulations = new List<Specification.APSIMSpec>();
            Specification.Paddock paddock = yieldProphet.PaddockList[0];

            Specification.APSIMSpec thisYear = CreateBaseSimulation(paddock, workingFolder);
            thisYear.Name = "ThisYear";
            thisYear.WriteDepthFile = true;
            simulations.Add(thisYear);

            Specification.APSIMSpec seasonSimulation = CreateBaseSimulation(paddock, workingFolder);
            seasonSimulation.Name = "Base";
            seasonSimulation.DailyOutput = false;
            seasonSimulation.YearlyOutput = true;
            seasonSimulation.EndDate = seasonSimulation.StartDate.AddDays(300);
            simulations.Add(seasonSimulation);

            Specification.APSIMSpec NUnlimitedSimulation = CreateBaseSimulation(paddock, workingFolder);
            NUnlimitedSimulation.Name = "NUnlimited";
            NUnlimitedSimulation.DailyOutput = false;
            NUnlimitedSimulation.YearlyOutput = true;
            NUnlimitedSimulation.EndDate = NUnlimitedSimulation.StartDate.AddDays(300);
            NUnlimitedSimulation.NUnlimited = true;
            simulations.Add(NUnlimitedSimulation);

            Specification.APSIMSpec NUnlimitedFromTodaySimulation = CreateBaseSimulation(paddock, workingFolder);
            NUnlimitedFromTodaySimulation.Name = "NUnlimitedFromToday";
            NUnlimitedFromTodaySimulation.DailyOutput = false;
            NUnlimitedFromTodaySimulation.YearlyOutput = true;
            NUnlimitedFromTodaySimulation.EndDate = NUnlimitedFromTodaySimulation.StartDate.AddDays(300);
            NUnlimitedFromTodaySimulation.NUnlimitedFromToday = true;
            simulations.Add(NUnlimitedFromTodaySimulation);

            Specification.APSIMSpec Next10DaysDry = CreateBaseSimulation(paddock, workingFolder);
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
        private static List<Specification.APSIMSpec> SowingOpportunityReport(Specification.YieldProphetSpec yieldProphet, string workingFolder)
        {
            List<Specification.APSIMSpec> simulations = new List<Specification.APSIMSpec>();
            Specification.Paddock paddock = yieldProphet.PaddockList[0];

            DateTime sowingDate = new DateTime(paddock.StartSeasonDate.Year, 3, 15);
            DateTime lastSowingDate = new DateTime(paddock.StartSeasonDate.Year, 7, 5);
            while (sowingDate <= lastSowingDate)
            {
                Specification.APSIMSpec sim = CreateBaseSimulation(paddock, workingFolder);
                sim.Name = sowingDate.ToString("ddMMM");
                sim.DailyOutput = false;
                sim.YearlyOutput = true;
                sim.WriteDepthFile = false;
                sim.StartDate = sowingDate;
                sim.EndDate = sim.StartDate.AddDays(300);

                Specification.Sow simSowing = Specification.Utils.GetCropBeingSown(sim.Management);
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
        private static void AddResetDatesToManagement(Specification.Paddock paddock, Specification.APSIMSpec simulation)
        {
            // Reset
            if (paddock.SoilWaterSampleDate == DateTime.MinValue)
                throw new Exception("Cannot find soil water reset date");

            if (paddock.SoilNitrogenSampleDate == DateTime.MinValue)
                paddock.SoilNitrogenSampleDate = paddock.SoilWaterSampleDate;

            Specification.Sow sowing = Specification.Utils.GetCropBeingSown(paddock.Management);
            if (sowing != null && sowing.Date != DateTime.MinValue)
            {
                // reset at sowing if the sample dates are after sowing.
                if (paddock.SoilWaterSampleDate > sowing.Date)
                {
                    simulation.Management.Add(new Specification.ResetWater() { Date = sowing.Date });
                    simulation.Management.Add(new Specification.ResetSurfaceOrganicMatter() { Date = sowing.Date });
                }
                if (paddock.SoilNitrogenSampleDate > sowing.Date)
                    simulation.Management.Add(new Specification.ResetNitrogen() { Date = sowing.Date });

                // reset on the sample dates.
                simulation.Management.Add(new Specification.ResetWater() { Date = paddock.SoilWaterSampleDate });
                simulation.Management.Add(new Specification.ResetSurfaceOrganicMatter() { Date = paddock.SoilWaterSampleDate });
                simulation.Management.Add(new Specification.ResetNitrogen() { Date = paddock.SoilNitrogenSampleDate });
            }
        }

        /// <summary>Gets the observed data for paddock.</summary>
        /// <param name="workingFolder">The working folder.</param>
        /// <param name="simulation">The paddock.</param>
        /// <param name="observedData">The observed data.</param>
        /// <returns>The data table</returns>
        private static DataTable GetObservedDataForPaddock(Specification.Paddock paddock, string workingFolder)
        {
            if (paddock.ObservedData != null)
                return paddock.ObservedData;

            else if (paddock.RainfallFilename != null && paddock.RainfallFilename != string.Empty)
            {
                // Read in the observed data from file
                string rainfallFileName = Path.Combine(workingFolder, paddock.RainfallFilename);
                Utility.ApsimTextFile observedDataFile = new Utility.ApsimTextFile();
                observedDataFile.Open(rainfallFileName);
                DataTable observedData = observedDataFile.ToTable();
                observedDataFile.Close();
                return observedData;
            }

            return new DataTable();
        }

    }
}
