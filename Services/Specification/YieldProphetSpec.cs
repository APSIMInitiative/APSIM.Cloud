// -----------------------------------------------------------------------
// <copyright file="YieldProphet.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services.Specification
{
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Serialization;
    using System.Data;

    /// <summary>
    /// A specification for a Yield Prophet job
    /// </summary>
    public class YieldProphetSpec
    {
        /// <summary>The version of the specification</summary>
        public int Version = 9;

        /// <summary>Gets or sets the list of paddocks</summary>
        [XmlElement("Paddock")]
        public Paddock[] PaddockList { get; set; }

        /// <summary>
        /// Report type enumeration
        /// </summary>
        public enum ReportTypeEnum
        {
            /// <summary>No report needed.</summary>
            None,

            /// <summary>User has requested a crop report</summary>
            Crop,

            /// <summary>User has requested a sowing opportunity report</summary>
            SowingOpportunity
        }

        /// <summary>Gets or sets the type of the report the user has requested.</summary>
        public ReportTypeEnum ReportType { get; set; }

        /// <summary>Gets or sets the name of the report.</summary>
        public string ReportName { get; set; }

        /// <summary>Gets or sets the name of the client.</summary>
        public string ClientName { get; set; }

        /// <summary>Gets or sets the name of the person generating the report.</summary>
        public string ReportGeneratedBy { get; set; }
    }

    /// <summary>
    /// A specification for a field in an APSIM simulation
    /// </summary>
    public class Paddock
    {
        /// <value>The name.</value>
        public string Name { get; set; }

        public DateTime StartSeasonDate { get; set; }
        public DateTime SoilWaterSampleDate { get; set; }
        public DateTime SoilNitrogenSampleDate { get; set; }

        /// <summary>Gets or sets the station number to use from SILO</summary>
        public int StationNumber { get; set; }

        /// <summary>Gets or sets the name of the station that appears on Crop Report</summary>
        public string StationName { get; set; }

        /// <summary>Gets or sets the rainfall filename. Can be null if no file present.</summary>
        public string RainfallFilename { get; set; }

        /// <summary>Gets or sets the observed data. Can be null if no data.</summary>
        [XmlIgnore]
        public DataTable ObservedData { get; set; }

        /// <summary>Gets or sets the rainfall source name that appears on Crop Report</summary>
        public string RainfallSource { get; set; }

        /// <summary>Gets or sets the temperature source name that appears on Crop Report</summary>
        public string TemperatureSource { get; set; }
        public double StubbleMass { get; set; }  // kg/ha
        public string StubbleType { get; set; }
        public bool UseProbeRainfall { get; set; }
        public bool UseProbeSoilMoisture { get; set; }
        public bool UseProbeTemperature { get; set; }

        /// <summary>Gets or sets the list of management actions.</summary>
        [XmlArrayItem(typeof(Sow))]
        [XmlArrayItem(typeof(Fertilise))]
        [XmlArrayItem(typeof(Irrigate))]
        [XmlArrayItem(typeof(Tillage))]
        [XmlArrayItem(typeof(StubbleRemoved))]
        [XmlArrayItem(typeof(ResetWater))]
        [XmlArrayItem(typeof(ResetNitrogen))]
        [XmlArrayItem(typeof(ResetSurfaceOrganicMatter))]
        public List<Management> Management { get; set; }

        /// <summary>Gets or sets the soil samples.</summary>
        public List<ApsimFile.Sample> Samples { get; set; }

        /// <summary>Gets or sets the full APSoil path of the soil to use to lookup APSoil.</summary>
        public string SoilPath { get; set; }

        /// <summary>Gets or sets the soil. If null, the 'SoilName' property will
        /// be used to lookup the soil from APSoil</summary>
        public ApsimFile.Soil Soil { get; set; }

        /// <summary>Initializes a new instance of the <see cref="Paddock"/> class.</summary>
        public Paddock()
        {
            Management = new List<Management>();
            Samples = new List<ApsimFile.Sample>();
        }

        /// <summary>
        /// Gets or sets the 'now' date. When creating weather files anything after this
        /// date will assumed to be in the future so historical weather data will be used
        /// after this date.
        /// </summary>
        public DateTime NowDate { get; set; }

        // ****************************************************
        // Auto calculated variables 
        // ****************************************************

        /// <summary>Gets or sets the % stubble incorporated in the top 10cm (AUTOGENERATED).</summary>
        public double StubbleIncorporatedPercent { get; set; }

        /// <summary>Gets or sets the date of last rainfall entry (AUTOGENERATED) (dd/MM/yyyy).</summary>
        public string DateOfLastRainfallEntry { get; set; }

        /// <summary>Gets or sets the total rainfall since 'SoilWaterSampleDate' (AUTOGENERATED) (mm).</summary>
        public double RainfallSinceSoilWaterSampleDate { get; set; }

        /// <summary>Gets or sets the last climate date (AUTOGENERATED).</summary>
        public DateTime LastClimateDate { get; set; }
    }

    /// <summary>
    /// Base class for all paddock management actions.
    /// </summary>
    public class Management
    {
        /// <summary>Gets or sets the date.</summary>
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// A sowing management.
    /// </summary>
    public class Sow : Management
    {
        public int MaxRootDepth { get; set; }   // mm
        public string Crop { get; set; }
        public string Cultivar { get; set; }
        public int SowingDensity { get; set; }
        public DateTime EmergenceDate { get; set; }
        public int RowSpacing { get; set; }
        public double BedRowSpacing { get; set; }
        public int BedWidth { get; set; }
        public string SkipRow { get; set; }

        /// <summary>Gets or sets the amount of irrigation applied at sowing (mm).</summary>
        public double IrrigationAmount { get; set; }
    }

    public class Fertilise : Management
    {
        public double Amount { get; set; }
        public bool Scenario { get; set; }
    }

    public class Irrigate : Management
    {
        public double Amount { get; set; }
        public bool Scenario { get; set; }
    }

    public class Tillage : Management
    {
        public enum DisturbanceEnum { Low, Medium, High }

        public DisturbanceEnum Disturbance { get; set; }
        public bool Scenario { get; set; }
    }

    public class StubbleRemoved : Management
    {
        public double Percent { get; set; }  // percent
    }

    public class ResetWater : Management
    {
    }

    public class ResetNitrogen : Management
    {

    }

    public class ResetSurfaceOrganicMatter : Management
    {

    }


}