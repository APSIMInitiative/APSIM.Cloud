// -----------------------------------------------------------------------
// <copyright file="AusFarmFileWriter.cs" company="CSIRO">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Shared.AusFarm
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Xml;
    using System.Text;
    using APSIM.Shared.Soils;
    using APSIM.Shared.Utilities;
    using CMPServices;

    internal class OrgMatter
    {
        public List<string> names;
        public List<string> types;
        public List<string> masses;
        public List<string> cnrs;
        public List<string> stand_fract;
        public OrgMatter()
        {
            names = new List<string>();
            types = new List<string>();
            masses = new List<string>();
            cnrs = new List<string>();
            stand_fract = new List<string>();
        }
        public void Remove(int idx)
        {
            names.RemoveAt(idx);
            types.RemoveAt(idx);
            masses.RemoveAt(idx);
            cnrs.RemoveAt(idx);
            stand_fract.RemoveAt(idx);
        }
    }

    /// <summary>
    /// This class uses the specified AusFarm SDML script and modifies it 
    /// into a runnable script with all the settings transferred from 
    /// an AusFarmSpec object.
    /// </summary>
    public class AusFarmFileWriter
    {
        #region privates

        /// <summary>
        /// Names of crops - crop/plant components. This does not include names of pasture types.
        /// </summary>
        private string[] ValidCropNames = { "wheat", "canola", "barley", "chickpea", "oats", "fieldpea", "fababean", "lupin" };
        /// <summary>
        /// The simulation XML node
        /// </summary>
        private XmlNode simulationXMLNode;
        private TCompParser xmlScriptDoc;

        /// <summary>
        /// Set the state variable referred to in the Generic component.
        /// </summary>
        /// <param name="compName"></param>
        /// <param name="varName"></param>
        /// <param name="value"></param>
        private void SetGenericCompStateVar(string compName, string varName, string value)
        {
            XmlNode compNode = FindComponentByPathName(simulationXMLNode, "Params");
            TSDMLValue init = GetTypedInit(compNode, "state_vars");
            uint i = 1;
            TTypedValue stateVar = null;
            while ((stateVar == null) && (i <= init.count()))
            {
                if (init.item(i).member("name").asStr() == varName)
                {
                    stateVar = init.item(i);
                }
                i++;
            }
            if (stateVar != null)
            {
                stateVar.member("value").setValue(value);
                SetTypedInit(compNode, "state_vars", init);
            }
        }

        /// <summary>
        /// Find a component using a fully qualified path
        /// </summary>
        /// <param name="parentNode"></param>
        /// <param name="compName">A path by component Names</param>
        /// <returns></returns>
        private XmlNode FindComponentByPathName(XmlNode parentNode, string pathName)
        {
            XmlNode childNode = XmlUtilities.Find(simulationXMLNode, pathName);
            return childNode;
        }

        /// <summary>
        /// Set a scalar value in the init section of a component.
        /// Handles the init sections of APSIM and AusFarm components.
        /// </summary>
        /// <param name="compNode">XmlNode for the component found</param>
        /// <param name="varName">Name of the variable. APSIM - xml tag, AusFarm - init name</param>
        /// <param name="value">The string value</param>
        private void SetValue(XmlNode compNode, string varName, string value)
        {
            if (compNode != null)
            {
                TCompParser comp = new TCompParser(compNode.OuterXml);
                XmlNode initdata = compNode.SelectSingleNode("initdata");
                string newInitDataSection;
                if (comp.IsAPSRU())
                {
                    //edit the node text for varName
                }
                else
                {
                    //AusFarm inits
                    string cdata = comp.initData();
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(cdata);
                    XmlNode initSection = doc.DocumentElement;
                    XmlNode val = initSection.SelectSingleNode("/initsection/init[attribute::name=\"" + varName + "\"]/val");
                    if (val != null)
                        val.InnerText = value;
                    newInitDataSection = initSection.OuterXml;
                    initdata.InnerXml = "<![CDATA[\n" + newInitDataSection + "\n]]>";
                }
            }
        }

        /// <summary>
        /// Set a double in an init section.
        /// </summary>
        /// <param name="compNode"></param>
        /// <param name="varName"></param>
        /// <param name="value"></param>
        private void SetValue(XmlNode compNode, string varName, double value)
        {
            SetValue(compNode, varName, value.ToString());
        }

        private void SetTypedValue(XmlNode compNode, string varName, string value)
        {
            TSDMLValue init = null;
            if (compNode != null)
            {
                TCompParser comp = new TCompParser(compNode.OuterXml);
                //AusFarm inits
                string sdml = comp.initTextByName(varName);
                if (sdml.Length > 0)
                {
                    init = new TSDMLValue(sdml, "");
                }

                init.setValue(value);

                StringBuilder newInitSection = new StringBuilder();
                newInitSection.Append("<initsection>");
                //AusFarm inits
                //find the init node in the section
                uint i = 1;
                while (i <= comp.initCount())
                {
                    if (comp.initName(i) == varName)
                    {
                        string initStr = init.getText(init, 0, 2);
                        initStr = initStr.Replace("&#39;", "'");    //replace any escaped single quotes
                        newInitSection.Append(initStr);
                    }
                    else
                        newInitSection.Append(comp.initText(i));
                    i++;
                }
                newInitSection.Append("</initsection>");
                XmlNode initdata = compNode.SelectSingleNode("initdata");
                initdata.InnerXml = "<![CDATA[" + newInitSection.ToString() + "]]>";
            }
        }

        /// <summary>
        /// Get the init sdml property from an AusFarm component's init section.
        /// </summary>
        /// <param name="compNode">The xml node for the component</param>
        /// <param name="varName">Name of the init value</param>
        /// <returns>An TSDMLValue</returns>
        private TSDMLValue GetTypedInit(XmlNode compNode, string varName)
        {
            TSDMLValue init = null;
            if (compNode != null)
            {
                TCompParser comp = new TCompParser(compNode.OuterXml);
                if (!comp.IsAPSRU())
                {
                    //AusFarm inits
                    string sdml = comp.initTextByName(varName);
                    if (sdml.Length > 0)
                    {
                        init = new TSDMLValue(sdml, "");
                    }
                }
            }
            return init;
        }

        /// <summary>
        /// Set the init value in the document using the TSDMLValue passed in.
        /// </summary>
        /// <param name="compNode">Component node in the document</param>
        /// <param name="varName">Name of the variable in the AusFarm init section</param>
        /// <param name="init">The TTypedValue with the required settings</param>
        private void SetTypedInit(XmlNode compNode, string varName, TSDMLValue init)
        {
            if (compNode != null)
            {
                TCompParser comp = new TCompParser(compNode.OuterXml);
                if (!comp.IsAPSRU())
                {
                    StringBuilder newInitSection = new StringBuilder();
                    newInitSection.Append("<initsection>");
                    //AusFarm inits
                    //find the init node in the section
                    uint i = 1;
                    while (i <= comp.initCount())
                    {
                        if (comp.initName(i) == varName)
                        {
                            string initStr = init.getText(init, 0, 2);
                            initStr = initStr.Replace("&#39;", "'");    //replace any escaped single quotes
                            newInitSection.Append(initStr);
                        }
                        else
                            newInitSection.Append(comp.initText(i));
                        i++;
                    }
                    newInitSection.Append("</initsection>");
                    XmlNode initdata = compNode.SelectSingleNode("initdata");
                    initdata.InnerXml = "<![CDATA[" + newInitSection.ToString() + "]]>";
                }
            }
        }

        /// <summary>
        /// Convert a string list of items in a List<string>
        /// </summary>
        /// <param name="strList">List of string items</param>
        /// <param name="list">Space seperated items</param>
        private void strToArray(ref List<string> strList, string list)
        {
            list = list.Trim() + " ";
            int i = 0;
            int start = 0;
            while (i < list.Length)
            {
                if (list[i] == ' ')
                {
                    strList.Add(list.Substring(start, i - start));
                    i++;
                    start = i;
                }
                while ((i < list.Length) && (list[i] == ' '))
                {
                    i++;
                    start++;
                }
                i++;
            }
        }

        /// <summary>
        /// Convert and array into a string of space delimited values
        /// </summary>
        /// <param name="values"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        private string arrayAsStr(double[] values, string format = "")
        {
            string result = "";
            bool formatted = (format.Length > 0);

            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    result += "   ";
                if (formatted)
                    result += values[i].ToString(format);
                else
                    result += values[i].ToString();
            }
            return result;
        }

        private const double NAN = 5.0e-33;

        /// <summary>
        /// Convert a double to a string with checking for NAN 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private string asApsimStr(double value, string format = "")
        {
            if (Math.Abs(value - NAN) > 1.0e-30)
            {
                if (format.Length > 0)
                    return value.ToString(format);
                else
                    return Convert.ToString(value);
            }
            else
                return "";
        }

        #endregion
        /// <summary>
        /// Initializes a new instance of the <see cref="APSIMFileWriter"/> class.
        /// This object is used to write the fields to the SDML xml file
        /// passed from an AusFarmSpec object via the AusFarmFiles host.
        /// </summary>
        public AusFarmFileWriter(SimulationType templateType)
        {
            // Load the template file.
            string scriptTemplate = "";
            switch (templateType)
            {
                case SimulationType.stCropOnly:
                    scriptTemplate = "APSIM.Cloud.Shared.Resources.ausfarm_crop_only.sdml";
                    break;
                case SimulationType.stSingleFlock:
                    scriptTemplate = "APSIM.Cloud.Shared.Resources.ausfarm_warooka.sdml";
                    break;
            }

            if (scriptTemplate.Length > 0)
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(scriptTemplate);
                StreamReader reader = new StreamReader(s);
                xmlScriptDoc = new TCompParser(reader.ReadToEnd());
                simulationXMLNode = xmlScriptDoc.rootNode();
            }
            else
                simulationXMLNode = null;
        }

        /// <summary>Returns the XML node of the simulation sdml file.
        /// Normally used after all the modifications have been done.</summary>
        /// <returns>The root XML node</returns>
        public XmlNode ToXML()
        {
            //do any extra additions to the script here...
            if (simulationXMLNode != null)
            {

            }

            return simulationXMLNode;
        }

        /// <summary>Names the simulation.</summary>
        /// <param name="simulationName">Name of the simulation.</param>
        public void NameSimulation(string simulationName)
        {
            XmlUtilities.SetNameAttr(simulationXMLNode, simulationName);
        }

        /// <summary>
        /// Set the whole farm area
        /// </summary>
        /// <param name="area"></param>
        public void SetArea(double area)
        {
            SetGenericCompStateVar("Params", "F4P_AREA", area.ToString());
        }

        /// <summary>
        /// Set the path for the output files. 
        /// </summary>
        /// <param name="outPath">Includes terminating path delimiter</param>
        public void OutputPath(string outPath)
        {
            SetGenericCompStateVar("Params", "F4P_OUTPATH", "'" + outPath + "'");
        }

        /// <summary>
        /// Sets the prefix for the named output files.
        /// </summary>
        /// <param name="reportName"></param>
        public void ReportName(string reportName)
        {
            SetGenericCompStateVar("Params", "F4P_OUTPREFIX", "'" + reportName + "'");
        }

        /// <summary>Sets the start end date.</summary>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        public void SetStartEndDate(DateTime startDate, DateTime endDate)
        {
            XmlNode compNode = FindComponentByPathName(simulationXMLNode, "time_server");
            SetValue(compNode, "start", startDate.ToString("yyyy/MM/d"));
            SetValue(compNode, "finish", endDate.ToString("yyyy/MM/d"));
        }

        /// <summary>Sets the weather file.</summary>
        /// <param name="weatherFileName">Name of the weather file.</param>
        public void SetWeatherFile(string weatherFileName)
        {
            XmlNode compNode = FindComponentByPathName(simulationXMLNode, "Weather");
            SetValue(compNode, "filename", weatherFileName);
            SetValue(compNode, "source", "silo");
        }

        /// <summary>
        /// Construct a series of decile strings [59,  86,  110, 139, 167, 186, 197] 
        /// where the items are values for each month of the year
        /// </summary>
        /// <param name="deciles">The monthly decile values</param>
        public void SetRainDeciles(double[,] deciles)
        {
            StringBuilder decileValues = new StringBuilder();
            for (int dec = 1; dec <= 10; dec++)
            {
                decileValues.Clear();
                decileValues.Append("[" + deciles[0, dec-1].ToString());
                for (int month = 2; month <= 12; month++)
                {
                    decileValues.Append("," +deciles[month-1, dec-1].ToString());
                }
                decileValues.Append("]");
                SetGenericCompStateVar("Params", "F4P_DECILE" + (10*dec).ToString(), decileValues.ToString());    
            }
        }

        /// <summary>
        /// Set the crop rotation list of crop names for the soil type.
        /// </summary>
        /// <param name="index">The crop rotation number (soil type). 1-3</param>
        /// <param name="crops">List of crop names for the rotation.</param>
        public void SetCropRotation(int index, List<CropSpec> crops)
        {
            if ((index > 0) && (index <= 3))
            {
                string rotationVariable = "F4P_CROP_ROT" + index.ToString();
                string isCrop = "F4P_ISCROP_ROT" + index.ToString();

                // crop name array
                string cropArrayStr = "[";
                for (int s = 0; s < crops.Count; s++)
                {
                    cropArrayStr += "'" + crops[s].name + "'";
                    if (s < crops.Count - 1)
                        cropArrayStr += ", ";
                }
                cropArrayStr += "]";
                SetGenericCompStateVar("Params", rotationVariable, cropArrayStr);

                // set the array of isCrop flags
                string isCropArrayStr = "[";
                for (int s = 0; s < crops.Count; s++)
                {
                    isCropArrayStr += (crops[s].isCrop == true ? "true": "false") ;
                    if (s < crops.Count - 1)
                        isCropArrayStr += ", ";
                }
                isCropArrayStr += "]";
                SetGenericCompStateVar("Params", isCrop, isCropArrayStr);
            }
        }

        /// <summary>
        /// Sets the soil values in every paddock that has the soil type.
        /// The valid configuration is:
        /// - One to three soil types. Currently there are four phases/paddocks for each soil type.
        ///   This means that up to four different crops will be sown on a soil type in any year.
        ///   To sow only one crop type on a soil type for the year, specifiy four crops with the same name.
        ///   More than four names can be in the rotation list. 
        ///   Each paddock in the soil type uses the same paddock initialisation
        ///   The .AreaPoportion value is useful to dividing the farm. Any soil type that has
        ///   zero proportion will have no area and will not be sown.
        ///   If only one soil type is found then the remaining two will be set to zero proportion area.
        /// </summary>
        /// <param name="simulation">The AusFarm simulation object</param>
        public void SetSoils(AusFarmSpec simulation)
        {
            double areaPropnTotal = 0.0;
            int soilIdx = 0;

            // find every paddock
            XmlNodeList paddocks = simulationXMLNode.SelectNodes("//system[attribute::class=\"Paddock\"]");

            // work through the 3 possible soil types
            while (soilIdx < 3)
            {
                if (soilIdx < simulation.OnFarmSoilTypes.Count)
                {
                    // for every soil type that has been defined
                    FarmSoilType soilArea = simulation.OnFarmSoilTypes[soilIdx];
                    areaPropnTotal += soilArea.AreaProportion;
                    SetGenericCompStateVar("Params", "F4P_AREAPROPN_ROT" + (soilIdx + 1).ToString(), String.Format("{0, 2:f2}", soilArea.AreaProportion));
                    if (soilArea.AreaProportion > 0)
                    {
                        FarmPaddockType defaultPaddock = simulation.OnFarmPaddocks[soilIdx];    //this paddock is applied to this soil type
                        Soil soilConfig = soilArea.SoilDescr;

                        int paddIdx = 0;
                        // for each paddock
                        foreach (XmlNode paddocknode in paddocks)
                        {
                            // parse the name of the paddock to see if this soil applies to it- Expect "Soil1_01"
                            int soilNo, paddNo;
                            ParsePaddockName(paddocknode, out soilNo, out paddNo);

                            // if this soil should be applied to this paddock
                            if (soilNo == soilIdx + 1)
                            {
                                SetSoilComponents(defaultPaddock, soilConfig, paddocknode);
                                SetSoilCrops(soilConfig, paddocknode);
                                paddIdx++;
                            }
                        }
                    }
                }
                else
                {
                    SetGenericCompStateVar("Params", "F4P_AREAPROPN_ROT" + (soilIdx + 1).ToString(), "0.0");
                }
                soilIdx++;
            }
        }

        /// <summary>
        /// Parse the paddock name. Expected: "Soil1_01". 
        /// If a name like 'feedlot_trade' is found then the indexes return -1.
        /// This function is very limited with the names having to follow the convention above.
        /// </summary>
        /// <param name="paddockNode">Paddock node in the sdml file</param>
        /// <param name="soilIdx">Returns the soil index</param>
        /// <param name="paddIdx">Returns the paddock index</param>
        private void ParsePaddockName(XmlNode paddockNode, out int soilIdx, out int paddIdx)
        {
            soilIdx = -1;
            paddIdx = -1;
            string paddockName = XmlUtilities.Attribute(paddockNode, "name");
            int _idx = paddockName.IndexOf('_');
            if ((_idx >= 0) && (String.Compare("soil", paddockName.Substring(0, 4), true) == 0))
            {
                string tmp = paddockName.Substring(_idx - 1, 1);
                soilIdx = Convert.ToInt32(tmp);
                tmp = paddockName.Substring(_idx + 1, paddockName.Length - (_idx + 1));
                paddIdx = Convert.ToInt32(tmp);
            }
        }

        /// <summary>
        /// Set the soilwat, soiln, surfaceom components
        /// </summary>
        /// <param name="defaultPaddock"></param>
        /// <param name="soilConfig"></param>
        /// <param name="paddocknode"></param>
        private void SetSoilComponents(FarmPaddockType defaultPaddock, Soil soilConfig, XmlNode paddocknode)
        {
            XmlNode apsimCompNode;
            XmlNode translatorNode;

            //set soilwat
            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"SoilWat\"]");
            initSoilWat(apsimCompNode, soilConfig);
            if (apsimCompNode != null)
            {
                translatorNode = apsimCompNode.ParentNode;
                initSoilWat_Trans(translatorNode, soilConfig, defaultPaddock.SoilType);
            }

            //set soiln
            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"SoilN\"]");
            initSoilN(apsimCompNode, soilConfig, defaultPaddock);
            if (apsimCompNode != null)
            {
                translatorNode = apsimCompNode.ParentNode;
                initSoilN_Trans(translatorNode, soilConfig);
            }

            //set surfaceOM
            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"SurfaceOM\"]");
            initSOM(apsimCompNode, soilConfig);
            if (apsimCompNode != null)
            {
                translatorNode = apsimCompNode.ParentNode;
                initSOM_Trans(translatorNode, soilConfig, defaultPaddock.SoilType);

                //if there is a paddock with init residue data and new crops in the rotation then set here 
                setSurfaceOMPaddockInits(apsimCompNode, defaultPaddock);
            }
        }

        /// <summary>
        /// Sets the soil parameters for the crop types in this paddock
        /// </summary>
        /// <param name="soilConfig">Soil crop parameters</param>
        /// <param name="paddocknode">The paddock node for the paddock</param>
        /// <returns></returns>
        private void SetSoilCrops(Soil soilConfig, XmlNode paddocknode)
        {
            XmlNode apsimCompNode;

            //set the ll, kl, xf values of the crop
            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Wheat\"]");
            setSoilCrop(apsimCompNode, "wheat", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Barley\"]");
            setSoilCrop(apsimCompNode, "barley", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Canola\"]");
            setSoilCrop(apsimCompNode, "canola", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Oats\"]");
            setSoilCrop(apsimCompNode, "oats", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.ChickPea\"]");
            setSoilCrop(apsimCompNode, "chickpea", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.FieldPea\"]");
            setSoilCrop(apsimCompNode, "fieldpea", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Fababean\"]");
            setSoilCrop(apsimCompNode, "fababean", soilConfig);

            apsimCompNode = paddocknode.SelectSingleNode("system/component[@class=\"Plant.Lupin\"]");
            setSoilCrop(apsimCompNode, "lupin", soilConfig);
            //sorghum
        }

        /// <summary>
        /// Set paddock inits for the surfaceOM component. Including stubble masses.
        /// </summary>
        /// <param name="apsimCompNode">SurfaceOM component in the paddock</param>
        /// <param name="paddock"></param>
        private void setSurfaceOMPaddockInits(XmlNode apsimCompNode, FarmPaddockType paddock)
        {
            // Stubble.
            //* this needs to be implemented with "wheat canola barley ... array of values
            // * - read in the values in each field and find the specified stubble type, set it, store it back
            // * 
            string stubbleType = paddock.StubbleType;
            if (stubbleType == null || stubbleType == "None")
                throw new Exception("No stubble type specified");

            FarmSoilType farmSoil = paddock.SoilType;
            OrgMatter stubble = new OrgMatter();
            XmlNode valueNode = apsimCompNode.SelectSingleNode("initdata/type");
            if (valueNode != null)
            {
                strToArray(ref stubble.types, valueNode.InnerText.ToLower());
                XmlNode nameNode = apsimCompNode.SelectSingleNode("initdata/name");
                strToArray(ref stubble.names, nameNode.InnerText.ToLower());
                XmlNode massNode = apsimCompNode.SelectSingleNode("initdata/mass");
                strToArray(ref stubble.masses, massNode.InnerText.ToLower());
                XmlNode cnrNode = apsimCompNode.SelectSingleNode("initdata/cnr");
                strToArray(ref stubble.cnrs, cnrNode.InnerText.ToLower());
                XmlNode sfNode = apsimCompNode.SelectSingleNode("initdata/standing_fraction");
                strToArray(ref stubble.stand_fract, sfNode.InnerText.ToLower());

                // check that all the residue types are listed that are in the crop rotation
                for (int crop = 0; crop < farmSoil.CropRotationList.Count; crop++)
                {
                    string cropName = farmSoil.CropRotationList[crop].name.ToLower();
                    if (stubble.types.IndexOf(cropName) < 0)
                    {
                        //re-add the stubble with the correct values
                        stubble.names.Add(cropName);
                        stubble.types.Add(cropName);
                        stubble.masses.Add("0.0");
                        stubble.cnrs.Add("60.0");           // ?
                        stubble.stand_fract.Add("0.0");
                    }
                }

                int idx = stubble.types.IndexOf(paddock.StubbleType);       // if the stubble type exists in the list already
                if (idx >= 0)
                {
                    stubble.Remove(idx);
                }
                //remove all mass amounts
                for (int i = 0; i < stubble.masses.Count; i++)
                    stubble.masses[i] = "0.0";

                //re-add the stubble with the correct values
                stubble.names.Add(paddock.StubbleType);
                stubble.types.Add(paddock.StubbleType);
                stubble.masses.Add(paddock.StubbleMass.ToString());
                stubble.cnrs.Add(YieldProphetUtility.GetStubbleCNRatio(paddock.StubbleType).ToString());
                stubble.stand_fract.Add("0.0");

                valueNode.InnerText = APSIM.Shared.Utilities.StringUtilities.BuildString(stubble.types.ToArray(), "    ");
                nameNode.InnerText = APSIM.Shared.Utilities.StringUtilities.BuildString(stubble.names.ToArray(), "    ");
                massNode.InnerText = APSIM.Shared.Utilities.StringUtilities.BuildString(stubble.masses.ToArray(), "    ");
                cnrNode.InnerText = APSIM.Shared.Utilities.StringUtilities.BuildString(stubble.cnrs.ToArray(), "    ");
                sfNode.InnerText = APSIM.Shared.Utilities.StringUtilities.BuildString(stubble.stand_fract.ToArray(), "    ");
            }
        }

        /// <summary>
        /// On the crop component, set the ll, kl, xf arrays
        /// </summary>
        /// <param name="apsimCompNode"></param>
        /// <param name="cropName">Crop name string</param>
        /// <param name="aSoil"></param>
        private void setSoilCrop(XmlNode apsimCompNode, string cropName, Soil aSoil)
        {
            if (apsimCompNode != null)
            {
                //set uptake source
                XmlNode anode;
                anode = apsimCompNode.SelectSingleNode("initdata/uptake_source");
                if (anode != null)
                    anode.InnerText = "apsim";

                bool cropInitialised = false;
                int i = 0;
                while (i < aSoil.Water.Crops.Count)
                {
                    SoilCrop crop = aSoil.Water.Crops[i];
                    if (String.Compare(crop.Name, cropName, true) == 0)
                    {
                        anode = apsimCompNode.SelectSingleNode("initdata/ll");
                        anode.InnerText = arrayAsStr(crop.LL, "f3");
                        anode = apsimCompNode.SelectSingleNode("initdata/kl");
                        anode.InnerText = arrayAsStr(crop.KL, "f3");
                        anode = apsimCompNode.SelectSingleNode("initdata/xf");
                        anode.InnerText = arrayAsStr(crop.XF, "f3");
                        i = aSoil.Water.Crops.Count;   //terminate
                        cropInitialised = true;
                    }
                    i++;
                }
                // if the soil crop values were not found in the soil list then use the wheat settings
                // so that even if the crop is not in the rotation but exists as a component in the
                // simulation, it will have the correct soil layers at init time
                if (!cropInitialised)
                {
                    SoilCrop wheat = aSoil.Water.Crops.Find(c => c.Name.Equals("wheat", StringComparison.InvariantCultureIgnoreCase));
                    if (wheat != null)
                    {
                        anode = apsimCompNode.SelectSingleNode("initdata/ll");
                        anode.InnerText = arrayAsStr(wheat.LL, "f3");
                        anode = apsimCompNode.SelectSingleNode("initdata/kl");
                        anode.InnerText = arrayAsStr(wheat.KL, "f3");
                        anode = apsimCompNode.SelectSingleNode("initdata/xf");
                        anode.InnerText = arrayAsStr(wheat.XF, "f3");
                    }
                }
            }
        }
        /// <summary>
        /// Initialise a soilwat component
        /// </summary>
        /// <param name="compNode"></param>
        /// <param name="aSoil"></param>
        private void initSoilWat(XmlNode compNode, Soil aSoil)
        {
            XmlNode anode;
            anode = compNode.SelectSingleNode("initdata");
            XmlNode comment = simulationXMLNode.OwnerDocument.CreateComment(aSoil.Name);
            anode.AppendChild(comment);

            anode = compNode.SelectSingleNode("initdata/dlayer");
            anode.InnerText = arrayAsStr(aSoil.Water.Thickness, "f1");
            anode = compNode.SelectSingleNode("initdata/sat");
            anode.InnerText = arrayAsStr(aSoil.Water.SAT, "f3");
            anode = compNode.SelectSingleNode("initdata/dul");
            anode.InnerText = arrayAsStr(aSoil.Water.DUL, "f3");
            anode = compNode.SelectSingleNode("initdata/ll15");
            anode.InnerText = arrayAsStr(aSoil.Water.LL15, "f3");
            anode = compNode.SelectSingleNode("initdata/air_dry");
            anode.InnerText = arrayAsStr(aSoil.Water.AirDry, "f3");
            anode = compNode.SelectSingleNode("initdata/bd");
            anode.InnerText = arrayAsStr(aSoil.Water.BD, "f3");
            anode = compNode.SelectSingleNode("initdata/swcon");
            anode.InnerText = arrayAsStr(aSoil.SoilWater.SWCON, "f3");
            anode = compNode.SelectSingleNode("initdata/sw");
            anode.InnerText = arrayAsStr(aSoil.Water.DUL, "f3");

            anode = compNode.SelectSingleNode("initdata/diffus_const");
            anode.InnerText = asApsimStr(aSoil.SoilWater.DiffusConst, "f3");
            anode = compNode.SelectSingleNode("initdata/diffus_slope");
            anode.InnerText = asApsimStr(aSoil.SoilWater.DiffusSlope, "f3");
            anode = compNode.SelectSingleNode("initdata/cn2_bare");
            anode.InnerText = asApsimStr(aSoil.SoilWater.CN2Bare, "f3");
            anode = compNode.SelectSingleNode("initdata/cn_red");
            anode.InnerText = asApsimStr(aSoil.SoilWater.CNRed, "f3");
            anode = compNode.SelectSingleNode("initdata/cn_cov");
            anode.InnerText = asApsimStr(aSoil.SoilWater.CNCov, "f3");
            anode = compNode.SelectSingleNode("initdata/salb");
            anode.InnerText = asApsimStr(aSoil.SoilWater.Salb, "f3");
            anode = compNode.SelectSingleNode("initdata/SummerCona");
            anode.InnerText = asApsimStr(aSoil.SoilWater.SummerCona, "f3");

            anode = compNode.SelectSingleNode("initdata/WinterCona");
            anode.InnerText = asApsimStr(aSoil.SoilWater.WinterCona, "f3");
            anode = compNode.SelectSingleNode("initdata/SummerU");
            anode.InnerText = asApsimStr(aSoil.SoilWater.SummerU, "f3");
            anode = compNode.SelectSingleNode("initdata/WinterU");
            anode.InnerText = asApsimStr(aSoil.SoilWater.WinterU, "f3");
            anode = compNode.SelectSingleNode("initdata/SummerDate");
            anode.InnerText = aSoil.SoilWater.SummerDate;
            anode = compNode.SelectSingleNode("initdata/WinterDate");
            anode.InnerText = aSoil.SoilWater.WinterDate;

            anode = compNode.SelectSingleNode("initdata/slope");
            anode.InnerText = asApsimStr(aSoil.SoilWater.Slope);
            anode = compNode.SelectSingleNode("initdata/discharge_width");
            anode.InnerText = asApsimStr(aSoil.SoilWater.DischargeWidth);
            anode = compNode.SelectSingleNode("initdata/catchment_area");
            anode.InnerText = asApsimStr(aSoil.SoilWater.CatchmentArea);
            anode = compNode.SelectSingleNode("initdata/max_pond");
            anode.InnerText = asApsimStr(aSoil.SoilWater.MaxPond);
        }


        /// <summary>
        /// Initialise a soiln component
        /// </summary>
        /// <param name="compNode"></param>
        /// <param name="aSoil"></param>
        private void initSoilN(XmlNode compNode, Soil aSoil, FarmPaddockType paddock)
        {
            XmlNode anode;
            anode = compNode.SelectSingleNode("initdata");
            XmlNode comment = simulationXMLNode.OwnerDocument.CreateComment(aSoil.Name);
            anode.AppendChild(comment);

            anode = compNode.SelectSingleNode("initdata/soiltype");
            anode.InnerText = aSoil.SoilType;

            anode = compNode.SelectSingleNode("initdata/root_cn");
            anode.InnerText = asApsimStr(aSoil.SoilOrganicMatter.RootCN);
            anode = compNode.SelectSingleNode("initdata/root_wt");
            anode.InnerText = asApsimStr(aSoil.SoilOrganicMatter.RootWt);
            anode = compNode.SelectSingleNode("initdata/soil_cn");
            anode.InnerText = asApsimStr(aSoil.SoilOrganicMatter.SoilCN);
            anode = compNode.SelectSingleNode("initdata/enr_a_coeff");
            anode.InnerText = asApsimStr(aSoil.SoilOrganicMatter.EnrACoeff);
            anode = compNode.SelectSingleNode("initdata/enr_b_coeff");
            anode.InnerText = asApsimStr(aSoil.SoilOrganicMatter.EnrBCoeff);
            anode = compNode.SelectSingleNode("initdata/profile_reduction");
            anode.InnerText = "off";

            anode = compNode.SelectSingleNode("initdata/oc");
            anode.InnerText = arrayAsStr(aSoil.SoilOrganicMatter.OC);
            anode = compNode.SelectSingleNode("initdata/ph");
            anode.InnerText = arrayAsStr(aSoil.Analysis.PH);
            anode = compNode.SelectSingleNode("initdata/fbiom");
            anode.InnerText = arrayAsStr(aSoil.SoilOrganicMatter.FBiom);
            anode = compNode.SelectSingleNode("initdata/finert");
            anode.InnerText = arrayAsStr(aSoil.SoilOrganicMatter.FInert);

            // set all the initial nitrogen values for this soiln
            double[] NH4Array = new double[aSoil.SoilWater.Thickness.Length];
            double[] NO3Array = new double[aSoil.SoilWater.Thickness.Length];
            for (int i = 0; i < NH4Array.Length; i++)
            {
                NH4Array[i] = 0.0;
                NO3Array[i] = 0.0;
            }
            if (paddock.Sample != null)
            {
                // be cautious and allow incomplete sample array sizes
                int i = 0;
                while ((i < paddock.Sample.NH4.Length) && (i < aSoil.SoilWater.Thickness.Length))
                {
                    NH4Array[i] = paddock.Sample.NH4[i];
                    i++;
                }
                i = 0;
                while ((i < paddock.Sample.NO3.Length) && (i < aSoil.SoilWater.Thickness.Length))
                {
                    NO3Array[i] = paddock.Sample.NO3[i];
                    i++;
                }
            }
            // these values are now set from the paddock soil Sample object
            anode = compNode.SelectSingleNode("initdata/no3ppm");
            anode.InnerText = arrayAsStr(NO3Array, "f3");
            anode = compNode.SelectSingleNode("initdata/nh4ppm");
            anode.InnerText = arrayAsStr(NH4Array, "f3");
        }

        private void initSOM(XmlNode compNode, Soil aSoil)
        {
            XmlNode anode;
            anode = compNode.SelectSingleNode("initdata");
            XmlNode comment = simulationXMLNode.OwnerDocument.CreateComment(aSoil.Name);
            anode.AppendChild(comment);

            anode = compNode.SelectSingleNode("initdata/name");
            if (anode.InnerText.Length < 1)
            {
                // if there are no residues configured then set some defaults
                anode.InnerText = "wheat canola pasture barley chickpea oats fieldpea fababean lupin";
                anode = compNode.SelectSingleNode("initdata/type");
                anode.InnerText = "wheat canola pasture barley chickpea oats fieldpea fababean lupin";
                anode = compNode.SelectSingleNode("initdata/mass");
                anode.InnerText = "0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0";
                anode = compNode.SelectSingleNode("initdata/cnr");
                anode.InnerText = "60.0 80.0 60.0 60.0 60.0 60.0 60.0 60.0 60.0";
                anode = compNode.SelectSingleNode("initdata/standing_fraction");
                anode.InnerText = "0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0 0.0";
            }
        }

        /// <summary>
        /// Initialise the Soil water translator
        /// </summary>
        /// <param name="compNode"></param>
        /// <param name="aSoil"></param>
        private void initSoilWat_Trans(XmlNode compNode, Soil aSoil, FarmSoilType farmSoil)
        {
            TSDMLValue init = GetTypedInit(compNode, "psd");
            init.member("sand").setElementCount((uint)aSoil.SoilWater.Thickness.Length);
            init.member("clay").setElementCount((uint)aSoil.SoilWater.Thickness.Length);
            double value;
            for (uint x = 0; x <= aSoil.SoilWater.Thickness.Length - 1; x++)
            {
                if ((aSoil.Analysis.ParticleSizeSand != null) && (aSoil.Analysis.ParticleSizeSand.Length > x))
                {
                    value = aSoil.Analysis.ParticleSizeSand[x];
                    if (value.Equals(double.NaN))
                        value = 0.0;                            // probably should be interpolated from any existing values
                    init.member("sand").item(x + 1).setValue(value * 0.01);

                    value = aSoil.Analysis.ParticleSizeClay[x];
                    if (value.Equals(double.NaN))
                        value = 0.0;
                    init.member("clay").item(x + 1).setValue(value * 0.01);
                }
                else
                {
                    // ## not suitable but it will allow the simulation to run
                    init.member("sand").item(x + 1).setValue(0.0);
                    init.member("clay").item(x + 1).setValue(0.0);
                }
            }
            SetTypedInit(compNode, "psd", init);

            // configure the published events for this translator so each crop component in the rotation is supported
            init = GetTypedInit(compNode, "published_events");
            
            // if not the correct number of published events
            if (init.count() < 2)
            {
                init.setElementCount(2);
                init.item(1).member("name").setValue(".model.new_profile");
                init.item(1).member("connects").setElementCount(1);
                init.item(1).member("connects").item(1).setValue("..nitrogen.model.new_profile");

                init.item(2).member("name").setValue(".model.nitrogenchanged");
                init.item(2).member("connects").setElementCount(1);
                init.item(2).member("connects").item(1).setValue("..nitrogen.model.nitrogenchanged");
            }

            // now ensure that the connections go to each crop component
            // there should be a connection item for every plant component in the paddock
            TTypedValue connects = null;
            uint idx = 1;
            // find the correct item in the published events array
            while ((connects == null) && idx <= 2)
            {
                if (init.item(idx).member("name").asStr() == ".model.new_profile")
                    connects = init.item(idx).member("connects");                       
                idx++;
            }

            string connectionName;
            // for each item in the crop rotation list
            for (int crop = 0; crop < farmSoil.CropRotationList.Count; crop++)
            {
                string cropName = farmSoil.CropRotationList[crop].name;
                if (IsValidCropName(cropName))
                {
                    idx = 1;
                    bool found = false;
                    while (!found && (idx <= connects.count()))
                    {
                        connectionName = connects.item(idx).asStr().ToLower();
                        if (connectionName.Contains(cropName.ToLower()))
                            found = true;
                        idx++;
                    }

                    //if not found int the connections then add it to the list
                    if (!found)
                    {
                        connects.setElementCount(connects.count() + 1);
                        connects.item(connects.count()).setValue(".." + cropName + ".model.new_profile");
                    }
                }
            }
            SetTypedInit(compNode, "published_events", init);
        }

        /// <summary>
        /// Check that the crop name is one of the Plant crops.
        /// </summary>
        /// <param name="crop">Crop name</param>
        /// <returns>True if found</returns>
        private bool IsValidCropName(string crop)
        {
            crop = crop.Trim();
            int i = 0;
            while (i < ValidCropNames.Length)
            {
                if (String.Compare(crop, ValidCropNames[i], true) == 0)
                    return true;
                i++;
            }
            return false;
        }

        private void initSoilN_Trans(XmlNode compNode, Soil aSoil)
        {
            TSDMLValue init = GetTypedInit(compNode, "excrete_params");
            if (init.count() < 3)
            {
                init.setElementCount(3);
                init.item(1).setValue(0.3);     //depositied in camp areas
                init.item(2).setValue(0.25);    //urine that volatizes
                init.item(3).setValue(0.035);   //surface faecal breakdown
                SetTypedInit(compNode, "excrete_params", init);
            }

            //may not need to do this in an already configured simulation
            init = GetTypedInit(compNode, "published_events");
            if (init.count() < 1)
            {
                init.setElementCount(2);
                init.item(1).member("name").setValue(".model.new_solute");
                init.item(1).member("connects").setElementCount(1);
                init.item(1).member("connects").item(1).setValue("..water.model.new_solute");
                init.item(2).member("name").setValue(".model.actualresiduedecompositioncalculated");
                init.item(2).member("connects").setElementCount(1);
                init.item(2).member("connects").item(1).setValue("..surfaceom.model.actualresiduedecompositioncalculated");
                SetTypedInit(compNode, "published_events", init);
            }
        }

        /// <summary>
        /// Initialise the SOM translator
        /// </summary>
        /// <param name="compNode"></param>
        /// <param name="aSoil"></param>
        private void initSOM_Trans(XmlNode compNode, Soil aSoil, FarmSoilType farmSoil)
        {
            TSDMLValue init = GetTypedInit(compNode, "surfaceom_types");
            bool found;

            // for each crop type in the rotaion list there should be a residue item in the translator
            for (int res = 0; res < farmSoil.CropRotationList.Count; res++)
            {
                if (IsValidCropName(farmSoil.CropRotationList[res].name))
                {
                    uint i = 1;
                    found = false;
                    while (!found && (i <= init.count()))
                    {
                        if (init.item(i).asStr() == farmSoil.CropRotationList[res].name)
                            found = true;
                        i++;
                    }
                    if (!found)
                    {
                        init.setElementCount(init.count() + 1);
                        init.item(init.count()).setValue(farmSoil.CropRotationList[res].name);
                    }
                }
            }
            SetTypedInit(compNode, "surfaceom_types", init);

            //may not need to do this in an already configured simulation
            init = GetTypedInit(compNode, "published_events");
            if (init.count() < 1)
            {
                init.setElementCount(2);
                init.item(1).member("name").setValue(".model.potentialresiduedecompositioncalculated");
                init.item(1).member("connects").setElementCount(1);
                init.item(1).member("connects").item(1).setValue("..nitrogen.model.potentialresiduedecompositioncalculated");
                init.item(2).member("name").setValue(".model.incorpfompool");
                init.item(2).member("connects").setElementCount(1);
                init.item(2).member("connects").item(1).setValue("..nitrogen.model.incorpfompool");
                SetTypedInit(compNode, "published_events", init);
            }
        }

        /// <summary>
        /// Write all the livestock enterprise information
        /// </summary>
        /// <param name="simulation"></param>
        public void WriteStockEnterprises(FarmLivestock livestock)
        {
            if (livestock.TradeLambCount > 0)
            {
                SetGenericCompStateVar("Params", "F4P_TRADE_BREED", DoQuote(livestock.TradeLambBreed));
                SetGenericCompStateVar("Params", "F4P_TRADE_COUNT", livestock.TradeLambCount.ToString());
                SetGenericCompStateVar("Params", "F4P_TRADE_BUY_ON", DoQuote(livestock.TradeLambBuyDay));
                SetGenericCompStateVar("Params", "F4P_TRADE_LAMB_SALE_WT", String.Format("{0, 2:f2}", livestock.TradeLambSaleWt));
            }
            // configure the breeding flocks
            string prefix;
            for (int f = 0; f < livestock.Flocks.Count; f++)
            {
                string selfReplace = "TRUE";
                if (livestock.Flocks[f].SelfReplacing == false)
                {
                    selfReplace = "FALSE";
                }
                SetGenericCompStateVar("Params", "F4P_SELF_REPLACING", selfReplace);
                prefix = "F4P_FLOCK" + (f + 1).ToString();
                SetGenericCompStateVar("Params", prefix + "_BREED", DoQuote(livestock.Flocks[f].Breed));
                SetGenericCompStateVar("Params", prefix + "_SIRE", DoQuote(livestock.Flocks[f].SireBreed));
                SetGenericCompStateVar("Params", prefix  +"_EWES", livestock.Flocks[f].BreedingEweCount.ToString());
                SetGenericCompStateVar("Params", prefix + "_JOIN", DoQuote(livestock.Flocks[f].EweJoinDay));
                SetGenericCompStateVar("Params", prefix + "_LAMB_SALE_WT", String.Format("{0, 2:f2}", livestock.Flocks[f].LambSaleWt));
                SetGenericCompStateVar("Params", prefix + "_CULL_YRS", String.Format("{0, 2:f2}", livestock.Flocks[f].CastForAgeYears));
                // other breed parameters

            }
            SetGenericCompStateVar("Params", "F4P_SHEAR_DAY", DoQuote(livestock.ShearingDay));

            // these should only be for the breeding flock (see ausfarm_warooka)
            SetGenericCompStateVar("Params", "F4P_SUPP1", DoQuote(livestock.Supplement1));
            SetGenericCompStateVar("Params", "F4P_SUPP2", DoQuote(livestock.Supplement2));
            SetGenericCompStateVar("Params", "F4P_SUPP1_PROPN", String.Format("{0, 2:f2}", livestock.Supp1Propn));
            SetGenericCompStateVar("Params", "F4P_SUPP2_PROPN", String.Format("{0, 2:f2}", livestock.Supp2Propn));
        }

        private string DoQuote(string value)
        {
            return "'" + value + "'";
        }
    }
}
