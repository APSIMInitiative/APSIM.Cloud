// -----------------------------------------------------------------------
// <copyright file="YieldProphetOld.cs" company="APSIM Initiative">
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
    using System.Globalization;
    using System.Xml.Serialization;
    using System.IO;
    using APSIM.Shared.Soils;
    using APSIM.Shared.Utilities;
    using APSIM.Shared.OldAPSIM;

    class YieldProphetOld
    {
        /// <summary>Converts the old Yield Prophet XML to new XML format capable of deserialisation</summary>
        /// <param name="yieldProphetXML">The old Yield Prophet XML</param>
        /// <returns>The new Yield Prophet XML</returns>
        public static YieldProphet YieldProphetFromXML(XmlNode node, string baseFolder)
        {
            List<Paddock> simulations = new List<Paddock>();

            List<XmlNode> paddocks = XmlUtilities.ChildNodes(node, "Paddock");
            for (int p = 0; p < paddocks.Count; p++)
            {
                try
                {
                    Paddock paddock = CreateSimulationSpec(paddocks[p], baseFolder);
                    simulations.Add(paddock);
                }
                catch (Exception err)
                {
                    string name = XmlUtilities.Value(paddocks[p], "Name");
                    throw new Exception(err.Message + "\r\nPaddock name: " + name);
                }
            }

            YieldProphet simulationsSpec = new YieldProphet();
            simulationsSpec.Paddock = simulations;

            // Some top level simulation metadata.
            string reportDescription = XmlUtilities.Value(node, "ReportDescription");
            if (reportDescription != "")
                simulationsSpec.ReportName = reportDescription;
            string reportType = XmlUtilities.Value(node, "ReportType");
            if (reportType == "Crop Report (Complete)")
                simulationsSpec.ReportType = YieldProphet.ReportTypeEnum.Crop;
            else if (reportType == "Sowing Opportunity Report")
                simulationsSpec.ReportType = YieldProphet.ReportTypeEnum.SowingOpportunity;
            simulationsSpec.ClientName = XmlUtilities.Value(node, "GrowerName");
            simulationsSpec.ReportGeneratedBy = XmlUtilities.Value(node, "LoginName");

            return simulationsSpec;
        }

        /// <summary>Converts the paddock XML.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <exception cref="System.Exception">Bad paddock name:  + name</exception>
        private static Paddock CreateSimulationSpec(XmlNode paddock, string baseFolder)
        {
            Paddock simulation = new Paddock();

            string name = XmlUtilities.NameAttr(paddock);
            int posCaret = name.IndexOf('^');

            if (posCaret == -1)
                throw new Exception("Bad paddock name: " + name);

            string remainder = StringUtilities.SplitOffAfterDelimiter(ref name, "^");
            string growerName;
            string paddockName = StringUtilities.SplitOffAfterDelimiter(ref remainder, "^");
            if (paddockName == string.Empty)
            {
                growerName = name;
                paddockName = remainder;
            }
            else
                growerName = remainder;

            simulation.StartSeasonDate = GetDate(paddock, "StartSeasonDateFull");

            // Give the paddock a name.
            string fullName = string.Format("{0};{1};{2}", simulation.StartSeasonDate.Year, growerName, paddockName);
            simulation.Name = fullName;

            // Set the report date.
            simulation.NowDate = GetDate(paddock.ParentNode, "TodayDateFull");
            if (simulation.NowDate == DateTime.MinValue)
                simulation.NowDate = DateTime.Now;

            // Store any rainfall data in the simulation.
            string rainFileName = GetString(paddock, "RainfallFilename");
            if (rainFileName != string.Empty)
            {
                string fullFileName = Path.Combine(baseFolder, rainFileName);
                if (!File.Exists(fullFileName))
                    throw new Exception("Cannot find file: " + fullFileName);
                simulation.ObservedData = ApsimTextFile.ToTable(fullFileName);
            }

            // Set the reset dates
            simulation.SoilWaterSampleDate = GetDate(paddock, "ResetDateFull");
            simulation.SoilNitrogenSampleDate = GetDate(paddock, "SoilNitrogenSampleDateFull");

            simulation.StationNumber = GetInteger(paddock, "StationNumber");
            simulation.StationName = GetString(paddock, "StationName");
            simulation.RainfallSource = GetString(paddock, "RainfallSource");
            
            // Create a sowing management
            Sow sowing = new Sow();
            simulation.Management.Add(sowing);
            sowing.Date = GetDate(paddock, "SowDateFull");
            sowing.EmergenceDate = GetDate(paddock, "EmergenceDateFull");
            sowing.Crop = GetString(paddock, "Crop");           
            sowing.Cultivar = GetString(paddock, "Cultivar");
            sowing.SkipRow = GetString(paddock, "SkipRow");
            sowing.SowingDensity = GetInteger(paddock, "SowingDensity");
            sowing.MaxRootDepth = GetInteger(paddock, "MaxRootDepth") * 10;  // cm to mm
            sowing.BedWidth = GetInteger(paddock, "BedWidth");
            sowing.BedRowSpacing = GetDouble(paddock, "BedRowSpacing");

            // Make sure we have a stubbletype
            simulation.StubbleType = GetString(paddock, "StubbleType");
            if (simulation.StubbleType == string.Empty || simulation.StubbleType == "None")
                simulation.StubbleType = "Wheat";
            simulation.StubbleMass = GetDouble(paddock, "StubbleMass");
            simulation.Slope = GetDouble(paddock, "Slope");
            simulation.SlopeLength = GetDouble(paddock, "SlopeLength");
            simulation.UseEC = GetBoolean(paddock, "UseEC");

            // Fertilise nodes.
            List<XmlNode> fertiliserNodes = XmlUtilities.ChildNodes(paddock, "Fertilise");
            for (int f = 0; f < fertiliserNodes.Count; f++)
            {
                Fertilise fertilise = new Fertilise();
                simulation.Management.Add(fertilise);
                fertilise.Date = GetDate(fertiliserNodes[f], "FertDateFull");
                fertilise.Amount = GetDouble(fertiliserNodes[f], "FertAmount");
                fertilise.Scenario = GetBoolean(fertiliserNodes[f], "Scenario");
            }

            // Irrigate nodes.
            List<XmlNode> irrigateNodes = XmlUtilities.ChildNodes(paddock, "Irrigate");
            for (int i = 0; i < irrigateNodes.Count; i++)
            {
                Irrigate irrigate = new Irrigate();
                simulation.Management.Add(irrigate);
                irrigate.Date = GetDate(irrigateNodes[i], "IrrigateDateFull");
                irrigate.Amount = GetDouble(irrigateNodes[i], "IrrigateAmount");
                irrigate.Efficiency = GetDouble(irrigateNodes[i], "IrrigateEfficiency");
                irrigate.Scenario = GetBoolean(irrigateNodes[i], "Scenario");
            }

            // Tillage nodes.
            foreach (XmlNode tillageNode in XmlUtilities.ChildNodes(paddock, "Tillage"))
            {
                Tillage tillage = new Tillage();
                simulation.Management.Add(tillage);
                tillage.Date = GetDate(tillageNode, "TillageDateFull");
                string disturbance = GetString(tillageNode, "Disturbance");
                if (disturbance == "Low")
                    tillage.Disturbance = Tillage.DisturbanceEnum.Low;
                else if (disturbance == "Medium")
                    tillage.Disturbance = Tillage.DisturbanceEnum.Medium;
                else
                    tillage.Disturbance = Tillage.DisturbanceEnum.High;
                tillage.Scenario = GetBoolean(tillageNode, "Scenario");
            }

            // Stubble removed nodes.
            foreach (XmlNode stubbleRemovedNode in XmlUtilities.ChildNodes(paddock, "StubbleRemoved"))
            {
                StubbleRemoved stubbleRemoved = new StubbleRemoved();
                simulation.Management.Add(stubbleRemoved);

                stubbleRemoved.Date = GetDate(stubbleRemovedNode, "StubbleRemovedDateFull");
                stubbleRemoved.Percent = GetDouble(stubbleRemovedNode, "StubbleRemovedAmount");
            }

            // Look for a soil node.
            XmlNode soilNode = XmlUtilities.FindByType(paddock, "Soil");

            // Fix up soil sample variables.
            Sample sample1 = new Sample();
            sample1.Name = "Sample1";
            sample1.Thickness = GetArray(paddock, "Sample1Thickness");
            sample1.SW = GetArray(paddock, "SW");
            if (sample1.SW == null)
            {
                // Really old way of doing samples - they are stored under soil.
                List<XmlNode> sampleNodes = XmlUtilities.ChildNodes(soilNode, "Sample");
                if (sampleNodes.Count > 0)
                    sample1 = XmlUtilities.Deserialise(sampleNodes[0], typeof(Sample)) as Sample;
            }
            else
            {
                sample1.NO3 = GetArray(paddock, "NO3");
                sample1.NH4 = GetArray(paddock, "NH4");
            }

            Sample sample2 = null;
            double[] sample2Thickness = GetArray(paddock, "Sample2Thickness");
            if (sample2Thickness == null)
            {
                // Really old way of doing samples - they are stored under soil.
                List<XmlNode> sampleNodes = XmlUtilities.ChildNodes(soilNode, "Sample");
                if (sampleNodes.Count > 1)
                    sample2 = XmlUtilities.Deserialise(sampleNodes[1], typeof(Sample)) as Sample;
            }
            else
            {
                sample2 = sample1;
                if (!MathUtilities.AreEqual(sample2Thickness, sample1.Thickness))
                {
                    sample2 = new Sample();
                    sample2.Name = "Sample2";
                }
                sample2.OC = GetArray(paddock, "OC");
                sample2.EC = GetArray(paddock, "EC");
                sample2.PH = GetArray(paddock, "PH");
                sample2.CL = GetArray(paddock, "CL");
                sample2.OCUnits = Sample.OCSampleUnitsEnum.WalkleyBlack;
                sample2.PHUnits = Sample.PHSampleUnitsEnum.CaCl2;
            }

            // Make sure we have NH4 values.
            if (sample1.NH4 == null && sample1.NO3 != null)
            {
                string[] defaultValues = StringUtilities.CreateStringArray("0.1", sample1.NO3.Length);
                sample1.NH4 = MathUtilities.StringsToDoubles(defaultValues);
            }

            RemoveNullFieldsFromSample(sample1);
            if (sample2 != null)
                RemoveNullFieldsFromSample(sample2);


            // Fix up <WaterFormat>
            string waterFormatString = GetString(paddock, "WaterFormat");
            if (waterFormatString.Contains("Gravimetric"))
                sample1.SWUnits = Sample.SWUnitsEnum.Gravimetric;
            else
                sample1.SWUnits = Sample.SWUnitsEnum.Volumetric;

            if (MathUtilities.ValuesInArray(sample1.Thickness))
                simulation.Samples.Add(sample1);

            if (sample2 != null && MathUtilities.ValuesInArray(sample2.Thickness) && sample2 != sample1)
                simulation.Samples.Add(sample2);

            // Check for InitTotalWater & InitTotalNitrogen
            simulation.InitTotalWater = GetDouble(paddock, "InitTotalWater");
            simulation.InitTotalNitrogen = GetDouble(paddock, "InitTotalNitrogen");

            // Check to see if we need to convert the soil structure.
            simulation.SoilPath = GetString(paddock, "SoilName");

            
            if (soilNode != null)
            {

                string testValue = XmlUtilities.Value(soilNode, "Water/Layer/Thickness");
                if (testValue != string.Empty)
                {
                    // old format.
                    soilNode = ConvertSoilNode.Upgrade(soilNode);
                }


                // See if there is a 'SWUnits' value. If found then copy it into 
                // <WaterFormat>
                string waterFormat = XmlUtilities.Value(paddock, "WaterFormat");
                if (waterFormat == string.Empty)
                {
                    int sampleNumber = 0;
                    foreach (XmlNode soilSample in XmlUtilities.ChildNodes(soilNode, "Sample"))
                    {
                        string swUnits = XmlUtilities.Value(soilSample, "SWUnits");
                        if (swUnits != string.Empty)
                            XmlUtilities.SetValue(paddock, "WaterFormat", swUnits);

                        // Also make sure we don't have 2 samples with the same name.
                        string sampleName = "Sample" + (sampleNumber + 1).ToString();
                        XmlUtilities.SetAttribute(soilSample, "name", sampleName);
                        sampleNumber++;
                    }
                }
                simulation.Soil = SoilUtilities.FromXML(soilNode.OuterXml);
                if (simulation.Samples != null)
                    simulation.Soil.Samples = simulation.Samples;
            }
            return simulation;
        }

        private static void RemoveNullFieldsFromSample(Sample sample)
        {
            if (sample.CL == null) sample.CL = new double[0];
            if (sample.EC == null) sample.EC = new double[0];
            if (sample.ESP == null) sample.ESP = new double[0];
            if (sample.NH4 == null) sample.NH4 = new double[0];
            if (sample.NO3 == null) sample.NO3 = new double[0];
            if (sample.OC == null) sample.OC = new double[0];
            if (sample.PH == null) sample.PH = new double[0];
            if (sample.SW == null) sample.SW = new double[0];
        }

        /// <summary>Changes the case of element.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="nodeName">Name of the node.</param>
        private static void ChangeCaseOfElement(XmlNode paddock, string nodeName)
        {
            XmlNode node = XmlUtilities.Find(paddock, nodeName);
            if (node != null)
                XmlUtilities.ChangeType(node, nodeName);
        }

        /// <summary>Gets a boolean value from the specified node name</summary>
        /// <param name="node">The parent node</param>
        /// <param name="nodeName">Name of the child node.</param>
        private static bool GetBoolean(XmlNode node, string nodeName)
        {
            XmlNode booleanNode = XmlUtilities.Find(node, nodeName);
            if (booleanNode != null)
            {
                if (booleanNode.InnerText == "no")
                    return false;
                else
                    return true;
            }
            return false;
        }

        /// <summary>Gets a string value from the specified nodename</summary>
        /// <param name="node">The parent node</param>
        /// <param name="nodeName">Name of the child node.</param>
        /// <returns>The value or "" if none</returns>
        private static string GetString(XmlNode node, string nodeName)
        {
            return XmlUtilities.Value(node, nodeName);
        }

        /// <summary>Gets an integer value from the specified nodename</summary>
        /// <param name="node">The parent node</param>
        /// <param name="nodeName">Name of the child node.</param>
        /// <returns>The value</returns>
        private static int GetInteger(XmlNode node, string nodeName)
        {
            string value = XmlUtilities.Value(node, nodeName);
            if (value == string.Empty)
                return 0;
            else
                return System.Convert.ToInt32(value);
        }

        /// <summary>Gets a double value from the specified nodename</summary>
        /// <param name="node">The parent node</param>
        /// <param name="nodeName">Name of the child node.</param>
        /// <returns>The value</returns>
        private static double GetDouble(XmlNode node, string nodeName)
        {
            string value = XmlUtilities.Value(node, nodeName);
            if (value == string.Empty)
                return 0;
            else
                return System.Convert.ToDouble(value);
        }

        /// <summary>Gets a date from the specified child node name.</summary>
        /// <param name="node">The parent node.</param>
        /// <param name="dateNodeName">The child node name</param>
        /// <returns>The date or DateTime.MinValue if not found</returns>
        private static DateTime GetDate(XmlNode node, string dateNodeName)
        {
            XmlNode dateNode = XmlUtilities.Find(node, dateNodeName);
            if (dateNode != null)
            {
                string newName = dateNode.Name.Replace("Full", "");
                dateNode = XmlUtilities.ChangeType(dateNode, newName);
                DateTime d;
                if (dateNode.InnerText.Contains('/'))
                    d = DateTime.ParseExact(dateNode.InnerText, "d/M/yyyy", CultureInfo.InvariantCulture);
                else
                    d = DateTime.ParseExact(dateNode.InnerText, "d-MMM-yyyy", CultureInfo.InvariantCulture);
                return d;
            }
            return DateTime.MinValue;
        }

        /// <summary>Reformats the array node to the new format.</summary>
        /// <param name="xmlNode">The XML node.</param>
        private static double[] GetArray(XmlNode xmlNode, string childName)
        {
            XmlNode node = XmlUtilities.Find(xmlNode, childName);
            if (node != null)
            {
                string[] arrayValues = node.InnerText.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                List<double> values = new List<double>();
                for (int i = 0; i < arrayValues.Length; i++)
                    if (arrayValues[i] == "999999")
                        values.Add(double.NaN);
                    else
                        values.Add(System.Convert.ToDouble(arrayValues[i]));

                return values.ToArray();
            }
            return null;
        }

    }
}
