// -----------------------------------------------------------------------
// <copyright file="APSIMxFileWriter.cs" company="CSIRO">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Xml;
    using APSIM.Shared.Soils;
    using APSIM.Shared.Utilities;

    /// <summary>Writes a .apsimx file</summary>
    class APSIMxFileWriter : IAPSIMFileWriter
    {
        /// <summary>The simulation XML</summary>
        private XmlNode simulationXML;
        /// <summary>The operations</summary>
        private List<string> operations = new List<string>();

        /// <summary>The crop being sown</summary>
        private string cropBeingSown;

        /// <summary>Initializes a new instance of the <see cref="APSIMFileWriter"/> class.</summary>
        public APSIMxFileWriter()
        {
            // Load the template file.
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Shared.Resources.Template.apsimx");

            XmlDocument doc = new XmlDocument(); 
            doc.Load(s);
            simulationXML = XmlUtilities.Find(doc.DocumentElement, "Simulation");
        }

        /// <summary>To the XML.</summary>
        /// <returns></returns>
        public XmlNode ToXML()
        {
            XmlNode operationsNode = XmlUtilities.Find(simulationXML, "Zone/Operations");

            string operationsXML = string.Empty;
            foreach (string operation in operations)
                operationsXML += operation + "\r\n";

            operationsNode.InnerXml = operationsXML;
            return simulationXML;
        }
        
        /// <summary>Names the simulation.</summary>
        /// <param name="simulationName">Name of the simulation.</param>
        public void NameSimulation(string simulationName)
        {
            XmlUtilities.SetValue(simulationXML, "Name", simulationName);
        }

        /// <summary>Sets the report date.</summary>
        /// <param name="reportDate">The date.</param>
        public void SetReportDate(DateTime reportDate)
        {
            
        }

        /// <summary>Sets the start end date.</summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        public void SetStartEndDate(DateTime startDate, DateTime endDate)
        {
            XmlUtilities.SetValue(simulationXML, "Clock/StartDate", startDate.ToString("yyyy-MM-dd"));
            XmlUtilities.SetValue(simulationXML, "Clock/EndDate", endDate.ToString("yyyy-MM-dd"));
        }

        /// <summary>Sets the weather file.</summary>
        /// <param name="weatherFileName">Name of the weather file.</param>
        public void SetWeatherFile(string weatherFileName)
        {
            XmlUtilities.SetValue(simulationXML, "Weather/FileName", weatherFileName);
        }

        /// <summary>Sets the stubble.</summary>
        /// <param name="stubbleType">Type of the stubble.</param>
        /// <param name="stubbleMass">The stubble mass.</param>
        /// <param name="cnratio">The cnratio.</param>
        /// <exception cref="Exception">No stubble type specified</exception>
        public void SetStubble(string stubbleType, double stubbleMass, int cnratio)
        {
            // Stubble.
            if (stubbleType == null || stubbleType == "None")
                throw new Exception("No stubble type specified");
            XmlUtilities.SetValue(simulationXML, "Zone/SurfaceOrganicMatter/ResidueType", stubbleType);
            XmlUtilities.SetValue(simulationXML, "Zone/SurfaceOrganicMatter/Mass", stubbleMass.ToString());
            XmlUtilities.SetValue(simulationXML, "Zone/SurfaceOrganicMatter/CNRatio", cnratio.ToString());
        }

        /// <summary>Sets the soil.</summary>
        /// <param name="soil">The soil.</param>
        public void SetSoil(Soil soil)
        {
            XmlDocument soilDoc = new XmlDocument();
            soilDoc.LoadXml(SoilUtilities.ToXML(soil));

            // Name soil crop models
            foreach (XmlNode soilcrop in XmlUtilities.FindAllRecursivelyByType(soilDoc.DocumentElement, "SoilCrop"))
                XmlUtilities.SetValue(soilcrop, "Name", XmlUtilities.Attribute(soilcrop, "name") + "Soil");

            // Name soil samples
            foreach (XmlNode sample in XmlUtilities.FindAllRecursivelyByType(soilDoc.DocumentElement, "Sample"))
                XmlUtilities.SetValue(sample, "Name", XmlUtilities.Attribute(sample, "name"));

            XmlNode paddockNode = XmlUtilities.Find(simulationXML, "Zone");
            XmlUtilities.EnsureNodeExists(soilDoc.DocumentElement, "SoilNitrogen");
            soilDoc.DocumentElement.RemoveChild(XmlUtilities.Find(soilDoc.DocumentElement, "SoilTemperature"));
            paddockNode.AppendChild(paddockNode.OwnerDocument.ImportNode(soilDoc.DocumentElement, true));
        }

        /// <summary>Write a sowing operation to the specified xml.</summary>
        /// <param name="sowing">The sowing.</param>
        public string AddSowingOperation(Sow sowing, bool useEC)
        {
            string operationsXML = string.Empty;

            if (sowing.Date != DateTime.MinValue)
            {
                cropBeingSown = sowing.Crop;
                string cropNodePath = "Zone/" + sowing.Crop;

                //string useECValue = "no";
                //if (useEC)
                //    useECValue = "yes";
                //XmlUtilities.SetValue(simulationXML, cropNodePath + "/ModifyKL", useECValue);

                // Maximum rooting depth.
                if (sowing.MaxRootDepth > 0 && sowing.MaxRootDepth < 20)
                    throw new Exception("Maximum root depth should be specified in mm, not cm");
                if (sowing.MaxRootDepth > 0)
                    AddOperation(sowing.Date, "[" + sowing.Crop + "].Root.MaximumRootDepth.FixedValue = " + sowing.MaxRootDepth);

                // Make sure we have a row spacing.
                if (sowing.RowSpacing == 0)
                    sowing.RowSpacing = 250;
                if (sowing.SeedDepth == 0)
                    sowing.SeedDepth = 50;

                string sowAction = "[" + sowing.Crop + "].Sow(population: " + sowing.SowingDensity.ToString() +
                                   ", cultivar: " + sowing.Cultivar +
                                   ", depth: " + sowing.SeedDepth +
                                   ", rowSpacing: " + sowing.RowSpacing.ToString() + ")";

                // Allan's furrow irrigation hack.
                //if (sowing.BedWidth > 0)
                //{
                //    double skiprow;
                //    if (sowing.BedWidth == 1)
                //        skiprow = 0.44;
                //    else if (sowing.BedWidth == 2)
                //        skiprow = 0.2;
                //    else
                //        throw new Exception("Invalid bed width found: " + sowing.BedWidth.ToString());

                //    sowAction += ", skiprow = " + skiprow.ToString();
                //}

                AddOperation(sowing.Date, sowAction);

                // Add a sowing tillage operation
                string tillageAction = "[SurfaceOrganicMatter].Incorporate(fraction: 0.1, depth: 50)";
                AddOperation(sowing.Date, tillageAction);

                // see if an emergence date was specified. If so then write some operations to 
                // specify it and the germination date.
                if (sowing.EmergenceDate != DateTime.MinValue)
                {
                    DateTime GerminationDate = sowing.EmergenceDate.AddDays(-5);
                    if (GerminationDate <= sowing.Date)
                        GerminationDate = sowing.Date.AddDays(1);
                    if (sowing.EmergenceDate <= GerminationDate)
                        sowing.EmergenceDate = GerminationDate.AddDays(1);
                    int DaysToGermination = (GerminationDate - sowing.Date).Days;
                    int DaysToEmergence = (sowing.EmergenceDate - sowing.Date).Days;
                    AddOperation(sowing.Date, "[" + sowing.Crop + "].Phenology.Germinating.DaysFromSowingToEndPhase = " + DaysToGermination);
                    AddOperation(sowing.Date, "[" + sowing.Crop + "].Phenology.Emerging.DaysFromSowingToEndPhase = " + DaysToEmergence);
                }

                if (sowing.IrrigationAmount > 0)
                    AddOperation(sowing.Date, "[Irrigation].Apply(amount: " + sowing.IrrigationAmount.ToString() + ")");

                // Add a crop node.
                XmlNode cropNode = XmlUtilities.EnsureNodeExists(simulationXML, "Zone/Plant");
                XmlUtilities.SetValue(cropNode, "ResourceName", sowing.Crop);
                XmlUtilities.SetValue(cropNode, "Name", sowing.Crop);
                XmlUtilities.SetValue(cropNode, "CropType", sowing.Crop);

                XmlUtilities.SetValue(simulationXML, "Zone/Manager/Script/CropName", sowing.Crop);
                //if (sowing.Crop == "Wheat")
                //    XmlUtilities.SetAttribute(simulationXML, "Paddock/WheatFrostHeat/enabled", "yes");
                //if (sowing.Crop == "Canola")
                //    XmlUtilities.SetAttribute(simulationXML, "Paddock/CanolaFrostHeat/enabled", "yes");
            } 

            return operationsXML;
        }

        /// <summary>Add a new operation to the specified operations node.</summary>
        /// <param name="date">The date.</param>
        /// <param name="action">The action.</param>
        private void AddOperation(DateTime date, string action)
        {
            operations.Add("<Operation>\r\n" +
                           "   <Date>" + date.ToString("yyyy-MM-dd") + "</Date>\r\n" +
                           "   <Action>" + action + "</Action>\r\n" +
                           "</Operation>\r\n");
        }

        /// <summary>Adds a fertilse operation.</summary>
        /// <param name="application">The application.</param>
        public void AddFertilseOperation(Fertilise application)
        {
            string action = "[Fertiliser].Apply(Amount: " + application.Amount.ToString("F0") +
                                                ", Depth: 20, Type: Fertiliser.Types.NO3N)";
            AddOperation(application.Date, action);
        }

        /// <summary>Adds a irrigation operation.</summary>
        /// <param name="application">The application.</param>
        public void AddIrrigateOperation(Irrigate application)
        {
            string action = "[Irrigation].Apply(amount: " + application.Amount.ToString("F0") +
                            ",efficiency: " + application.Efficiency.ToString("F2") + ")";
            AddOperation(application.Date, action);
        }

        /// <summary>Adds a tillage operation.</summary>
        /// <param name="application">The application.</param>
        public void AddTillageOperation(Tillage application)
        {
            double incorpFOM;
            if (application.Disturbance == Tillage.DisturbanceEnum.Low)
                incorpFOM = 0.2;
            else if (application.Disturbance == Tillage.DisturbanceEnum.Medium)
                incorpFOM = 0.5;
            else
                incorpFOM = 0.8;
            string action = "[SurfaceOrganicMatter].Incorporate(fraction:" + incorpFOM.ToString("F1") + ", depth: 100)";
            AddOperation(application.Date, action);
        }
        /// <summary>Adds a stubble removed operation.</summary>
        /// <param name="application">The application.</param>
        public void AddStubbleRemovedOperation(StubbleRemoved application)
        {
            double incorpFOM = application.Percent / 100;

            string action = "[SurfaceOrganicMatter].Incorporate(fraction: " + incorpFOM.ToString("F1") + ", depth: 0)";

            AddOperation(application.Date, action);
        }

        /// <summary>Adds the reset water operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddResetWaterOperation(ResetWater reset)
        {
            AddOperation(reset.Date, "[Soil].SoilWater.Reset()");
        }

        /// <summary>Adds the reset nitrogen operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddResetNitrogenOperation(ResetNitrogen reset)
        {
            AddOperation(reset.Date, "[Soil].SoilNitrogen.Reset()");
        }

        /// <summary>Adds the surface organic matter operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddSurfaceOrganicMatterOperation(ResetSurfaceOrganicMatter reset)
        {
            AddOperation(reset.Date, "[SurfaceOrganicMatter].Reset()");
        }

        /// <summary>Creates a met factorial.</summary>
        /// <param name="factor">The factor</param>
        public static void ApplyFactor(XmlNode simulationXML, APSIMSpecification.Factor factor)
        {
            
        }

        /// <summary>Sets the n unlimited.</summary>
        /// <param name="simulationXML">The top level simulation node</param>
        public void SetNUnlimited()
        {
            //XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/NUnlimited", "yes");
        }

        /// <summary>Sets the n unlimited from today</summary>
        /// <param name="simulationXML">The top level simulation node</param>
        public void SetNUnlimitedFromToday()
        {
           // XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/NUnlimitedFromToday", "yes");
        }

        /// <summary>Sets the daily output.</summary>
        public void SetDailyOutput()
        {
            List<XmlNode> reports = new List<XmlNode>();
            XmlUtilities.FindAllRecursively(simulationXML, "Report", ref reports);
            foreach (XmlNode report in reports)
                if (XmlUtilities.Value(report, "Name") == "Daily")
                    XmlUtilities.SetValue(report, "EventNames/string", "[Clock].DoReport");
        }

        /// <summary>Sets the monthly output.</summary>
        public void SetMonthlyOutput()
        {
            List<XmlNode> reports = new List<XmlNode>();
            XmlUtilities.FindAllRecursively(simulationXML, "Report", ref reports);
            foreach (XmlNode report in reports)
                if (XmlUtilities.Value(report, "Name") == "Monthly")
                    XmlUtilities.SetValue(report, "EventNames/string", "[Clock].EndOfMonth");
        }

        /// <summary>Sets the yearly output.</summary>
        public void SetYearlyOutput()
        {
            List<XmlNode> reports = new List<XmlNode>();
            XmlUtilities.FindAllRecursively(simulationXML, "Report", ref reports);
            foreach (XmlNode report in reports)
                if (XmlUtilities.Value(report, "Name") == "Yearly")
                    XmlUtilities.SetValue(report, "EventNames/string", "[" + cropBeingSown + "].Harvesting");
        }

        /// <summary>Writes the depth file.</summary>
        public void WriteDepthFile()
        {
            //XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/WriteDepthFile", "yes");
        }

        /// <summary>Sets the next 10 days (from the report date) to be dry (no rain).</summary>
        public void Next10DaysDry()
        {
            //XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/DryNext10Days", "yes");
            //XmlUtilities.SetAttribute(simulationXML, "Paddock/Yearly/enabled", "no");
            //SetDailyOutput();
            //XmlUtilities.SetValue(simulationXML, "Paddock/Daily/Reporting Frequency/event", "Next10Days");
        }

        /// <summary>
        /// Sets the erosion parameters
        /// </summary>
        /// <param name="slope">The slope (%).</param>
        /// <param name="slopeLength">The slope length (m).</param>
        public void SetErosion(double slope, double slopeLength)
        {
            //XmlUtilities.SetValue(simulationXML, "Paddock/Erosion/slope", slope.ToString("F0"));
            //XmlUtilities.SetValue(simulationXML, "Paddock/Erosion/slope_length", slopeLength.ToString("F0"));
        }
    }
}
