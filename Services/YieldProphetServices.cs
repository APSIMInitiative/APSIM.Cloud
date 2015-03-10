// -----------------------------------------------------------------------
// <copyright file="YieldProphetServices.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Serialization;
    using System.IO;
    using ApsimFile;
    using System.Data;
    using System.Reflection;
    using System.Diagnostics;

    /// <summary>
    /// YieldProphet specific functions.
    /// </summary>
    public class YieldProphetServices
    {
        private static string ApsimReport = @"C:\Users\hol353\Work\ApsimReport\ApsimReport.exe";

        /// <summary>Factory method for creating a YieldProphet object.</summary>
        /// <param name="xml">The XML to use to create the object</param>
        /// <returns>The newly created object.</returns>
        public static Specification.YieldProphet Create(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            if (Utility.Xml.Value(doc.DocumentElement, "Version") == "")
                doc.LoadXml(YieldProphetOld.Convert(xml));
            
            XmlReader reader = new XmlNodeReader(doc.DocumentElement);
            reader.Read();
            XmlSerializer serial = new XmlSerializer(typeof(Specification.YieldProphet));
            return (Specification.YieldProphet)serial.Deserialize(reader);
        }

        /// <summary>Convert the YieldProphet spec to XML.</summary>
        /// <returns>The XML string.</returns>
        public static string ToXML(Specification.YieldProphet yieldProphet)
        {
            XmlSerializer serial = new XmlSerializer(typeof(Specification.YieldProphet));
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            StringWriter writer = new StringWriter();
            serial.Serialize(writer, yieldProphet, ns);
            string xml = writer.ToString();
            if (xml.Length > 5 && xml.Substring(0, 5) == "<?xml")
            {
                // remove the first line: <?xml version="1.0"?>/n
                int posEol = xml.IndexOf("\n");
                if (posEol != -1)
                    return xml.Substring(posEol + 1);
            }
            return xml;
        }

        /// <summary>Runs a yield prophet job</summary>
        /// <param name="yieldProphetXML">The yield prophet XML.</param>
        /// <param name="observedData">Observed data. Can be null.</param>
        /// <param name="nowDate">The now date.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public static void Run(string yieldProphetXML, DataTable observedData, DateTime nowDate, string workingDirectory)
        {
            // Create a YieldProphet object from our YP xml file
            Specification.YieldProphet spec = YieldProphetServices.Create(yieldProphetXML);

            // Specify the now date.
            spec.PaddockList[0].NowDate = nowDate;

            // Specify the observed data.
            spec.PaddockList[0].ObservedData = observedData;

            // Convert YieldProphet spec into a simulation set.
            List<APSIM.Cloud.Services.Specification.APSIM> simulations = YieldProphetServices.ToAPSIM(spec, workingDirectory);

            // Make sure the simulation runs.
            APSIMRun.Locally(simulations, workingDirectory);

            // Perform all post simulation tasks.
            PostSimulation(spec, nowDate, workingDirectory);

        }

        /// <summary>Perform all post simulation actions</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingDirectory">The working directory.</param>
        private static void PostSimulation(Specification.YieldProphet yieldProphet, DateTime nowDate, string workingDirectory)
        {
            // Create an XML file for the YieldProphet spec so that ApsimReport can find it.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(ToXML(yieldProphet));
            doc.Save(Path.Combine(workingDirectory, "YieldProphet.xml"));

            // copy in the report file.
            string reportFileName = Path.Combine(workingDirectory, yieldProphet.ReportType + ".report");
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Services.Resources." + yieldProphet.ReportType + ".report");
            doc.Load(s);
            doc.Save(reportFileName);

            // run ApsimReport to generate .GIF files and a .PDF
            string baseGIFFileName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt)");
            Process process = Process.Start(ApsimReport, Utility.String.DQuote(reportFileName) + " " +
                                            Utility.String.DQuote(baseGIFFileName + ".gif"));
            process.WaitForExit();
            process = Process.Start(ApsimReport, Utility.String.DQuote(reportFileName) + " " +
                                    Utility.String.DQuote(baseGIFFileName + ".pdf"));
            process.WaitForExit();
        }


        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="yieldProphet">The yield prophet spec.</param>
        /// <param name="endDate">The end date for using any observed data.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <returns>The name of the created .apsim file.</returns>
        public static List<Specification.APSIM> ToAPSIM(Specification.YieldProphet yieldProphet, string workingFolder)
        {
            if (yieldProphet.ReportType == Specification.YieldProphet.ReportTypeEnum.Crop)
                return CropReport(yieldProphet, workingFolder);
            else if (yieldProphet.ReportType == Specification.YieldProphet.ReportTypeEnum.SowingOpportunity)
                return SowingOpportunityReport(yieldProphet, workingFolder);

            return null;
        }

        /// <summary>Convert the yieldProphet specification into a series of APSIM simulation specifications.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <param name="workingFolder">The working folder.</param>
        private static Specification.APSIM CreateBaseSimulation(Specification.Paddock paddock, string workingFolder)
        {
            Specification.Paddock copyOfPaddock = Utility.Xml.Clone(paddock) as Specification.Paddock;
            copyOfPaddock.ObservedData = paddock.ObservedData;

            Specification.APSIM shortSimulation = new Specification.APSIM();
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
        private static List<Specification.APSIM> CropReport(Specification.YieldProphet yieldProphet, string workingFolder)
        {
            List<Specification.APSIM> simulations = new List<Specification.APSIM>();
            Specification.Paddock paddock = yieldProphet.PaddockList[0];

            Specification.APSIM thisYear = CreateBaseSimulation(paddock, workingFolder);
            thisYear.Name = "ThisYear";
            thisYear.WriteDepthFile = true;
            simulations.Add(thisYear);

            Specification.APSIM seasonSimulation = CreateBaseSimulation(paddock, workingFolder);
            seasonSimulation.Name = "Base";
            seasonSimulation.DailyOutput = false;
            seasonSimulation.YearlyOutput = true;
            seasonSimulation.EndDate = seasonSimulation.StartDate.AddDays(300);
            simulations.Add(seasonSimulation);

            Specification.APSIM NUnlimitedSimulation = CreateBaseSimulation(paddock, workingFolder);
            NUnlimitedSimulation.Name = "NUnlimited";
            NUnlimitedSimulation.DailyOutput = false;
            NUnlimitedSimulation.YearlyOutput = true;
            NUnlimitedSimulation.EndDate = NUnlimitedSimulation.StartDate.AddDays(300);
            NUnlimitedSimulation.NUnlimited = true;
            simulations.Add(NUnlimitedSimulation);

            Specification.APSIM NUnlimitedFromTodaySimulation = CreateBaseSimulation(paddock, workingFolder);
            NUnlimitedFromTodaySimulation.Name = "NUnlimitedFromToday";
            NUnlimitedFromTodaySimulation.DailyOutput = false;
            NUnlimitedFromTodaySimulation.YearlyOutput = true;
            NUnlimitedFromTodaySimulation.EndDate = NUnlimitedFromTodaySimulation.StartDate.AddDays(300);
            NUnlimitedFromTodaySimulation.NUnlimitedFromToday = true;
            simulations.Add(NUnlimitedFromTodaySimulation);

            Specification.APSIM Next10DaysDry = CreateBaseSimulation(paddock, workingFolder);
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
        private static List<Specification.APSIM> SowingOpportunityReport(Specification.YieldProphet yieldProphet, string workingFolder)
        {
            List<Specification.APSIM> simulations = new List<Specification.APSIM>();
            Specification.Paddock paddock = yieldProphet.PaddockList[0];

            DateTime sowingDate = new DateTime(paddock.StartSeasonDate.Year, 3, 15);
            DateTime lastSowingDate = new DateTime(paddock.StartSeasonDate.Year, 7, 5);
            while (sowingDate <= lastSowingDate)
            {
                Specification.APSIM sim = CreateBaseSimulation(paddock, workingFolder);
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
        private static void AddResetDatesToManagement(Specification.Paddock paddock, Specification.APSIM simulation)
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

        #region Calculated fields

        /// <summary>Fills the auto-calculated fields.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="observedData">The observed data.</param>
        /// <param name="weatherData">The weather data.</param>
        private static void FillInCalculatedFields(Specification.Paddock paddock, DataTable observedData, WeatherFile weatherData, string workingFolder)
        {
            paddock.LastClimateDate = weatherData.LastSILODateFound;

            IEnumerable<Specification.Tillage> tillages = paddock.Management.OfType<Specification.Tillage>();
            if (tillages.Count() > 0)
                paddock.StubbleIncorporatedPercent = Specification.Utils.CalculateAverageTillagePercent(tillages);

            DateTime lastRainfallDate = GetLastRainfallDate(observedData);
            if (lastRainfallDate != DateTime.MinValue)
                paddock.DateOfLastRainfallEntry = lastRainfallDate.ToString("dd/MM/yyyy");

            if (weatherData.FilesCreated.Count() > 0)
            {
                string firstMetFile = Path.Combine(workingFolder, weatherData.FilesCreated[0]);
                Utility.ApsimTextFile textFile = new Utility.ApsimTextFile();
                textFile.Open(firstMetFile);
                DataTable data = textFile.ToTable();
                textFile.Close();
                paddock.RainfallSinceSoilWaterSampleDate = SumTableAfterDate(data, "Rain", paddock.SoilWaterSampleDate);
            }
        }

        /// <summary>Gets the last rainfall date in the observed data.</summary>
        /// <param name="observedData">The observed data.</param>
        /// <returns>The date of the last rainfall row or DateTime.MinValue if no data.</returns>
        private static DateTime GetLastRainfallDate(DataTable observedData)
        {
            if (observedData.Rows.Count == 0)
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
