/// -----------------------------------------------------------------------
// <copyright file="Farm4ProphetToAusFarm.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Convert a Farm4Prophet class to an AusFarm simulation
    /// </summary>
    public class Farm4ProphetToAusFarm
    {
        /// <summary>Converts a Farm4Prophet specification to a list of AusFarm simulations.</summary>
        /// <param name="f4Prophet">The farm 4 prophet spec.</param>
        /// <returns>The list of AusFarm simulations that will need to be run.</returns>
        public static List<AusFarmSpec> ToAusFarm(Farm4Prophet f4Prophet)
        {
                return SimulationRuns(f4Prophet);
        }

        /// <summary>
        /// Create simulation specifications.
        /// </summary>
        /// <param name="f4Prophet">The farm 4 prophet specification</param>
        /// <returns>A list of AusFarm specs. </returns>
        private static List<AusFarmSpec> SimulationRuns(Farm4Prophet f4Prophet)
        {
            List<AusFarmSpec> ausfarmSpecs = new List<AusFarmSpec>();

            foreach (FarmSystem farm in f4Prophet.FarmList)
            {
                AusFarmSpec simulation = CreateBaseSimulation(farm);
                simulation.Name = farm.Name;
                simulation.Area = farm.Area;
                simulation.StartDate = farm.StartSeasonDate;
                simulation.EndDate = simulation.StartDate.AddMonths(farm.RunLength);
                simulation.ReportName = farm.ReportName;
                ausfarmSpecs.Add(simulation);
            }
            return ausfarmSpecs;
        }

        /// <summary>
        /// Creates a base AusFarm simulation spec from the Farm4Prophet spec.
        /// </summary>
        /// <param name="paddock">The farm paddock</param>
        /// <returns>The created AusFarm spec</returns>
        private static AusFarmSpec CreateBaseSimulation(FarmSystem farm)
        {
            FarmSystem copyOfFarm = farm; 

            AusFarmSpec runnableSim = new AusFarmSpec();
            runnableSim.Name = "BaseSim";

            runnableSim.StartDate = DateTime.MaxValue;

            //===========================================================
            // May be appropriate here to decide which simulation template
            // will be used based on the requirement for crops and animals.
            //===========================================================
            runnableSim.SimTemplateType = farm.SimTemplateType;

            /* sample dates used to initialise the run times?
            if (farm.SoilWaterSampleDate < runnableSim.StartDate)
                runnableSim.StartDate = farm.SoilWaterSampleDate;
            if (farm.SoilNitrogenSampleDate != DateTime.MinValue &&
                farm.SoilNitrogenSampleDate < runnableSim.StartDate)
                runnableSim.StartDate = farm.SoilNitrogenSampleDate; */
            //if (sow != null && sow.Date < shortSimulation.StartDate)
            //    shortSimulation.StartDate = sow.Date;

            if (farm.StartSeasonDate < runnableSim.StartDate)
                runnableSim.StartDate = farm.StartSeasonDate;

            runnableSim.OnFarmSoilTypes.AddRange(copyOfFarm.OnFarmSoilTypes);
            runnableSim.OnFarmPaddocks.AddRange(copyOfFarm.OnFarmPaddocks);
            runnableSim.LiveStock = copyOfFarm.LiveStock;

            runnableSim.StationNumber = copyOfFarm.StationNumber;
            return runnableSim;
        }
    }
}
