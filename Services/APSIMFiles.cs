// -----------------------------------------------------------------------
// <copyright file="CreateAPSIMFilesFromJobSpec.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.IO;
    using System.Reflection;
    using ApsimFile;
    using System.Xml.Serialization;
    using System.Data;

    /// <summary>TODO: Update summary.</summary>
    public class APSIMFiles
    {
        /// <summary>Create all necessary YP files (.apsim and .met) from a YieldProphet spec.</summary>
        /// <param name="apsim">The yield prophet spec.</param>
        /// <param name="endDate">The end date for using any observed data.</param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <param name="filterFileName">The name of a file containing paddocks to include. Can be null.</param>
        /// <returns>The name of the created .apsim file.</returns>
        public static string Create(IEnumerable<Specification.APSIM> simulations, string workingFolder)
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
        private static void CreateWeatherFilesForSimulations(IEnumerable<Specification.APSIM> simulations, string workingFolder, XmlNode apsimNode)
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
            foreach (Specification.APSIM simulation in simulations)
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

            if (nowDate < latestEndDate)
                weatherData.CreateLongTerm(rainFileName, stationNumber,
                                           earliestStartDate, latestEndDate, nowDate,
                                           observedData, 30);
            else
                weatherData.CreateOneSeason(rainFileName, stationNumber,
                                            earliestStartDate, latestEndDate,
                                            observedData);

            // Now modify the simulations to create a met factorial.
            foreach (Specification.APSIM simulation in simulations)
                APSIMFileWriter.CreateMetFactorial(apsimNode, simulation.Name, rainFileName, weatherData.FilesCreated);
        }

        /// <summary>Create a .apsim file for the job</summary>
        /// <param name="yieldProphetSpec">The specification to use</param>
        /// <param name="filterFileName">Name of the filter file.</param>
        /// <returns>The root XML node for the file</returns>
        /// <exception cref="System.Exception"></exception>
        private static XmlNode CreateApsimFile(IEnumerable<Specification.APSIM> simulations)
        {
            APSOIL.Service apsoilService = null;
            XmlDocument doc = new XmlDocument();
            doc.AppendChild(doc.CreateElement("folder"));
            Utility.Xml.SetNameAttr(doc.DocumentElement, "Simulations");
            Utility.Xml.SetAttribute(doc.DocumentElement, "version", APSIMChangeTool.CurrentVersion.ToString());

            foreach (Specification.APSIM simulation in simulations)
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
        private static XmlNode CreateSimulationXML(Specification.APSIM simulation, APSOIL.Service apsoilService)
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
            apsimWriter.SetStubble(simulation.StubbleType, simulation.StubbleMass, Specification.Utils.GetStubbleCNRatio(simulation.StubbleType));

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

            // Do soil stuff.
            DoSoil(simulation, apsoilService);
            apsimWriter.SetSoil(simulation.Soil);

            // Loop through all management actions and create an operations list
            foreach (Specification.Management management in simulation.Management)
            {
                if (management is Specification.Sow)
                    apsimWriter.AddSowingOperation(management as Specification.Sow);
                else if (management is Specification.Fertilise)
                    apsimWriter.AddFertilseOperation(management as Specification.Fertilise);
                else if (management is Specification.Irrigate)
                    apsimWriter.AddIrrigateOperation(management as Specification.Irrigate);
                else if (management is Specification.Tillage)
                    apsimWriter.AddTillageOperation(management as Specification.Tillage);
                else if (management is Specification.StubbleRemoved)
                    apsimWriter.AddStubbleRemovedOperation(management as Specification.StubbleRemoved);
                else if (management is Specification.ResetWater)
                    apsimWriter.AddResetWaterOperation(management as Specification.ResetWater);
                else if (management is Specification.ResetNitrogen)
                    apsimWriter.AddResetNitrogenOperation(management as Specification.ResetNitrogen);
                else if (management is Specification.ResetSurfaceOrganicMatter)
                    apsimWriter.AddSurfaceOrganicMatterOperation(management as Specification.ResetSurfaceOrganicMatter);
            }

            return apsimWriter.ToXML();
        }

        /// <summary>Do all soil related settings.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="apsoilService">The apsoil service.</param>
        /// <exception cref="System.Exception">Cannot find soil:  + paddock.SoilName</exception>
        public static void DoSoil(Specification.APSIM simulation, APSOIL.Service apsoilService)
        {
            if (simulation.Soil == null)
            {
                // Look for a <SoilName> and if found go get the soil from the Apsoil web service.
                if (apsoilService == null)
                    apsoilService = new APSOIL.Service();
                string soilXml = apsoilService.SoilXML(simulation.SoilPath);
                if (soilXml == string.Empty)
                    throw new Exception("Cannot find soil: " + simulation.SoilPath);

                simulation.Soil = Soil.Create(soilXml);
            }
            // Make sure we have a soil crop parameterisation. If not then try creating one
            // based on wheat.
            Specification.Sow crop = Specification.Utils.GetCropBeingSown(simulation.Management);
            if (crop != null && !Utility.String.Contains(simulation.Soil.CropNames, crop.Crop))
            {
                SoilCrop wheat = simulation.Soil.Crop("wheat");

                SoilCrop newSoilCrop = new SoilCrop();
                newSoilCrop.Name = crop.Crop;
                newSoilCrop.Thickness = wheat.Thickness;
                newSoilCrop.LL = wheat.LL;
                newSoilCrop.KL = wheat.KL;
                newSoilCrop.XF = wheat.XF;
                simulation.Soil.Water.Crops.Add(newSoilCrop);
            }

            // Remove any initwater nodes.
            simulation.Soil.InitialWater = null;

            // Create a soil water sample.
            if (simulation.Samples != null)
                simulation.Soil.Samples = simulation.Samples;
            foreach (Sample sample in simulation.Soil.Samples)
                CheckSample(simulation.Soil, sample);

            // get rid of <soiltype> from the soil
            // this is necessary because NPD uses this field and puts in really long
            // descriptive classifications. Soiln2 bombs with an FString internal error.
            simulation.Soil.SoilType = "";

            // Set the soil name to 'soil'
            simulation.Soil.Name = "Soil";
        }

        /// <summary>Checks the soil sample.</summary>
        /// <param name="parentSoil">The parent soil.</param>
        /// <param name="sample">The sample.</param>
        private static void CheckSample(Soil parentSoil, Sample sample)
        {
            if (sample.SW != null)
            {
                // Convert the units to volumetric temporarily.
                Sample.SWUnitsEnum savedUnits = sample.SWUnits;
                sample.SWUnitsSet(Sample.SWUnitsEnum.Volumetric, parentSoil);

                // Make sure the soil water isn't below airdry or above DUL.
                double[] SWValues = sample.SW;
                double[] AirDry = parentSoil.AirDryMapped(sample.Thickness);
                double[] DUL = parentSoil.DULMapped(sample.Thickness);
                for (int i = 0; i < sample.SW.Length; i++)
                {
                    SWValues[i] = Math.Max(SWValues[i], AirDry[i]);
                    SWValues[i] = Math.Min(SWValues[i], DUL[i]);
                }

                // Convert the units back to what it was
                sample.SW = SWValues;
                sample.SWUnitsSet(savedUnits, parentSoil);
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
