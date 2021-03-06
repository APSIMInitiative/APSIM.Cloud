﻿// -----------------------------------------------------------------------
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

        /// <summary>Gets or sets the name of the report.</summary>
        public string ReportName { get; set; }
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

        /// <summary>Type of APSIM run to create.</summary>
        public enum RunTypeEnum
        {
            /// <summary>Run just this season.</summary>
            SingleSeason,

            /// <summary>Run a single longterm simulation - no patching.</summary>
            LongTerm,

            /// <summary>Run a series of longterm simulations, patched with data from this year. Results will be combined.</summary>
            LongTermPatched,

            /// <summary>Run a POAMA simulation, patched with data from this year.</summary>
            POAMA
        }

        /// <summary>Type of run to perform.</summary>
        public RunTypeEnum RunType { get; set; }

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

        /// <summary>
        /// Gets or sets the full APSoil path of the soil to use to lookup APSoil. 
        /// Can also be a URL to the Soil and Landscape Grid.
        /// </summary>
        public string SoilPath { get; set; }

        /// <summary>Gets or sets the soil. If null, the 'SoilName' property will
        /// be used to lookup the soil from APSoil</summary>
        public APSIM.Shared.Soils.Soil Soil { get; set; }

        /// <summary>Gets or sets the total water (mm) at the beginning of the simulation.</summary>
        public double InitTotalWater { get; set; }

        /// <summary>Gets or sets the total nitrogen (kg/ha) at the beginning of the simulation.</summary>
        public double InitTotalNitrogen { get; set; }

        /// <summary>Gets or sets whether the paddock is N unlimited.</summary>
        public bool NUnlimited { get; set; }

        /// <summary>Gets or sets whether the paddock is N unlimited from today.</summary>
        public bool NUnlimitedFromToday { get; set; }

        /// <summary>Gets or sets a value indicating whether daily output is required.</summary>
        public bool DailyOutput { get; set; }

        /// <summary>Gets or sets a value indicating whether monthly output is required.</summary>
        public bool MonthlyOutput { get; set; }

        /// <summary>Gets or sets a value indicating whether yearly output is required.</summary>
        public bool YearlyOutput { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a depth.out file should be created at
        /// the end of the simulation run.
        /// </summary>
        public bool WriteDepthFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a 'next 10 days' output file should be created.
        /// </summary>
        public bool Next10DaysDry { get; set; }

        /// <summary>
        /// Gets or sets the 'now' date. When creating weather files anything after this
        /// date will assumed to be in the future so historical weather data will be used
        /// after this date.
        /// </summary>
        public DateTime NowDate { get; set; }

        /// <summary>Gets or sets the first year for long term simulations</summary>
        public int LongtermStartYear { get; set; }

        /// <summary>Initializes a new instance of the <see cref="Paddock"/> class.</summary>
        public Paddock()
        {
            Management = new List<Management>();
            Samples = new List<APSIM.Shared.Soils.Sample>();
        }
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

        /// <summary>Gets or sets whether the management operation should happen every year of the simulation</summary>
        [DataMember]
        public bool IsEveryYear { get; set; }
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
        [DataMember]
        public int SeedDepth { get; set; }

        /// <summary>Gets or sets the amount of irrigation applied at sowing (mm).</summary>
        [DataMember]
        public double IrrigationAmount { get; set; }
    }

    [DataContract]
    public class Fertilise : Management
    {
        [DataMember]
        public double Amount { get; set; }
    }

    [DataContract]
    public class Irrigate : Management
    {
        [DataMember]
        public double Amount { get; set; }
        [DataMember]
        public double Efficiency { get; set; }
    }

    [DataContract]
    public class Tillage : Management
    {
        public enum DisturbanceEnum { Low, Medium, High }

        [DataMember]
        public DisturbanceEnum Disturbance { get; set; }
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