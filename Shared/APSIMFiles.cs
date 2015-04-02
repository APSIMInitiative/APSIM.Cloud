// -----------------------------------------------------------------------
// <copyright file="APSIMFiles.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.IO;
    using System.Reflection;
    using System.Xml.Serialization;
    using System.Data;
    using APSIM.Shared.Soils;

    /// <summary>TODO: Update summary.</summary>
    public class APSIMFiles
    {
        private static int APSIMVerionNumber = 36;

        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="apsim">The yield prophet spec.</param>
        /// <param name="endDate">The end date for using any observed data.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="filterFileName">The name of a file containing paddocks to include. Can be null.</param>
        /// <returns>The name of the created .apsim file.</returns>
        public static string Create(IEnumerable<APSIMSpec> simulations, string workingFolder)
        {
            // Create the .apsim XML
            XmlNode apsimNode = CreateApsimFile(simulations);

            // Create all necessary weather files.
            CreateWeatherFilesForSimulations(simulations, workingFolder, apsimNode);

            // Write the .apsim file.
            string apsimFileName = Path.Combine(workingFolder, "YieldProphet.apsim");
            StreamWriter writer = new StreamWriter(apsimFileName);
            writer.Write(Utility.Xml.FormattedXML(apsimNode.OuterXml));
            writer.Close();

            return apsimFileName;
        }

        /// <summary>Creates the weather files for all simulations.</summary>
        /// <param name="simulations">The simulations.</param>
        /// <param name="workingFolder">The working folder to create the files in.</param>
        private static void CreateWeatherFilesForSimulations(IEnumerable<APSIMSpec> simulations, string workingFolder, XmlNode apsimNode)
        {
            // Assume that all simulations are related i.e. use the same observed data.
            // If there are 10 simulations then go find the smallest and largest start 
            // and end dates so that a single weather file set can be created to service
            // all simulations in the set.

            // Write the .met files for each paddock.
            WeatherFile weatherData = new WeatherFile();

            DateTime earliestStartDate = DateTime.MaxValue;
            DateTime latestEndDate = DateTime.MinValue;
            DateTime nowDate = DateTime.MaxValue;
            DataTable observedData = null;
            int stationNumber = 0;
            foreach (APSIMSpec simulation in simulations)
            {
                stationNumber = simulation.StationNumber;
                nowDate = simulation.NowDate;
                observedData = simulation.ObservedData;
                if (simulation.StartDate < earliestStartDate)
                    earliestStartDate = simulation.StartDate;
                if (simulation.EndDate > latestEndDate)
                    latestEndDate = simulation.EndDate;
            }

            // Create the set of weather files.
            string rainFileName = Path.Combine(workingFolder, stationNumber.ToString()) + ".met";

            // Create a short term weather file.
            weatherData.CreateOneSeason(rainFileName, stationNumber,
                            earliestStartDate, nowDate,
                            observedData);


            // Create a long term weather file.
            weatherData.CreateLongTerm(rainFileName, stationNumber,
                                        earliestStartDate, latestEndDate, nowDate,
                                        observedData, 30);

            // Now modify the simulations to create a met factorial.
            foreach (APSIMSpec simulation in simulations)
            {
                if (simulation.EndDate > nowDate)
                {
                    // long term runs
                    APSIMFileWriter.CreateMetFactorial(apsimNode, simulation.Name, rainFileName, weatherData.FilesCreated);
                }
                else
                {
                    // short term runs.
                    string[] shortTermWeatherFiles = new string[] { rainFileName };
                    APSIMFileWriter.CreateMetFactorial(apsimNode, simulation.Name, rainFileName, shortTermWeatherFiles);
                }
            }
        }

        /// <summary>Create a .apsim file for the job</summary>
        /// <param name="yieldProphetSpec">The specification to use</param>
        /// <param name="filterFileName">Name of the filter file.</param>
        /// <returns>The root XML node for the file</returns>
        /// <exception cref="System.Exception"></exception>
        private static XmlNode CreateApsimFile(IEnumerable<APSIMSpec> simulations)
        {
            APSOIL.ServiceSoapClient apsoilService = null;
            XmlDocument doc = new XmlDocument();
            doc.AppendChild(doc.CreateElement("folder"));
            Utility.Xml.SetNameAttr(doc.DocumentElement, "Simulations");
            Utility.Xml.SetAttribute(doc.DocumentElement, "version", APSIMVerionNumber.ToString());

            foreach (APSIMSpec simulation in simulations)
            {
                try
                {
                    XmlNode simulationXML = CreateSimulationXML(simulation, apsoilService);
                    if (simulationXML != null)
                        doc.DocumentElement.AppendChild(doc.ImportNode(simulationXML, true));
                }
                catch (Exception err)
                {
                    throw new Exception(err.Message + "\r\nPaddock name: " + simulation.Name);
                }
            }

            return doc.DocumentElement;
        }

        /// <summary>
        /// Create a one year APSIM simulation for the specified yield prophet specification
        /// and paddock
        /// </summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="todayDate">The today date.</param>
        /// <param name="apsoilService">The apsoil service.</param>
        /// <returns>The XML node of the APSIM simulation.</returns>
        private static XmlNode CreateSimulationXML(APSIMSpec simulation, APSOIL.ServiceSoapClient apsoilService)
        {
            APSIMFileWriter apsimWriter = new APSIMFileWriter();

            // Name the paddock.
            apsimWriter.NameSimulation(simulation.Name);

            // Set the clock start and end dates.
            apsimWriter.SetStartEndDate(simulation.StartDate, simulation.EndDate);

            // Set the report date.
            apsimWriter.SetReportDate(simulation.NowDate);

            // Set the weather file
            apsimWriter.SetWeatherFile(simulation.Name + ".met");

            // Set the stubble
            apsimWriter.SetStubble(simulation.StubbleType, simulation.StubbleMass, YieldProphetUtility.GetStubbleCNRatio(simulation.StubbleType));

            // Set NUnlimited 
            if (simulation.NUnlimited)
                apsimWriter.SetNUnlimited();

            // Set NUnlimited from today
            if (simulation.NUnlimitedFromToday)
                apsimWriter.SetNUnlimitedFromToday();

            // Set Daily output
            if (simulation.DailyOutput)
                apsimWriter.SetDailyOutput();

            // Set Monthly output
            if (simulation.MonthlyOutput)
                apsimWriter.SetMonthlyOutput();

            // Set Yearly output
            if (simulation.YearlyOutput)
                apsimWriter.SetYearlyOutput();

            if (simulation.WriteDepthFile)
                apsimWriter.WriteDepthFile();

            if (simulation.Next10DaysDry)
                apsimWriter.Next10DaysDry();

            apsimWriter.SetErosion(simulation.Slope, simulation.SlopeLength);

            // Do soil stuff.
            Soil soil = DoSoil(simulation, apsoilService);
            apsimWriter.SetSoil(soil);

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

            return apsimWriter.ToXML();
        }

        /// <summary>Do all soil related settings.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="apsoilService">The apsoil service.</param>
        /// <exception cref="System.Exception">Cannot find soil:  + paddock.SoilName</exception>
        public static Soil DoSoil(APSIMSpec simulation, APSOIL.ServiceSoapClient apsoilService)
        {
            Soil soil;
            if (simulation.Soil == null)
            {
                // Look for a <SoilName> and if found go get the soil from the Apsoil web service.
                if (apsoilService == null)
                    apsoilService = new APSOIL.ServiceSoapClient();
                string soilXml = apsoilService.SoilXML(simulation.SoilPath);
                if (soilXml == string.Empty)
                    throw new Exception("Cannot find soil: " + simulation.SoilPath);

                soil = SoilUtility.FromXML(soilXml);
            }
            else
            {
                // Convert webservice proxy soil to a real soil.
                XmlDocument soilDoc = new XmlDocument();
                soilDoc.LoadXml(Utility.Xml.Serialise(simulation.Soil, false));
                soil = Utility.Xml.Deserialise(soilDoc.DocumentElement, typeof(Soil)) as Soil;

                // The crops aren't being Serialised correctly. They are put under a <Crops> node
                // which isn't right. Do them manually.
                foreach (SoilCrop oldCrop in simulation.Soil.Water.Crops)
                {
                    soil.Water.Crops.Add(new SoilCrop()
                    {
                        Name = oldCrop.Name,
                        Thickness = oldCrop.Thickness,
                        LL = oldCrop.LL,
                        KL = oldCrop.KL,
                        XF = oldCrop.XF
                    });
                }
            }

            // Make sure we have a soil crop parameterisation. If not then try creating one
            // based on wheat.
            Sow crop = YieldProphetUtility.GetCropBeingSown(simulation.Management);
            if (crop != null && !Utility.String.Contains(SoilUtility.GetCropNames(soil), crop.Crop))
            {
                SoilCrop wheat = SoilUtility.Crop(soil, "wheat");

                SoilCrop newSoilCrop = new SoilCrop();
                newSoilCrop.Name = crop.Crop;
                newSoilCrop.Thickness = wheat.Thickness;
                newSoilCrop.LL = wheat.LL;
                newSoilCrop.KL = wheat.KL;
                newSoilCrop.XF = wheat.XF;
                soil.Water.Crops.Add(newSoilCrop);
            }

            // Remove any initwater nodes.
            soil.InitialWater = null;

            // Transfer the APSIM simulation proxy samples to the soil
            if (simulation.Samples != null)
            {
                // Convert webservice proxy samples to real samples.
                XmlDocument soilDoc = new XmlDocument();
                soilDoc.LoadXml(Utility.Xml.Serialise(simulation.Samples, false));
                soil.Samples = Utility.Xml.Deserialise(soilDoc.DocumentElement, typeof(List<Sample>)) as List<Sample>;
            }

            foreach (Sample sample in soil.Samples)
                CheckSample(soil, sample);

            // get rid of <soiltype> from the soil
            // this is necessary because NPD uses this field and puts in really long
            // descriptive classifications. Soiln2 bombs with an FString internal error.
            soil.SoilType = "";

            // Set the soil name to 'soil'
            soil.Name = "Soil";

            return soil;
        }

        /// <summary>Checks the soil sample.</summary>
        /// <param name="parentSoil">The parent soil.</param>
        /// <param name="sample">The sample.</param>
        private static void CheckSample(Soil parentSoil, Sample sample)
        {
            if (sample.SW != null)
            {
                // Make sure the soil water isn't below airdry or above DUL.
                double[] SWValues = SoilUtility.SW(parentSoil, sample, Sample.SWUnitsEnum.Volumetric);
                double[] AirDry = SoilUtility.AirDryMapped(parentSoil, sample.Thickness);
                double[] DUL = SoilUtility.DULMapped(parentSoil, sample.Thickness);
                for (int i = 0; i < sample.SW.Length; i++)
                {
                    SWValues[i] = Math.Max(SWValues[i], AirDry[i]);
                    SWValues[i] = Math.Min(SWValues[i], DUL[i]);
                }
            }

            // Do some checking of NO3 / NH4
            CheckMissingValuesAreNaN(sample.NO3);
            CheckMissingValuesAreNaN(sample.NH4);
            CheckMissingValuesAreNaN(sample.OC);
            CheckMissingValuesAreNaN(sample.EC);
            CheckMissingValuesAreNaN(sample.PH);

            if (sample.NO3 != null)
                sample.NO3 = FixArrayLength(sample.NO3, sample.Thickness.Length);
            if (sample.NH4 != null)
                sample.NH4 = FixArrayLength(sample.NH4, sample.Thickness.Length);

            // NH4 can be null so give default values if that is the case.
            for (int i = 0; i < sample.NH4.Length; i++)
                if (double.IsNaN(sample.NH4[i]))
                    sample.NH4[i] = 0.1;

            sample.OCUnits = Sample.OCSampleUnitsEnum.WalkleyBlack;
            if (sample.OC != null)
                sample.OC = FixArrayLength(sample.OC, sample.Thickness.Length);

            if (sample.EC != null)
                sample.EC = FixArrayLength(sample.EC, sample.Thickness.Length);

            if (sample.PH != null)
                sample.PH = FixArrayLength(sample.PH, sample.Thickness.Length);
        }

        /// <summary>
        /// Make sure the specified array is of the specified length. Will pad
        /// with double.NaN to make it the required length.
        /// </summary>
        /// <param name="values">The array of values to resize.</param>
        /// <param name="length">The new size of the array.</param>
        /// <returns>The new array.</returns>
        private static double[] FixArrayLength(double[] values, int length)
        {
            if (values.Length != length)
            {
                int i = values.Length;
                Array.Resize(ref values, length);
                while (i < length)
                {
                    values[i] = double.NaN;
                    i++;
                }
            }
            return values;
        }

        /// <summary>
        /// Make sure that the values passed in don't have -999999. Throw exception
        /// when that happens
        /// </summary>
        /// <param name="values">The values.</param>
        /// <exception cref="System.Exception">Use double.NaN for missing values in soil array values</exception>
        private static void CheckMissingValuesAreNaN(double[] values)
        {
            if (values.FirstOrDefault(v => v == Utility.Math.MissingValue) != 0)
                throw new Exception("Use NaN for missing values in soil array values");
        }


    }
}
