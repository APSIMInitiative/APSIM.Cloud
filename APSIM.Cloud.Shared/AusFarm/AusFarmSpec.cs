// -----------------------------------------------------------------------
// <copyright file="AusFarmSpec.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Data;
    using System.Xml.Serialization;

    /// <summary>
    /// A specification for an AusFarm simulation. This object is created from the Farm4Prophet 
    /// object sent to this server.
    /// </summary>
    public class AusFarmSpec
    {
        public AusFarmSpec()
        {
            OnFarmSoilTypes = new List<FarmSoilType>();
            OnFarmPaddocks = new List<FarmPaddockType>();
            LiveStock = new FarmLivestock();
        }

        public SimulationType SimTemplateType;

        /// <summary>Gets or sets the name of the simulation</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the start date of the simulation</summary>
        public DateTime StartDate { get; set; }

        /// <summary>Gets or sets the end date of the simulation</summary>
        public DateTime EndDate { get; set; }

        /// <summary>Gets or sets the SILO station number.</summary>
        public int StationNumber { get; set; }

        /// <summary>The rain deciles calculated from the weather file from a starting month of the year.</summary>
        public double[,] RainDeciles { get; set; }

        /// <summary>Cropping region name</summary>
        public string CroppingRegion { get; set; }

        /// <summary>Gets or sets any observed data. Can be null if no data.</summary>
        public DataTable ObservedData { get; set; }

        public double Area { get; set; }

        public List<FarmPaddockType> OnFarmPaddocks { get; set; }
        public List<FarmSoilType> OnFarmSoilTypes { get; set; }
        public FarmLivestock LiveStock { get; set; }
        
        /// <summary>Gets or sets a value indicating whether the run should be nitrogen unlimited</summary>
        public bool NUnlimited { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the run should be nitrogen 
        /// unlimited from today until the end of the crop.
        /// </summary>
        public bool NUnlimitedFromToday { get; set; }

        /// <summary>Gets or sets the name of the output files.
        /// </summary>
        public string ReportName { get; set; }
    }
}
