﻿/// -----------------------------------------------------------------------
// <copyright file="YieldProphetToAPSIM.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Converts a Yield Prophet specification to an APSIM one.
    /// </summary>
    public class YieldProphetToAPSIM
    {
        /// <summary>Converts a Yield Prophet specification to an APSIM one.</summary>
        /// <param name="yieldProphet">The yield prophet spec.</param>
        /// <returns>The list of APSIM simulations that will need to be run.</returns>
        public static List<APSIMSpec> ToAPSIM(YieldProphet yieldProphet)
        {
            if (yieldProphet.ReportType == YieldProphet.ReportTypeEnum.Crop)
                return CropReport(yieldProphet);
            else if (yieldProphet.ReportType == YieldProphet.ReportTypeEnum.SowingOpportunity)
                throw new Exception("Don't use the sowing opportunity report type.");
            else
                return OtherRuns(yieldProphet);
        }

        /// <summary>
        /// Create validation specs.
        /// </summary>
        /// <param name="yieldProphet">The yield prophet specification</param>
        /// <returns>A list of APSIM specs. </returns>
        private static List<APSIMSpec> OtherRuns(YieldProphet yieldProphet)
        {
            List<APSIMSpec> apsimSpecs = new List<APSIMSpec>();

            foreach (Paddock paddock in yieldProphet.Paddock)
            {
                APSIMSpec simulation;
                try
                {
                    simulation = CreateBaseSimulation(paddock);
                    if (paddock.RunType == Paddock.RunTypeEnum.Validation)
                    {
                        simulation.EndDate = simulation.StartDate.AddDays(360);
                        simulation.DailyOutput = false;
                        simulation.YearlyOutput = true;
                    }
                }
                catch (Exception err)
                {
                    simulation = new APSIMSpec();
                    simulation.TypeOfRun = paddock.RunType;
                    simulation.ErrorMessage = err.Message;

                }

                simulation.Name = paddock.Name;
                simulation.WriteDepthFile = false;
                apsimSpecs.Add(simulation);
            }
            return apsimSpecs;
        }

        /// <summary>Creates a base APSIM simulation spec for the Yield Prophet spec.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <returns>The created APSIM simulation spec.</returns>
        private static APSIMSpec CreateBaseSimulation(Paddock paddock)
        {
            Paddock copyOfPaddock = paddock; // XmlUtilities.Clone(paddock) as JobsService.Paddock;
            copyOfPaddock.ObservedData = paddock.ObservedData;

            APSIMSpec shortSimulation = new APSIMSpec();
            shortSimulation.Name = "Base";
            shortSimulation.WeatherFileName = shortSimulation.Name + ".met";

            // Start date of simulation should be the earliest of ResetDate, SowDate and StartSeasonDate
            Sow sow = YieldProphetUtility.GetCropBeingSown(paddock.Management);
            if (sow == null)
                throw new Exception("No sowing specified for paddock: " + paddock.Name);

            if (sow.Date == DateTime.MinValue)
                throw new Exception("No sowing DATE specified for paddock: " + paddock.Name);

            if (sow.Crop == null || sow.Crop == "" || sow.Crop == "None")
                throw new Exception("No sowing CROP specified for paddock: " + paddock.Name);

            shortSimulation.StartDate = DateTime.MaxValue;
            if (paddock.SoilWaterSampleDate != DateTime.MinValue &&
                paddock.SoilWaterSampleDate < shortSimulation.StartDate)
                shortSimulation.StartDate = paddock.SoilWaterSampleDate;
            if (paddock.SoilNitrogenSampleDate != DateTime.MinValue &&
                paddock.SoilNitrogenSampleDate < shortSimulation.StartDate)
                shortSimulation.StartDate = paddock.SoilNitrogenSampleDate;
            if (sow != null && sow.Date < shortSimulation.StartDate && sow.Date != DateTime.MinValue)
                shortSimulation.StartDate = sow.Date;
            if (paddock.StartSeasonDate < shortSimulation.StartDate)
                shortSimulation.StartDate = paddock.StartSeasonDate;

            if (paddock.LongtermStartYear == 0)
                shortSimulation.LongtermStartYear = 1957;
            else
                shortSimulation.LongtermStartYear = paddock.LongtermStartYear;

            if (paddock.RunType == Paddock.RunTypeEnum.SingleSeason)
                shortSimulation.EndDate = copyOfPaddock.NowDate.AddDays(-1);
            else if (paddock.RunType == Paddock.RunTypeEnum.LongTermPatched)
                shortSimulation.EndDate = shortSimulation.StartDate.AddDays(360);
            shortSimulation.NowDate = copyOfPaddock.NowDate.AddDays(-1);
            if (shortSimulation.NowDate == DateTime.MinValue)
                shortSimulation.NowDate = DateTime.Now;
            shortSimulation.DailyOutput = paddock.RunType == Paddock.RunTypeEnum.SingleSeason;
            shortSimulation.YearlyOutput = !shortSimulation.DailyOutput;
            shortSimulation.ObservedData = copyOfPaddock.ObservedData;
            shortSimulation.Soil = copyOfPaddock.Soil;
            shortSimulation.SoilPath = copyOfPaddock.SoilPath;
            shortSimulation.Samples = new List<APSIM.Shared.Soils.Sample>();
            shortSimulation.Samples.AddRange(copyOfPaddock.Samples);
            shortSimulation.InitTotalWater = copyOfPaddock.InitTotalWater;
            shortSimulation.InitTotalNitrogen = copyOfPaddock.InitTotalNitrogen;
            shortSimulation.StationNumber = copyOfPaddock.StationNumber;
            shortSimulation.StubbleMass = copyOfPaddock.StubbleMass;
            shortSimulation.StubbleType = copyOfPaddock.StubbleType;
            shortSimulation.Management = new List<Management>();
            shortSimulation.Management.AddRange(copyOfPaddock.Management);
            shortSimulation.UseEC = paddock.UseEC;
            shortSimulation.WriteDepthFile = false;
            shortSimulation.TypeOfRun = paddock.RunType;
            shortSimulation.DecileDate = paddock.StartSeasonDate;
            shortSimulation.NUnlimited = paddock.NUnlimited;
            shortSimulation.NUnlimitedFromToday = paddock.NUnlimitedFromToday;
            AddResetDatesToManagement(copyOfPaddock, shortSimulation);

            // Do a stable sort on management actions.
            shortSimulation.Management = shortSimulation.Management.OrderBy(m => m.Date).ToList();

            return shortSimulation;
        }

        /// <summary>Create a series of APSIM simulation specifications for a YP crop report.</summary>
        /// <param name="yieldProphet">The yield prophet specification.</param>
        /// <returns>The created APSIM simulation specs.</returns>
        private static List<APSIMSpec> CropReport(YieldProphet yieldProphet)
        {
            List<APSIMSpec> simulations = new List<APSIMSpec>();
            Paddock paddock = yieldProphet.Paddock[0];

            APSIMSpec thisYear = CreateBaseSimulation(paddock);
            thisYear.Name = "ThisYear";
            thisYear.WriteDepthFile = true;
            thisYear.TypeOfRun = Paddock.RunTypeEnum.SingleSeason;
            simulations.Add(thisYear);

            APSIMSpec seasonSimulation = CreateBaseSimulation(paddock);
            seasonSimulation.Name = "Base";
            seasonSimulation.DailyOutput = false;
            seasonSimulation.YearlyOutput = true;
            seasonSimulation.EndDate = seasonSimulation.StartDate.AddDays(360);
            seasonSimulation.TypeOfRun = Paddock.RunTypeEnum.LongTermPatched;
            simulations.Add(seasonSimulation);

            APSIMSpec NUnlimitedSimulation = CreateBaseSimulation(paddock);
            NUnlimitedSimulation.Name = "NUnlimited";
            NUnlimitedSimulation.DailyOutput = false;
            NUnlimitedSimulation.YearlyOutput = true;
            NUnlimitedSimulation.EndDate = NUnlimitedSimulation.StartDate.AddDays(360);
            NUnlimitedSimulation.TypeOfRun = Paddock.RunTypeEnum.LongTermPatched;
            NUnlimitedSimulation.NUnlimited = true;
            simulations.Add(NUnlimitedSimulation);

            APSIMSpec NUnlimitedFromTodaySimulation = CreateBaseSimulation(paddock);
            NUnlimitedFromTodaySimulation.Name = "NUnlimitedFromToday";
            NUnlimitedFromTodaySimulation.DailyOutput = false;
            NUnlimitedFromTodaySimulation.YearlyOutput = true;
            NUnlimitedFromTodaySimulation.EndDate = NUnlimitedFromTodaySimulation.StartDate.AddDays(360);
            NUnlimitedFromTodaySimulation.TypeOfRun = Paddock.RunTypeEnum.LongTermPatched;
            NUnlimitedFromTodaySimulation.NUnlimitedFromToday = true;
            simulations.Add(NUnlimitedFromTodaySimulation);

            APSIMSpec Next10DaysDry = CreateBaseSimulation(paddock);
            Next10DaysDry.Name = "Next10DaysDry";
            Next10DaysDry.DailyOutput = false;
            Next10DaysDry.YearlyOutput = true;
            Next10DaysDry.EndDate = Next10DaysDry.StartDate.AddDays(360);
            Next10DaysDry.TypeOfRun = Paddock.RunTypeEnum.LongTermPatched;
            Next10DaysDry.Next10DaysDry = true;
            simulations.Add(Next10DaysDry);
            return simulations;
        }

        /// <summary>Add in reset management events to the APSIM spec for the specified paddock.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="simulation">The simulation to add the management operations to.</param>
        /// <exception cref="System.Exception">Cannot find soil water reset date</exception>
        /// <exception cref="Exception">Cannot find soil water reset date</exception>
        private static void AddResetDatesToManagement(Paddock paddock, APSIMSpec simulation)
        {
            // Reset
            if (paddock.SoilWaterSampleDate != DateTime.MinValue)
            {

                if (paddock.SoilNitrogenSampleDate == DateTime.MinValue)
                    paddock.SoilNitrogenSampleDate = paddock.SoilWaterSampleDate;

                Sow sowing = YieldProphetUtility.GetCropBeingSown(paddock.Management);
                if (sowing != null && sowing.Date != DateTime.MinValue)
                {
                    List<Management> resetActions = new List<Management>();

                    // reset at sowing if the sample dates are after sowing.
                    if (paddock.SoilWaterSampleDate > sowing.Date)
                    {
                        resetActions.Add(new ResetWater() { Date = sowing.Date });
                        resetActions.Add(new ResetSurfaceOrganicMatter() { Date = sowing.Date });
                    }
                    if (paddock.SoilNitrogenSampleDate > sowing.Date)
                        resetActions.Add(new ResetNitrogen() { Date = sowing.Date });

                    // reset on the sample dates.
                    resetActions.Add(new ResetWater() { Date = paddock.SoilWaterSampleDate });
                    resetActions.Add(new ResetSurfaceOrganicMatter() { Date = paddock.SoilWaterSampleDate });
                    resetActions.Add(new ResetNitrogen() { Date = paddock.SoilNitrogenSampleDate });

                    simulation.Management.InsertRange(0, resetActions);
                }
            }
        }
    }
}
