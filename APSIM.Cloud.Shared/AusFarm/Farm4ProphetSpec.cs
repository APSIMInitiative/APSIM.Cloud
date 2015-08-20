// -----------------------------------------------------------------------
// <copyright file="Farm4Prophet.cs" company="APSIM Initiative">
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
    ///  A specification for a Farm4Prophet job
    /// </summary>
    public class Farm4Prophet
    {
        /// <summary>The version of the specification</summary>
        public int Version = 1412;

        /// <summary>
        /// This will be the job name in the web service database. Required.
        /// </summary>
        public string TaskName { get; set; }

        /// <summary>Gets or sets the list of Farm systems</summary>
        [XmlElement("FarmList")]
        public List<FarmSystem> FarmList { get; set; }

        /// <summary>Gets or sets the name of the client.</summary>
        public string ClientName { get; set; }

        /// <summary>Gets or sets the return location for results</summary>
        public string ReturnResults { get; set; }

        /// <summary>Gets or sets the name of the person generating the report.</summary>
        public string ReportGeneratedBy { get; set; }

        public Farm4Prophet()
        {
            FarmList = new List<FarmSystem>();
        }
    }

    /// <summary>
    /// Range of valid simulation types that can be configured
    /// using a variety of template sdml files.
    /// </summary>
    public enum SimulationType { stCropOnly, stMixed };

    /// <summary>
    /// A specification for a AusFarm simulated Farm System
    /// </summary>
    public class FarmSystem
    {
        /// <summary>
        /// The type of simulation that is required for this test run. 
        /// This setting determines the template to use for the sdml script.
        /// </summary>
        public SimulationType SimTemplateType;

        /// <summary>
        /// Gets or sets the name of the Farm
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the start of season
        /// </summary>
        public DateTime StartSeasonDate { get; set; }

        /// <summary>
        /// The simulation run length in months. Gets checked against the
        /// last date of weather file.
        /// </summary>
        public int RunLength { get; set; }

        /// <summary>Gets or sets the station number to use from SILO</summary>
        public int StationNumber { get; set; }

        /// <summary>Gets or sets the name of the station that appears on Crop Report</summary>
        public string StationName { get; set; }

        /// <summary>
        /// Gets or sets the name of the report. This is used
        /// to seperate the outputs for each simulation run.
        /// They will be named : 
        ///     reportName + "_daily.txt"
        ///     reportName + "_monthly.txt"
        ///     reportName + "_yearly.txt"
        ///     reportName + "_animal.txt"
        /// </summary>
        public string ReportName { get; set; }

        /// <summary>
        /// Total farm area in ha
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// Gets or sets the soil type information for each farm soil type
        /// </summary>
        public List<FarmSoilType> OnFarmSoilTypes { get; set; }

        /// <summary>
        /// The paddocks may need to have specific initialisation especially
        /// where initial nitrogen and stubble masses are required.
        /// Each paddock will have a soil type.
        /// </summary>
        public List<FarmPaddockType> OnFarmPaddocks { get; set; }
        public FarmLivestock LiveStock { get; set; }

        /// <summary>Initializes a new instance of the <see cref="FarmSystem"/> class.</summary>
        public FarmSystem()
        {
            OnFarmSoilTypes = new List<FarmSoilType>();
            OnFarmPaddocks = new List<FarmPaddockType>();
            LiveStock = new FarmLivestock();
        }
    }

    /// <summary>
    /// Each soil type found on the simulated farm.
    /// The soil type is applied to a paddock. 
    /// </summary>
    public class FarmSoilType
    {
        /// <summary>
        /// Gets or sets the proportion (0-1) of farm area used by this soil type.
        /// If this value is zero then this soil type will not have any crops sown on it.
        /// </summary>
        public double AreaProportion { get; set; }    

        /// <summary>
        /// Gets or sets the soil name/ ApSoil path 
        /// </summary>
        public string SoilPath { get; set; }

        /// <summary>Gets or sets the slope (%).</summary>
        public double Slope { get; set; }

        /// <summary>Gets or sets the slope length (m).</summary>
        public double SlopeLength { get; set; }

        /// <summary>
        /// Gets or sets the list of crops that are sown on this soil type in rotation
        /// </summary>
        [XmlElement("CropRotationList")]
        public List<CropSpec> CropRotationList { get; set; }

        /// <summary>
        /// Gets or sets a soil detailed description 
        /// </summary>
        public APSIM.Shared.Soils.Soil SoilDescr { get; set; }

        public FarmSoilType()
        {
            CropRotationList = new List<CropSpec>();
        }
    }

    /// <summary>
    /// Crop details
    /// </summary>
    public struct CropSpec
    {
        /// <summary>
        /// Name of the crop
        /// </summary>
        public string name; 
        /// <summary>
        /// This could be a pasture or a crop
        /// </summary>
        public bool isCrop;
        public CropSpec(string Name, bool iscrop)
        {
            name = Name;
            isCrop = iscrop;
        }
    }

    /// <summary>
    /// Defines a paddock area on in a Farm system.
    /// Each paddock can have initial soil sample.
    /// </summary>
    public class FarmPaddockType
    {
        protected FarmSoilType FSoilType = null;

        /// <summary>
        /// Area in ha. Can be zero.
        /// </summary>
        public double Area { get; set; }    

        /// <summary>
        /// Gets or sets the soil type for this paddock
        /// </summary>
        public FarmSoilType SoilType { get { return FSoilType; } set { FSoilType = value; } }

        /// <summary>Gets or sets the stubble mass (kg/ha).</summary>
        public double StubbleMass { get; set; }

        /// <summary>Gets or sets the type of the stubble.</summary>
        public string StubbleType { get; set; }

        /// <summary>
        /// Gets or sets the sampling date
        /// </summary>
        public DateTime SoilWaterSampleDate { get; set; }

        /// <summary>
        /// Gets or sets the sampling date
        /// </summary>
        public DateTime SoilNitrogenSampleDate { get; set; }

        /// <summary>Gets or sets the soil sample</summary>
        public APSIM.Shared.Soils.Sample Sample { get; set; }

        /// <summary>
        /// Paddocks are constructed with a soil type
        /// </summary>
        /// <param name="aSoil"></param>
        public FarmPaddockType(FarmSoilType aSoil)
        {
            FSoilType = aSoil;
        }

        /// <summary>
        /// Hide default constructor
        /// </summary>
        internal FarmPaddockType()
        {
        }
    }
    
    public class FarmLivestock
    {
        /// <summary>
        /// Breed name for the trade lambs
        /// Valid names include: merino
        /// </summary>
        public string TradeLambBreed { get; set; }
        /// <summary>
        /// Count of lambs to purchase. If set to zero then the
        /// trade lamb enterprise is disabled
        /// </summary>
        public int TradeLambCount { get; set; }
        /// <summary>
        /// Purchase day of the year for trade lambs
        /// e.g. 1-Nov
        /// </summary>
        public string TradeLambBuyDay { get; set; }
        /// <summary>
        /// Weight target for the trade lamb sales
        /// </summary>
        public double TradeLambSaleWt { get; set; }
        /// <summary>
        /// Number of breeding ewes in the enterprise
        /// </summary>
        public int BreedingEweCount { get; set; }
        /// <summary>
        /// The day of the year for ewe joining 
        /// </summary>
        public string EweJoinDay { get; set; }
        /// <summary>
        /// Lambing proportion. 
        /// </summary>
        public double LambingRate { get; set; }
        /// <summary>
        /// Age in years to cast for age the merino ewes
        /// </summary>
        public double CastForAgeYears { get; set; }
        /// <summary>
        /// Target sale weight for offspring
        /// </summary>
        public double LambSaleWt { get; set; }
        /// <summary>
        /// Day of the year for shearing
        /// </summary>
        public string ShearingDay { get; set; }
        /// <summary>
        /// Name of the supplement
        /// </summary>
        public string Supplement1 { get; set; }
        public string Supplement2 { get; set; }
        /// <summary>
        /// Proportion of the supplement mix for
        /// supplement 1. 0-1
        /// </summary>
        public double Supp1Propn { get; set; }
        /// <summary>
        /// Proportion of the supplement mix for
        /// supplement 2. 0-1
        /// </summary>
        public double Supp2Propn { get; set; }

        public FarmLivestock()
        {
        }
    }
}

