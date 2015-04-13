// -----------------------------------------------------------------------
// <copyright file="ApsimWriter.cs" company="CSIRO">
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

    /// <summary>Writes a .apsim file</summary>
    class APSIMFileWriter
    {
        /// <summary>The simulation XML</summary>
        private XmlNode simulationXML;
        /// <summary>The operations</summary>
        private List<string> operations = new List<string>();

        /// <summary>Initializes a new instance of the <see cref="APSIMFileWriter"/> class.</summary>
        public APSIMFileWriter()
        {
            // Load the template file.
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("APSIM.Cloud.Shared.Resources.Template.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(s);
            simulationXML = XmlUtilities.Find(doc.DocumentElement, "Base");
        }

        /// <summary>To the XML.</summary>
        /// <returns></returns>
        public XmlNode ToXML()
        {
            XmlNode operationsNode = XmlUtilities.Find(simulationXML, "Paddock/Operations");

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
            XmlUtilities.SetNameAttr(simulationXML, simulationName);
        }

        /// <summary>Sets the report date.</summary>
        /// <param name="reportDate">The date.</param>
        public void SetReportDate(DateTime reportDate)
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/DateReportWasGenerated", reportDate.ToString("yyyy-MM-dd"));
        }

        /// <summary>Sets the start end date.</summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        public void SetStartEndDate(DateTime startDate, DateTime endDate)
        {
            XmlUtilities.SetValue(simulationXML, "Clock/start_date", startDate.ToString("d/MM/yyyy"));
            XmlUtilities.SetValue(simulationXML, "Clock/end_date", endDate.ToString("d/MM/yyyy"));
        }

        /// <summary>Sets the weather file.</summary>
        /// <param name="weatherFileName">Name of the weather file.</param>
        public void SetWeatherFile(string weatherFileName)
        {
            XmlUtilities.SetValue(simulationXML, "Met/filename", weatherFileName);
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
            XmlUtilities.SetValue(simulationXML, "Paddock/SurfaceOM/Type", stubbleType);
            XmlUtilities.SetValue(simulationXML, "Paddock/SurfaceOM/Mass", stubbleMass.ToString());
            XmlUtilities.SetValue(simulationXML, "Paddock/SurfaceOM/cnr", cnratio.ToString());
        }

        /// <summary>Sets the soil.</summary>
        /// <param name="soil">The soil.</param>
        public void SetSoil(Soil soil)
        {
            XmlDocument soilDoc = new XmlDocument();
            soilDoc.LoadXml(SoilUtility.ToXML(soil));
            XmlNode paddockNode = XmlUtilities.Find(simulationXML, "Paddock");
            paddockNode.AppendChild(paddockNode.OwnerDocument.ImportNode(soilDoc.DocumentElement, true));
        }

        /// <summary>Write a sowing operation to the specified xml.</summary>
        /// <param name="sowing">The sowing.</param>
        /// <returns></returns>
        /// <exception cref="Exception">
        /// Maximum root depth should be specified in mm, not cm
        /// or
        /// Invalid bed width found:  + sowing.BedWidth.ToString()
        /// </exception>
        public string AddSowingOperation(Sow sowing, bool useEC)
        {
            string operationsXML = string.Empty;

            if (sowing.Date != DateTime.MinValue)
            {
                XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/CropName", sowing.Crop);

                string cropNodePath = "Paddock/" + sowing.Crop;

                string useECValue = "no";
                if (useEC)
                    useECValue = "yes";
                XmlUtilities.SetValue(simulationXML, cropNodePath + "/ModifyKL", useECValue);

                // Maximum rooting depth.
                if (sowing.MaxRootDepth > 0 && sowing.MaxRootDepth < 20)
                    throw new Exception("Maximum root depth should be specified in mm, not cm");
                if (sowing.MaxRootDepth > 0)
                    XmlUtilities.SetValue(simulationXML, cropNodePath + "/MaxRootDepth", sowing.MaxRootDepth.ToString());

                // Make sure we have a row spacing.
                if (sowing.RowSpacing == 0)
                    sowing.RowSpacing = 250;

                string sowAction = sowing.Crop + " sow plants = " + sowing.SowingDensity.ToString() +
                                   ", sowing_depth = 30" +
                                   ", cultivar = " + sowing.Cultivar +
                                   ", row_spacing = " + sowing.RowSpacing.ToString() +
                                   ", crop_class = plant" +
                                   ", skip = solid";

                // Allan's furrow irrigation hack.
                if (sowing.BedWidth > 0)
                {
                    double skiprow;
                    if (sowing.BedWidth == 1)
                        skiprow = 0.44;
                    else if (sowing.BedWidth == 2)
                        skiprow = 0.2;
                    else
                        throw new Exception("Invalid bed width found: " + sowing.BedWidth.ToString());

                    sowAction += ", skiprow = " + skiprow.ToString();
                }

                AddOperation(sowing.Date, sowAction);

                // Add a sowing tillage operation
                string tillageAction = "SurfaceOM tillage type = planter, f_incorp = 0.1, tillage_depth = 50";
                AddOperation(sowing.Date, tillageAction);

                // see if an emergence date was specified. If so then write some operations to 
                // specify it and the germination date.
                if (sowing.EmergenceDate != DateTime.MinValue)
                {
                    TimeSpan FiveDays = new TimeSpan(5, 0, 0, 0);
                    TimeSpan OneDay = new TimeSpan(1, 0, 0, 0);
                    DateTime GerminationDate = sowing.EmergenceDate - FiveDays;
                    if (GerminationDate <= sowing.Date)
                        GerminationDate = sowing.Date + OneDay;
                    if (sowing.EmergenceDate <= GerminationDate)
                        sowing.EmergenceDate = GerminationDate + OneDay;
                    int DaysToGermination = (GerminationDate - sowing.Date).Days;
                    int DaysToEmergence = (sowing.EmergenceDate - sowing.Date).Days;
                    AddOperation(sowing.Date, sowing.Crop + " set DaysToGermination = " + DaysToGermination.ToString());
                    AddOperation(sowing.Date, sowing.Crop + " set DaysToEmergence = " + DaysToEmergence.ToString());
                }

                if (sowing.IrrigationAmount > 0)
                {
                    AddOperation(sowing.Date, "Irrigation apply amount = " + sowing.IrrigationAmount.ToString());
                }
            }

            return operationsXML;
        }

        /// <summary>Add a new operation to the specified operations node.</summary>
        /// <param name="date">The date.</param>
        /// <param name="action">The action.</param>
        private void AddOperation(DateTime date, string action)
        {
            operations.Add("<operation condition=\"start_of_day\">\r\n" +
                           "   <date>" + date.ToString("dd-MMM") + "</date>\r\n" +
                           "   <action>" + action + "</action>\r\n" +
                           "</operation>\r\n");
        }

        /// <summary>Adds a fertilse operation.</summary>
        /// <param name="application">The application.</param>
        public void AddFertilseOperation(Fertilise application)
        {
            if (application.Scenario)
                AddOperation(application.Date, "act_mods ScenarioOperation");

            string action = "Fertiliser apply amount = " + application.Amount.ToString("F0") + " (kg/ha)" +
                                           ", depth = 20 (mm), type = no3_n";
            AddOperation(application.Date, action);
        }

        /// <summary>Adds a irrigation operation.</summary>
        /// <param name="application">The application.</param>
        public void AddIrrigateOperation(Irrigate application)
        {
            if (application.Scenario)
                AddOperation(application.Date, "act_mods ScenarioOperation");

            string action = "Irrigation apply amount = " + application.Amount.ToString("F0") + " (mm)" +
                                         "efficiency = " + application.Efficiency.ToString("F2") + "(0-1)";
            AddOperation(application.Date, action); AddOperation(application.Date, action);
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
            string action = "SurfaceOM tillage type = user_defined" +
                            ", f_incorp = " + incorpFOM.ToString("F1") +
                            ", tillage_depth = 100";
            AddOperation(application.Date, action);
        }
        /// <summary>Adds a stubble removed operation.</summary>
        /// <param name="application">The application.</param>
        public void AddStubbleRemovedOperation(StubbleRemoved application)
        {
            double incorpFOM = application.Percent / 100;

            string action = "SurfaceOM tillage type = user_defined" +
                            ", f_incorp = " + incorpFOM.ToString("F1") +
                            ", tillage_depth = 0";

            AddOperation(application.Date, action);
        }

        /// <summary>Adds the reset water operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddResetWaterOperation(ResetWater reset)
        {
            AddOperation(reset.Date, "'Soil Water' reset");
            AddOperation(reset.Date, "act_mods reseting");
        }

        /// <summary>Adds the reset nitrogen operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddResetNitrogenOperation(ResetNitrogen reset)
        {
            AddOperation(reset.Date, "'Soil Nitrogen' reset");
        }

        /// <summary>Adds the surface organic matter operation.</summary>
        /// <param name="reset">The reset.</param>
        public void AddSurfaceOrganicMatterOperation(ResetSurfaceOrganicMatter reset)
        {
            AddOperation(reset.Date, "SurfaceOM reset");
        }

        /// <summary>Creates a met factorial.</summary>
        /// <param name="simulationXML">The top level simulation node</param>
        /// <param name="paddockName">The name of the paddock.</param>
        /// <param name="rainFileName">Name of the rain file.</param>
        /// <param name="weatherFileNames">The weather file names.</param>
        public static void CreateMetFactorial(XmlNode simulationXML, string paddockName, string rainFileName, string[] weatherFileNames)
        {
            // Make sure <factorial> exists.
            XmlNode factorial = XmlUtilities.Find(simulationXML, "Factorials");
            if (factorial == null)
                factorial = simulationXML.AppendChild(simulationXML.OwnerDocument.CreateElement("factorial"));
            XmlUtilities.SetNameAttr(factorial, "Factorials");
            XmlUtilities.SetValue(factorial, "active", "1");

            // Add a <factor> for Met
            XmlNode metFactor = factorial.AppendChild(factorial.OwnerDocument.CreateElement("factor"));
            XmlUtilities.SetNameAttr(metFactor, "Met");

            // Set <targets> in met factor
            XmlUtilities.SetValue(metFactor, "targets/Target", "/Simulations/" + paddockName + "/Met");

            // Set <vars> in met factor
            string weatherFilesCSV = StringUtilities.BuildString(weatherFileNames, ",");
            XmlUtilities.SetValue(metFactor, "vars/filename", weatherFilesCSV);

            // Set <metfile> in met factor
            XmlNode met = metFactor.AppendChild(metFactor.OwnerDocument.CreateElement("metfile"));
            XmlUtilities.SetNameAttr(met, "Met");
            XmlUtilities.SetValue(met, "filename", "");
        }

        /// <summary>Sets the n unlimited.</summary>
        /// <param name="simulationXML">The top level simulation node</param>
        public void SetNUnlimited()
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/NUnlimited", "yes");
        }

        /// <summary>Sets the n unlimited from today</summary>
        /// <param name="simulationXML">The top level simulation node</param>
        public void SetNUnlimitedFromToday()
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/NUnlimitedFromToday", "yes");
        }

        /// <summary>Sets the daily output.</summary>
        public void SetDailyOutput()
        {
            XmlUtilities.SetAttribute(simulationXML, "Paddock/Daily/enabled", "yes");
        }

        /// <summary>Sets the monthly output.</summary>
        public void SetMonthlyOutput()
        {
            XmlUtilities.SetAttribute(simulationXML, "Paddock/Monthly/enabled", "yes");
        }

        /// <summary>Sets the yearly output.</summary>
        public void SetYearlyOutput()
        {
            XmlUtilities.SetAttribute(simulationXML, "Paddock/Yearly/enabled", "yes");

        }

        /// <summary>Writes the depth file.</summary>
        public void WriteDepthFile()
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/WriteDepthFile", "yes");
        }

        /// <summary>Sets the next 10 days (from the report date) to be dry (no rain).</summary>
        public void Next10DaysDry()
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Management/ui/DryNext10Days", "yes");
            XmlUtilities.SetAttribute(simulationXML, "Paddock/Yearly/enabled", "no");
            SetDailyOutput();
            XmlUtilities.SetValue(simulationXML, "Paddock/Daily/Reporting Frequency/event", "Next10Days");
        }

        /// <summary>
        /// Sets the erosion parameters
        /// </summary>
        /// <param name="slope">The slope (%).</param>
        /// <param name="slopeLength">The slope length (m).</param>
        public void SetErosion(double slope, double slopeLength)
        {
            XmlUtilities.SetValue(simulationXML, "Paddock/Erosion/slope", slope.ToString("F0"));
            XmlUtilities.SetValue(simulationXML, "Paddock/Erosion/slope_length", slopeLength.ToString("F0"));
        }
    }
}
