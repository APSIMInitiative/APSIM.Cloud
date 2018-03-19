// -----------------------------------------------------------------------
// <copyright file="AusFarmFiles.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Shared.AusFarm
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
    using APSIM.Shared.Utilities;
    using APSIM.Cloud.Shared;
    
    public class AusFarmFiles
    {
        // private static int AusFarmVersionNumber = 1413;
        private static string FMetFile;

        /// <summary>
        /// Create all necessary simulation files (.sdml and .met) from a Farm4Prophet spec.
        /// </summary>
        /// <param name="simulations"></param>
        /// <param name="workingFolder">The folder where files shoud be created.</param>
        /// <returns>The names of the created .sdml files.</returns>
        public static string[] Create(IEnumerable<AusFarmSpec> simulations, string workingFolder)
        {
            string[] files = new string[simulations.Count()];
            // Create the .sdml XML
            // iterate through the simulations
            int index = 0;
            foreach (AusFarmSpec simulation in simulations)
            {
                // Create all necessary weather files.
                CreateWeatherFileForSimulation(simulation, workingFolder);

                XmlNode ausfarmNode = CreateAusFarmFile(simulation, workingFolder);

                // Write the .sdml file.
                files[index] = "ausfarm_" + index.ToString() + ".sdml";
                string ausfarmFileName = Path.Combine(workingFolder, files[index]);
                StreamWriter writer = new StreamWriter(ausfarmFileName);
                writer.Write(ausfarmNode.OuterXml);
                writer.Close();
                index++;
            }

            return files;
        }

        /// <summary>Creates the weather files for all simulations.</summary>
        /// <param name="simulations">The simulations.</param>
        /// <param name="workingFolder">The working folder to create the files in.</param>
        /// <param name="ausfarmNode"></param>
        private static void CreateWeatherFileForSimulation(AusFarmSpec simulation, string workingFolder)
        {
            // Write the .met file for the simulation
            DateTime earliestStartDate = DateTime.MaxValue;
            DateTime latestEndDate = DateTime.MinValue;
            DateTime nowDate = DateTime.MaxValue;
            DataTable observedData = null;
            int stationNumber = 0;

            stationNumber = simulation.StationNumber;
            if (simulation.StartDate < earliestStartDate)
                earliestStartDate = simulation.StartDate;
            if (simulation.EndDate > latestEndDate)
                latestEndDate = simulation.EndDate;


            // Create the weather files.
            FMetFile = Path.Combine(workingFolder, stationNumber.ToString()) + ".met";

            // Create a weather file.
            Weather.Data weatherFile = Weather.ExtractDataFromSILO(stationNumber, earliestStartDate, nowDate);
            Weather.OverlayData(observedData, weatherFile.Table);
            Weather.WriteWeatherFile(weatherFile.Table, FMetFile, weatherFile.Latitude, weatherFile.Longitude,
                                         weatherFile.TAV, weatherFile.AMP);

            // ensure that the run period doesn't exceed the data retrieved
            if (simulation.EndDate > weatherFile.LastDate)
                simulation.EndDate = weatherFile.LastDate;

            // calculate the rain deciles from April from the year of the start of the simulation. 
            // this could be improved to use more than a few decades of weather.
            simulation.RainDeciles = new double[12, 10]; // 12 months, 10 deciles
            DateTime accumStartDate = new DateTime(simulation.StartDate.Year, 4, 1);
            if (simulation.StartDate > accumStartDate)              // ensure that the start date for decile accum exists in the weather
                accumStartDate.AddYears(1);
            simulation.RainDeciles = Weather.CalculateRainDeciles(stationNumber, accumStartDate, simulation.EndDate);
        }

        /// <summary>Create a .sdml file for the job</summary>
        /// <param name="AusFarmSpec">The specification to use</param>
        /// <returns>The root XML node for the file</returns>
        private static XmlNode CreateAusFarmFile(AusFarmSpec simulation, string workingFolder)
        {
            
            XmlDocument doc = new XmlDocument();
            try
            {
                XmlNode simulationXML = CreateSimulationXML(simulation, workingFolder);
                if (simulationXML != null)
                    doc.AppendChild(doc.ImportNode(simulationXML, true));
            }
            catch (Exception err)
            {
                throw new Exception(err.Message + "\r\nSimulation name: " + simulation.Name);
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
        private static XmlNode CreateSimulationXML(AusFarmSpec simulation, string workingFolder)
        {
            AusFarmFileWriter ausfarmWriter;

            // determine which type of simulation this is based on stock, paddocks, crops
            if ((simulation.LiveStock.Flocks.Count == 0) && (simulation.LiveStock.TradeLambCount == 0))
            {
                ausfarmWriter = new AusFarmFileWriter(SimulationType.stCropOnly);
                simulation.SimTemplateType = SimulationType.stCropOnly; // ensure that the simulation type is alway correct
            }
            else
            {
                if (simulation.LiveStock.Flocks.Count == 1)
                {
                    ausfarmWriter = new AusFarmFileWriter(SimulationType.stSingleFlock);
                    simulation.SimTemplateType = SimulationType.stSingleFlock; // ensure that the simulation type is alway correct
                }
                else if (simulation.LiveStock.Flocks.Count == 2)
                {
                    ausfarmWriter = new AusFarmFileWriter(SimulationType.stDualFlock);
                    simulation.SimTemplateType = SimulationType.stDualFlock; // ensure that the simulation type is alway correct
                }
                else
                    throw new Exception();
            }

            // Name the simulation
            ausfarmWriter.NameSimulation(simulation.Name);

            // Set the clock start and end dates.
            ausfarmWriter.SetStartEndDate(simulation.StartDate, simulation.EndDate);

            //set the path for output files
            ausfarmWriter.OutputPath(workingFolder);
            ausfarmWriter.ReportName(simulation.ReportName);
            
            // Set the weather file
            ausfarmWriter.SetWeatherFile(FMetFile);
            ausfarmWriter.SetRainDeciles(simulation.RainDeciles);
            ausfarmWriter.SetCroppingRegion(simulation.CroppingRegion);
            ausfarmWriter.SetArea(simulation.Area);
            for (int i = 0; i < simulation.OnFarmSoilTypes.Count; i++)
            {
                ausfarmWriter.SetCropRotation(i + 1, simulation.OnFarmSoilTypes[i].CropRotationList);
            }
            
            // Do soil stuff.
            DoSoils(simulation);
            ausfarmWriter.SetSoils(simulation);

            if (simulation.SimTemplateType != SimulationType.stCropOnly)
            {
                // Set the Livestock data
                ausfarmWriter.WriteStockEnterprises(simulation.LiveStock);
            }

            return ausfarmWriter.ToXML();
        }

        /// <summary>
        /// Retrieves soil types from ApSoil
        /// Configures any missing crop ll, kl values
        /// </summary>
        /// <param name="simulation"></param>
        /// <param name="apsoilService"></param>
        /// <returns></returns>
        public static void DoSoils(AusFarmSpec simulation)
        {
            APSOIL.Service apsoilService = null; 
            Soil soil;
            for (int i = 0; i < simulation.OnFarmSoilTypes.Count; i++)
            {
                FarmSoilType soilType = simulation.OnFarmSoilTypes[i];
                soil = soilType.SoilDescr;
                if (soil == null)
                {
                    // Look for a <SoilName> and if found go get the soil from the Apsoil web service.
                    if (apsoilService == null)
                        apsoilService = new APSOIL.Service();
                    string soilXml = apsoilService.SoilXML(soilType.SoilPath);
                    if (soilXml == string.Empty)
                        throw new Exception("Cannot find soil: " + soilType.SoilPath);

                    soil = SoilUtilities.FromXML(soilXml);
                }
                
                // Other crop types not listed here will have their ll, kll, xf values calculated later

                // Remove any initwater nodes.
                soil.InitialWater = null;

                foreach (Sample sample in soil.Samples)
                    CheckSample(soil, sample);

                // get rid of <soiltype> from the soil
                // this is necessary because NPD uses this field and puts in really long
                // descriptive classifications. Soiln2 bombs with an FString internal error.
                soil.SoilType = "";

                // Set the soil name to 'soil'
                //soil.Name = "Soil";
                soilType.SoilDescr = soil;  //store the changed description
            }
        }

        /// <summary>Checks the soil sample.</summary>
        /// <param name="parentSoil">The parent soil.</param>
        /// <param name="sample">The sample.</param>
        private static void CheckSample(Soil parentSoil, Sample sample)
        {

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

            sample.OCUnits = SoilOrganicMatter.OCUnitsEnum.WalkleyBlack;
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
            if (values.FirstOrDefault(v => v == MathUtilities.MissingValue) != 0)
                throw new Exception("Use NaN for missing values in soil array values");
        }

    }
}
