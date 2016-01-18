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
    public enum SimulationType { stCropOnly, stSingleFlock, stDualFlock };

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
        /// Cropping region that is used for determining sowing windows
        /// Southern LRZ
        /// Southern MRZ
        /// Southern HRZ
        /// Western LRZ
        /// Western MRZ
        /// Western HRZ
        /// </summary>
        public string CroppingRegion { get; set; }
        
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

        /// <summary>
        /// The livestock description which includes the animal enterprise descriptions.
        /// </summary>
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
        /// The total amount of fertiliser allowed to be applied to a crop (cereals and canola)
        /// in one season on this soil type. Units are kg of N.
        /// </summary>
        public double SeasonFertiliser { get; set; }

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
    /// Crop sown on a soil type 
    /// </summary>
    public struct CropSpec
    {
        /// <summary>
        /// Name of the crop
        /// </summary>
        public string Name;

        /// <summary>
        /// Optional.
        /// Maximum rooting depth allowed for this crop (on it's soil type) in mm.
        /// If it is not specified then plants will use the profile specified.
        /// </summary>
        public double MaxRootDepth;

        /// <summary>
        /// Constructor for a crop type item
        /// </summary>
        /// <param name="Name">Name of the crop</param>
        /// <param name="maxRtDepth">Rooting depth allowed in mm. Use a value lteq 0 for not set.</param>
        public CropSpec(string name, double maxRtDepth = 0)
        {
            Name = name.ToLower();
            MaxRootDepth = maxRtDepth;
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
    
    /// <summary>
    /// The livestock enterprises in the farming system. Includes the breeding
    /// flocks and the supplements fed.
    /// </summary>
    public class FarmLivestock
    {
        /// <summary>
        /// The breeding flocks in this livestock system
        /// </summary>
        [XmlElement("Flocks")]
        public List<FlockDescr> Flocks { get; set; }
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
        /// Day of the year for shearing for all sheep
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
            Flocks = new List<FlockDescr>();
        }        
    }

    public class BreedParameters
    {
        /// <summary>
        /// Set this to false to avoid these values overwriting any existing params
        /// </summary>
        public Boolean UseParams { get; set; }
        /// <summary>
        /// Standard reference weight. Weigh of mature animal in average condition score
        /// without fleece or conceptus. kg
        /// </summary>
        public double SRW { get; set; }
        /// <summary>
        /// Weaner mortality %/year
        /// </summary>
        public double WeanerMortality { get; set; }
        /// <summary>
        /// Fleece yield %
        /// </summary>
        public double FleeceYield { get; set; }
        /// <summary>
        /// Maximum average wool fibre diameter microns
        /// </summary>
        public double MaxFibre { get; set; }
        /// <summary>
        /// Breed reference fleece weight, kg.
        /// </summary>
        public double PotFleece { get; set; }
        /// <summary>
        /// Conception percentage of singles when ewes are at CS 3
        /// </summary>
        public double ConceptSingle { get; set; }
        /// <summary>
        /// Conception percentage of twins when ewes are at CS 3
        /// </summary>
        public double ConceptTwin { get; set; }
        public BreedParameters()
        {
            UseParams = true;
        }
    }

    /// <summary>
    /// The breeding livestock flock description.
    /// </summary>
    public class FlockDescr
    {
        /// <summary>
        /// The breed parameters to use for this flock. In F4P the use of this
        /// varies between flocks. Flock1 = dam params, Flock2 = sire params.
        /// To avoid changing the values already set for the breed set 
        /// </summary>
        public BreedParameters BreedParams;
        /// <summary>
        /// True if this is a self replacing flock
        /// </summary>
        public bool SelfReplacing { get; set; }
        /// <summary>
        /// The breed name. e.g. "Small merino". If this value is empty and this is the second flock
        /// then the offspring breed from the previous flock will be used.
        /// </summary>
        public string Dam { get; set; }
        /// <summary>
        /// The breed of the ram used. Set it to the same as the ewe breed for self replacing flocks.
        /// </summary>
        public string Sire { get; set; }
        /// <summary>
        /// Target sale weight for offspring, kg
        /// </summary>
        public double LambSaleWt { get; set; }
        /// <summary>
        /// Age in years to cast for age the merino ewes
        /// </summary>
        public double CastForAgeYears { get; set; }
        /// <summary>
        /// Number of breeding ewes in this flock
        /// </summary>
        public int BreedingEweCount { get; set; }
        /// <summary>
        /// The day of the year for ewe joining, 1-Feb 
        /// </summary>
        public string EweJoinDay { get; set; }
        public FlockDescr()
        {
            BreedParams = new BreedParameters();
            SelfReplacing = false;
        }
    }
}

