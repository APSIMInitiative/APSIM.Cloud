// -----------------------------------------------------------------------
// <copyright file="YieldProphet.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Serialization;
    using System.Data;
    using System.IO;
    using System.Runtime.Serialization;

    /// <summary>
    /// A specification for a Yield Prophet job
    /// </summary>
    public class YieldProphet
    {
        /// <summary>The version of the specification</summary>
        public int Version = 9;

        /// <summary>Gets or sets the list of paddocks</summary>
        [XmlElement("Paddock")]
        public List<Paddock> Paddock { get; set; }

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

        /// <summary>Gets or sets the observed data. Can be null if no data.</summary>
        public DataTable ObservedData { get; set; }

        /// <summary>Gets or sets the rainfall source name that appears on Crop Report</summary>
        public string RainfallSource { get; set; }

        /// <summary>Gets or sets the temperature source name that appears on Crop Report</summary>
        public string TemperatureSource { get; set; }

        /// <summary>Gets or sets the stubble mass (kg/ha).</summary>
        public double StubbleMass { get; set; }

        /// <summary>Gets or sets the type of the stubble.</summary>
        public string StubbleType { get; set; }

        /// <summary>Gets or sets the slope (%).</summary>
        public double Slope { get; set; }

        /// <summary>Gets or sets the slope length (m).</summary>
        public double SlopeLength { get; set; }

        /// <summary>Gets or sets a value indicating whether EC and CL should be used.</summary>
        public bool UseEC { get; set; }

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
        public List<APSIM.Shared.Soils.Sample> Samples { get; set; }

        /// <summary>Gets or sets the full APSoil path of the soil to use to lookup APSoil.</summary>
        public string SoilPath { get; set; }

        /// <summary>Gets or sets the soil. If null, the 'SoilName' property will
        /// be used to lookup the soil from APSoil</summary>
        public APSIM.Shared.Soils.Soil Soil { get; set; }

        /// <summary>Initializes a new instance of the <see cref="Paddock"/> class.</summary>
        public Paddock()
        {
            Management = new List<Management>();
            Samples = new List<APSIM.Shared.Soils.Sample>();
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
    [DataContract]
    [KnownType(typeof(Sow))]
    [KnownType(typeof(Fertilise))]
    [KnownType(typeof(Irrigate))]
    [KnownType(typeof(Tillage))]
    [KnownType(typeof(StubbleRemoved))]
    [KnownType(typeof(ResetWater))]
    [KnownType(typeof(ResetNitrogen))]
    [KnownType(typeof(ResetSurfaceOrganicMatter))]
    public class Management
    {
        /// <summary>Gets or sets the date.</summary>
        [DataMember]
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// A sowing management.
    /// </summary>
    [DataContract]
    public class Sow : Management
    {
        [DataMember]
        public int MaxRootDepth { get; set; }   // mm
        [DataMember]
        public string Crop { get; set; }
        [DataMember]
        public string Cultivar { get; set; }
        [DataMember]
        public int SowingDensity { get; set; }
        [DataMember]
        public DateTime EmergenceDate { get; set; }
        [DataMember]
        public int RowSpacing { get; set; }
        [DataMember]
        public double BedRowSpacing { get; set; }
        [DataMember]
        public int BedWidth { get; set; }
        [DataMember]
        public string SkipRow { get; set; }

        /// <summary>Gets or sets the amount of irrigation applied at sowing (mm).</summary>
        [DataMember]
        public double IrrigationAmount { get; set; }
    }

    [DataContract]
    public class Fertilise : Management
    {
        [DataMember]
        public double Amount { get; set; }
        [DataMember]
        public bool Scenario { get; set; }
    }

    [DataContract]
    public class Irrigate : Management
    {
        [DataMember]
        public double Amount { get; set; }
        [DataMember]
        public double Efficiency { get; set; }
        [DataMember]
        public bool Scenario { get; set; }
    }

    [DataContract]
    public class Tillage : Management
    {
        public enum DisturbanceEnum { Low, Medium, High }

        [DataMember]
        public DisturbanceEnum Disturbance { get; set; }
        [DataMember]
        public bool Scenario { get; set; }
    }

    [DataContract]
    public class StubbleRemoved : Management
    {
        [DataMember]
        public double Percent { get; set; }  // percent
    }

    [DataContract]
    public class ResetWater : Management
    {
    }

    [DataContract]
    public class ResetNitrogen : Management
    {

    }

    [DataContract]
    public class ResetSurfaceOrganicMatter : Management
    {

    }


}