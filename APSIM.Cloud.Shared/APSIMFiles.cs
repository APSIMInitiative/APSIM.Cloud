// -----------------------------------------------------------------------
// <copyright file="APSIMFiles.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using APSIM.Shared.Soils;
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Xml;

    /// <summary>TODO: Update summary.</summary>
    public class APSIMFiles
    {
        public static int APSIMVerionNumber = 36;

        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="simulation">The simulation to write.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="fileNameToWrite">The name of a file to write.</param>
        /// <returns>The name of the created .apsim file.</returns>
        public static string Create(APSIMSpecification simulation, string workingFolder, string fileNameToWrite)
        {
            return Create(new List<APSIMSpecification>() { simulation }, workingFolder, fileNameToWrite);
        }

        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="simulations">The simulations to write.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="fileNameToWrite">The name of a file to write.</param>
        /// <returns>The name of the created .apsim file.</returns>
        public static string Create(List<APSIMSpecification> simulations, string workingFolder, string fileNameToWrite)
        {
            bool usingAPSIMx = Path.GetExtension(fileNameToWrite) == ".apsimx";

            // Create the .apsim XML
            XmlDocument doc = new XmlDocument();
            if (usingAPSIMx)
            {
                doc.AppendChild(doc.CreateElement("Simulations"));
                XmlUtilities.SetValue(doc.DocumentElement, "Name", "Simulations");
                XmlUtilities.SetValue(doc.DocumentElement, "DataStore/Name", "DataStore");
            }
            else
            {
                doc.AppendChild(doc.CreateElement("folder"));
                XmlUtilities.SetNameAttr(doc.DocumentElement, "Simulations");
                XmlUtilities.SetAttribute(doc.DocumentElement, "version", APSIMVerionNumber.ToString());
            }

            // Determine whether all simulations are single season.
            bool allSimulationsAreSingleSeason = true;
            foreach (APSIMSpecification simulation in simulations)
                if (simulation.RunType != APSIMSpecification.RunTypeEnum.Normal)
                    allSimulationsAreSingleSeason = false;

            WeatherFileCache weatherCache = new WeatherFileCache();
            foreach (APSIMSpecification simulation in simulations.FindAll(sim=>sim.ErrorMessage == null))
            {
                try
                {
                    CreateWeatherFilesForSimulations(simulation, workingFolder, weatherCache, allSimulationsAreSingleSeason);
                    CreateApsimFile(simulation, workingFolder, doc.DocumentElement, usingAPSIMx);
                }
                catch (Exception err)
                {
                    simulation.ErrorMessage = simulation.Name + ": " + err.Message;
                }
            }

            // Apply factors.
            if (!usingAPSIMx)
                foreach (APSIMSpecification simulation in simulations)
                {
                    if (simulation.ErrorMessage == null && simulation.Factors != null)
                        foreach (APSIMSpecification.Factor factor in simulation.Factors)
                            APSIMFileWriter.ApplyFactor(doc.DocumentElement, factor);
                }

            // Write the .apsim file.
            string apsimFileName = Path.Combine(workingFolder, fileNameToWrite);
            File.WriteAllText(apsimFileName, XmlUtilities.FormattedXML(doc.DocumentElement.OuterXml));

            // Write a .spec file.
            string apsimRunFileName = Path.Combine(workingFolder, Path.ChangeExtension(fileNameToWrite, ".spec"));
            string xml = XmlUtilities.Serialise(simulations, false);
            File.WriteAllText(apsimRunFileName, xml);

            return apsimFileName;
        }

        /// <summary>Creates the weather files for all simulations.</summary>
        /// <param name="simulations">The simulations.</param>
        /// <param name="workingFolder">The working folder to create the files in.</param>
        /// <param name="allSimulationsAreSingleSeason">All simulations are short season?</param>
        private static void CreateWeatherFilesForSimulations(APSIMSpecification simulation, string workingFolder, WeatherFileCache weatherCache, bool allSimulationsAreSingleSeason)
        {
            if (simulation.ErrorMessage == null)
            {
                string rainFileName = Path.Combine(workingFolder, simulation.Name + ".met");

                string[] filesCreated = null;

                // Make sure the observed data has a codes column.
                if (simulation.ObservedData != null)
                    Weather.AddCodesColumn(simulation.ObservedData, 'O');

                if (simulation.RunType == APSIMSpecification.RunTypeEnum.LongTermPatched )
                {
                    // long term.
                    DateTime longTermStartDate = new DateTime(simulation.LongtermStartYear, 1, 1);
                    int numYears = simulation.StartDate.Year - longTermStartDate.Year + 1;

                    // Check to see if in cache.
                    filesCreated = weatherCache.GetWeatherFiles(simulation.StationNumber,
                                                                simulation.StartDate, simulation.NowDate,
                                                                simulation.ObservedData.TableName, numYears);
                    if (filesCreated == null)
                    {
                        // Create a long term weather file.
                        filesCreated = Weather.CreateLongTerm(rainFileName, simulation.StationNumber,
                                                              simulation.StartDate, simulation.NowDate,
                                                              simulation.ObservedData, simulation.DecileDate, numYears);
                        weatherCache.AddWeatherFiles(simulation.StationNumber,
                                                     simulation.StartDate, simulation.NowDate,
                                                     simulation.ObservedData.TableName, numYears, filesCreated);
                    }
                }
                else
                {
                    // short term.
                    // Create a short term weather file.
                    Weather.Data weatherFile = Weather.ExtractDataFromSILO(simulation.StationNumber, simulation.StartDate, simulation.EndDate);
                    Weather.OverlayData(simulation.ObservedData, weatherFile.Table);
                    Weather.WriteWeatherFile(weatherFile.Table, rainFileName, weatherFile.Latitude, weatherFile.Longitude,
                                                 weatherFile.TAV, weatherFile.AMP);
                    filesCreated = new string[] { rainFileName };
                }

                if (filesCreated.Length > 0)
                {
                    simulation.WeatherFileName = Path.GetFileName(filesCreated[0]);

                    // Set the simulation end date to the end date of the weather file. This will avoid
                    // problems where SILO hasn't been updated for a while.
                    ApsimTextFile weatherFile = new ApsimTextFile();
                    try
                    {
                        weatherFile.Open(filesCreated[0]);
                        simulation.EndDate = weatherFile.LastDate;
                    }
                    finally
                    {
                        weatherFile.Close();
                    }
                }

                if (!allSimulationsAreSingleSeason)
                {
                    APSIMSpecification.Factor factor = new APSIMSpecification.Factor();
                    factor.Name = "Met";
                    factor.ComponentPath = "/Simulations/" + simulation.Name + "/Met";
                    factor.ComponentVariableName = "filename";
                    factor.ComponentVariableValues = filesCreated;
                    if (simulation.Factors == null)
                        simulation.Factors = new List<APSIMSpecification.Factor>();
                    simulation.Factors.Add(factor);
                }
            }
        }

        /// <summary>Create a .apsim file node for the job and append it to the parentNode</summary>
        /// <param name="simulation">The specification to use</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="parentNode">Parent XmlNode to append the simulation to.</param>
        /// <param name="usingAPSIMx">Write APSIMx files?</param>
        private static void CreateApsimFile(APSIMSpecification simulation, string workingFolder, XmlNode parentNode, bool usingAPSIMx)
        {
            if (simulation.ErrorMessage == null)
            {
                XmlNode simulationXML = CreateSimulationXML(simulation, workingFolder, usingAPSIMx);
                if (simulationXML != null)
                    parentNode.AppendChild(parentNode.OwnerDocument.ImportNode(simulationXML, true));
            }
        }

        /// <summary>
        /// Create a one year APSIM simulation for the specified yield prophet specification
        /// and paddock
        /// </summary>
        /// <param name="simulation">The specification to use</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="usingAPSIMx">Write APSIMx files?</param>
        /// <returns>The XML node of the APSIM simulation.</returns>
        private static XmlNode CreateSimulationXML(APSIMSpecification simulation, string workingFolder, bool usingAPSIMx)
        {
            IAPSIMFileWriter apsimWriter;
            if (usingAPSIMx)
                apsimWriter = new APSIMxFileWriter();
            else
                apsimWriter = new APSIMFileWriter();

            // Name the paddock.
            apsimWriter.NameSimulation(simulation.Name);

            // Set the clock start and end dates.
            apsimWriter.SetStartEndDate(simulation.StartDate, simulation.EndDate);

            // Set the report date.
            apsimWriter.SetReportDate(simulation.NowDate);

            // Set the weather file
            apsimWriter.SetWeatherFile(simulation.WeatherFileName);

            // Set the stubble
            apsimWriter.SetStubble(simulation.StubbleType, simulation.StubbleMass, YieldProphetUtility.GetStubbleCNRatio(simulation.StubbleType));

            // Set NUnlimited 
            if (simulation.NUnlimited)
                apsimWriter.SetNUnlimited();

            // Set NUnlimited from today
            if (simulation.NUnlimitedFromToday)
                apsimWriter.SetNUnlimitedFromToday();

            if (simulation.WriteDepthFile)
                apsimWriter.WriteDepthFile();

            if (simulation.Next10DaysDry)
                apsimWriter.Next10DaysDry();

            apsimWriter.SetErosion(simulation.Slope, simulation.SlopeLength);

            // Do soil stuff.
            DoSoil(simulation, workingFolder);
            apsimWriter.SetSoil(simulation.Soil);

            // Loop through all management actions and create an operations list
            foreach (Management management in simulation.Management)
            {
                if (management is Sow)
                    apsimWriter.AddSowingOperation(management as Sow, simulation.UseEC);
                else if (management is Fertilise)
                    apsimWriter.AddFertilseOperation(management as Fertilise);
                else if (management is Irrigate)
                    apsimWriter.AddIrrigateOperation(management as Irrigate);
                else if (management is Tillage)
                    apsimWriter.AddTillageOperation(management as Tillage);
                else if (management is StubbleRemoved)
                    apsimWriter.AddStubbleRemovedOperation(management as StubbleRemoved);
                else if (management is ResetWater)
                    apsimWriter.AddResetWaterOperation(management as ResetWater);
                else if (management is ResetNitrogen)
                    apsimWriter.AddResetNitrogenOperation(management as ResetNitrogen);
                else if (management is ResetSurfaceOrganicMatter)
                    apsimWriter.AddSurfaceOrganicMatterOperation(management as ResetSurfaceOrganicMatter);
            }

            // Set Daily output
            if (simulation.DailyOutput)
                apsimWriter.SetDailyOutput();

            // Set Monthly output
            if (simulation.MonthlyOutput)
                apsimWriter.SetMonthlyOutput();

            // Set Yearly output
            if (simulation.YearlyOutput)
                apsimWriter.SetYearlyOutput();

            return apsimWriter.ToXML();
        }

        /// <summary>Do all soil related settings.</summary>
        /// <param name="simulation">The specification to use</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        private static void DoSoil(APSIMSpecification simulation, string workingFolder)
        {
            Soil soil;
            if (simulation.Soil == null)
            {
                if (simulation.SoilPath.StartsWith("<Soil"))
                {
                    // Soil and Landscape grid
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(simulation.SoilPath);
                    soil = XmlUtilities.Deserialise(doc.DocumentElement, typeof(Soil)) as Soil;
                }
                else if (simulation.SoilPath.StartsWith("http"))
                {
                    // Soil and Landscape grid
                    string xml;
                    using (var client = new WebClient())
                    {
                        xml = client.DownloadString(simulation.SoilPath);
                    }
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);
                    List<XmlNode> soils = XmlUtilities.ChildNodes(doc.DocumentElement, "Soil");
                    if (soils.Count == 0)
                        throw new Exception("Cannot find soil in Soil and Landscape Grid");
                    soil = XmlUtilities.Deserialise(soils[0], typeof(Soil)) as Soil;
                }				
                else
                {
                    // Apsoil web service.
                    APSOIL.Service apsoilService = new APSOIL.Service();
                    string soilXml = apsoilService.SoilXML(simulation.SoilPath.Replace("\r\n", ""));
                    if (soilXml == string.Empty)
                        throw new Exception("Cannot find soil: " + simulation.SoilPath);
                    soil = SoilUtilities.FromXML(soilXml);
                }
            }
            else
            {
                // Just use the soil we already have
                soil = simulation.Soil; 
            }

            // Make sure we have a soil crop parameterisation. If not then try creating one
            // based on wheat.
            Sow sowing = YieldProphetUtility.GetCropBeingSown(simulation.Management);
            string[] cropNames = soil.Water.Crops.Select(c => c.Name).ToArray();
            if (cropNames.Length == 0)
                throw new Exception("Cannot find any crop parameterisations in soil: " + simulation.SoilPath);

            if (sowing != null && !StringUtilities.Contains(cropNames, sowing.Crop))
            {
                SoilCrop wheat = soil.Water.Crops.Find(c => c.Name.Equals("wheat", StringComparison.InvariantCultureIgnoreCase));
                if (wheat == null)
                {
                    // Use the first crop instead.
                    wheat = soil.Water.Crops[0];
                }

                SoilCrop newSoilCrop = new SoilCrop();
                newSoilCrop.Name = sowing.Crop;
                newSoilCrop.Thickness = wheat.Thickness;
                newSoilCrop.LL = wheat.LL;
                newSoilCrop.KL = wheat.KL;
                newSoilCrop.XF = wheat.XF;
                soil.Water.Crops.Add(newSoilCrop);
            }

            // Remove any initwater nodes.
            soil.InitialWater = null;

            // Transfer the simulation samples to the soil
            if (simulation.Samples != null)
                soil.Samples = simulation.Samples;
            
            if (simulation.InitTotalWater != 0)
            {
                soil.InitialWater = new InitialWater();
                soil.InitialWater.PercentMethod = InitialWater.PercentMethodEnum.FilledFromTop;

                double pawc;
                if (sowing == null || sowing.Crop == null)
                {
                    pawc = MathUtilities.Sum(PAWC.OfSoilmm(soil));
                    soil.InitialWater.RelativeTo = "LL15";
                }
                else
                {
                    SoilCrop crop = soil.Water.Crops.Find(c => c.Name.Equals(sowing.Crop, StringComparison.InvariantCultureIgnoreCase));
                    pawc = MathUtilities.Sum(PAWC.OfCropmm(soil, crop));
                    soil.InitialWater.RelativeTo = crop.Name;
                }

                soil.InitialWater.FractionFull = Convert.ToDouble(simulation.InitTotalWater) / pawc;
            }

            if (simulation.InitTotalNitrogen != 0)
            {
                // Add in a sample.
                Sample nitrogenSample = new Sample();
                nitrogenSample.Name = "NitrogenSample";
                soil.Samples.Add(nitrogenSample);
                nitrogenSample.Thickness = new double[] { 150, 150, 3000 };
                nitrogenSample.NO3Units = Nitrogen.NUnitsEnum.kgha;
                nitrogenSample.NH4Units = Nitrogen.NUnitsEnum.kgha;
                nitrogenSample.NO3 = new double[] { 6.0, 2.1, 0.1 };
                nitrogenSample.NH4 = new double[] { 0.5, 0.1, 0.1 };
                nitrogenSample.OC = new double[] { double.NaN, double.NaN, double.NaN };
                nitrogenSample.EC = new double[] { double.NaN, double.NaN, double.NaN };
                nitrogenSample.PH = new double[] { double.NaN, double.NaN, double.NaN };

                double Scale = Convert.ToDouble(simulation.InitTotalNitrogen) / MathUtilities.Sum(nitrogenSample.NO3);
                nitrogenSample.NO3 = MathUtilities.Multiply_Value(nitrogenSample.NO3, Scale);
            }

            // Add in soil temperature. Needed for Aflatoxin risk.
            soil.SoilTemperature = new SoilTemperature();
            soil.SoilTemperature.BoundaryLayerConductance = 15;
            soil.SoilTemperature.Thickness = new double[] { 2000 };
            soil.SoilTemperature.InitialSoilTemperature = new double[] { 22 };
            if (soil.Analysis.ParticleSizeClay == null)
                soil.Analysis.ParticleSizeClay = MathUtilities.CreateArrayOfValues(60, soil.Analysis.Thickness.Length);
            InFillMissingValues(soil.Analysis.ParticleSizeClay);

            foreach (Sample sample in soil.Samples)
                CheckSample(soil, sample, sowing);

            Defaults.FillInMissingValues(soil);

            // Correct the CONA / U parameters depending on LAT/LONG
            CorrectCONAU(simulation, soil, workingFolder);

            // get rid of <soiltype> from the soil
            // this is necessary because NPD uses this field and puts in really long
            // descriptive classifications. Soiln2 bombs with an FString internal error.
            soil.SoilType = null;

            // Set the soil name to 'soil'
            soil.Name = "Soil";

            simulation.Soil = soil;
        }

        /// <summary>
        /// Correct the CONA / U parameters depending on LAT/LONG
        /// </summary>
        /// <param name="simulation"></param>
        /// <param name="soil"></param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        private static void CorrectCONAU(APSIMSpecification simulation, Soil soil, string workingFolder)
        {
            // Get lat/long from weather file.
            ApsimTextFile weather = new ApsimTextFile();
            try
            {
                weather.Open(Path.Combine(workingFolder, simulation.WeatherFileName));
                double latitude = Convert.ToDouble(weather.Constant("Latitude").Value);
                double longitude = Convert.ToDouble(weather.Constant("Longitude").Value);

                // Dubbo latitude = -32.24
                // NSW western border longitude = 141.0
                if (longitude >= 141.0 && latitude > -32.24)
                {
                    // Northern values.
                    soil.SoilWater.SummerU = 6.0;
                    soil.SoilWater.SummerCona = 3.5;
                    soil.SoilWater.WinterU = 4.0;
                    soil.SoilWater.WinterCona = 2.5;
                }
                else
                {
                    // Southern values.
                    soil.SoilWater.SummerU = 6.0;
                    soil.SoilWater.SummerCona = 3.5;
                    soil.SoilWater.WinterU = 2.0;
                    soil.SoilWater.WinterCona = 2.0;
                }
                soil.SoilWater.SummerDate = "1-Nov";
                soil.SoilWater.WinterDate = "1-Apr";
            }
            finally
            {
                weather.Close();
            }
        }

        /// <summary>
        /// In fill missing values in the specified array, taking the bottom
        /// values and copying it down the layers.
        /// </summary>
        /// <param name="values">The values to check</param>
        private static void InFillMissingValues(double[] values)
        {
            // find the last non missing value.
            double lastValue = double.NaN;
            for (int i = 0; i < values.Length; i++)
            {
                if (!Double.IsNaN(values[i]))
                    lastValue = values[i];
            }

            // replace all missing values.
            for (int i = 0; i < values.Length; i++)
            {
                if (Double.IsNaN(values[i]))
                    values[i] = lastValue;
            }
        }

        /// <summary>Checks the soil sample.</summary>
        /// <param name="parentSoil">The parent soil.</param>
        /// <param name="sample">The sample.</param>
        /// <param name="sowing">The sowing rule</param>
        private static void CheckSample(Soil parentSoil, Sample sample, Sow sowing)
        {
            // Do some checking of NO3 / NH4
            CheckMissingValuesAreNaN(sample.NO3);
            CheckMissingValuesAreNaN(sample.NH4);
            CheckMissingValuesAreNaN(sample.OC);
            CheckMissingValuesAreNaN(sample.EC);
            CheckMissingValuesAreNaN(sample.PH);

            if (sample.SW != null && sample.SW.Length > 0)
            {
                // Check to make sure SW is >= CLL for layers below top layer
                int cropIndex = parentSoil.Water.Crops.FindIndex(crop => crop.Name.Equals(sowing.Crop, StringComparison.CurrentCultureIgnoreCase));
                if (cropIndex != -1)
                {
                    double[] CLLVol = parentSoil.Water.Crops[cropIndex].LL;
                    double[] CLLVolMapped = LayerStructure.MapConcentration(CLLVol, parentSoil.Water.Thickness,
                                                                         sample.Thickness, 0);

                    if (sample.SWUnits == Sample.SWUnitsEnum.Gravimetric)
                    {
                        // Sample SW is in gravimetric
                        double[] BDMapped = LayerStructure.MapConcentration(parentSoil.Water.BD, parentSoil.Water.Thickness,
                                                                            sample.Thickness, parentSoil.Water.BD.Last());
                        double[] CLLGrav = MathUtilities.Divide(CLLVolMapped, BDMapped); // vol to grav
                        double[] SWVol = MathUtilities.Multiply(sample.SW, BDMapped); // grav to vol
                        for (int i = 1; i < sample.Thickness.Length; i++)
                        {
                            if (SWVol[i] < CLLVolMapped[i])
                                sample.SW[i] = CLLGrav[i];
                        }
                    }
                    else
                    {
                        // Sample SW is in volumetric
                        for (int i = 1; i < sample.Thickness.Length; i++)
                        {
                            if (sample.SW[i] < CLLVolMapped[i])
                                sample.SW[i] = CLLVolMapped[i];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Make sure that the values passed in don't have -999999. Throw exception
        /// when that happens
        /// </summary>
        /// <param name="values">The values.</param>
        /// <exception cref="System.Exception">Use double.NaN for missing values in soil array values</exception>
        private static void CheckMissingValuesAreNaN(double[] values)
        {
            if (values != null && values.FirstOrDefault(v => v == MathUtilities.MissingValue) != 0)
                throw new Exception("Use NaN for missing values in soil array values");
        }

        private class WeatherFileCache
        {
            Dictionary<string, string[]> cache = new Dictionary<string, string[]>();

            public string[] GetWeatherFiles(int stationNumber, DateTime startDate, DateTime NowDate,
                                            string observedDataName, int numYears)
            {
                string key = stationNumber.ToString() + startDate + NowDate + observedDataName + numYears;
                if (cache.ContainsKey(key))
                    return cache[key];
                else
                    return null;
            }

            public void AddWeatherFiles(int stationNumber, DateTime startDate, DateTime NowDate,
                                        string observedDataName, int numYears, string[] fileNames)
            {
                string key = stationNumber.ToString() + startDate + NowDate + observedDataName + numYears;
                if (cache.ContainsKey(key))
                    cache[key] = fileNames;
                else
                    cache.Add(key, fileNames);
            }
        }
    }
}
