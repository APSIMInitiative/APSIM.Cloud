// -----------------------------------------------------------------------
// <copyright file="Utility.cs" company="CSIRO">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Serialization;
    using System.IO;
    using System.Data;
    using System.Reflection;
    using APSIM.Shared.Utilities;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class YieldProphetUtility
    {
        /// <summary>Factory method for creating a YieldProphet object from a file.</summary>
        /// <param name="fileName">The filename of the xml file</param>
        /// <returns>The newly created object.</returns>
        public static YieldProphet YieldProphetFromFile(string fileName)
        {
            if (Path.GetExtension(fileName) == ".zip")
                return YieldProphetFromZip(fileName);
            else
            {
                StreamReader reader = new StreamReader(fileName);
                string xml = reader.ReadToEnd();
                reader.Close();

                return YieldProphetUtility.YieldProphetFromXML(xml, Path.GetDirectoryName(fileName));
            }
        }

        /// <summary>
        /// Creates a instance of a yield prophet spec from a zip file.
        /// </summary>
        /// <param name="zipFileName">The name of the .zip file.</param>
        /// <returns>The newly create yieldprophet object.</returns>
        private static YieldProphet YieldProphetFromZip(string zipFileName)
        {
            YieldProphet yieldProphet;

            string tempFolder = Path.GetTempFileName();
            File.Delete(tempFolder);
            Directory.CreateDirectory(tempFolder);
            FileStream reader = File.OpenRead(zipFileName);
            try
            {
                string[] fileNames = ZipUtilities.UnZipFiles(reader, tempFolder, null);

                string fileName = Path.Combine(tempFolder, "YieldProphet.xml");
                if (!File.Exists(fileName))
                {
                    // Look for first XML file.
                    foreach (string file in fileNames)
                    {
                        if (file.Contains(".xml"))
                        {
                            fileName = file;
                            break;
                        }
                    }
                }

                yieldProphet = YieldProphetUtility.YieldProphetFromFile(fileName);
                yieldProphet.ReportName = Path.GetFileNameWithoutExtension(fileName);
            }
            finally
            {
                reader.Close();
            }
            Directory.Delete(tempFolder, true);
            return yieldProphet;
        }

        /// <summary>Factory method for creating a YieldProphet object.</summary>
        /// <param name="xml">The XML to use to create the object</param>
        /// <returns>The newly created object.</returns>
        public static YieldProphet YieldProphetFromXML(string xml, string workingFolder)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            if (XmlUtilities.Value(doc.DocumentElement, "Version") != "9")
                return YieldProphetOld.YieldProphetFromXML(doc.DocumentElement, workingFolder);
            else
                return YieldProphetFromXML(doc.DocumentElement);
        }

        /// <summary>Factory method for creating a YieldProphet object from an XmlNode</summary>
        /// <param name="xml">The XML node to use to create the object</param>
        /// <returns>The newly created object.</returns>
        public static YieldProphet YieldProphetFromXML(XmlNode node)
        {
            XmlReader reader = new XmlNodeReader(node);
            reader.Read();
            XmlSerializer serial = new XmlSerializer(typeof(YieldProphet));
            return (YieldProphet)serial.Deserialize(reader);
        }

        /// <summary>Factory method for creating a Paddock object from an XmlNode</summary>
        /// <param name="xml">The XML node to use to create the object</param>
        /// <returns>The newly created object.</returns>
        public static Paddock PaddockFromXML(XmlNode node)
        {
            XmlReader reader = new XmlNodeReader(node);
            reader.Read();
            XmlSerializer serial = new XmlSerializer(typeof(Paddock));
            return (Paddock)serial.Deserialize(reader);
        }

        /// <summary>Convert the YieldProphet spec to XML.</summary>
        /// <returns>The XML string.</returns>
        public static string YieldProphetToXML(YieldProphet yieldProphet)
        {
            XmlSerializer serial = new XmlSerializer(typeof(YieldProphet));
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            StringWriter writer = new StringWriter();
            serial.Serialize(writer, yieldProphet, ns);
            string xml = writer.ToString();
            // The code below can cause out of memory error with very large number of simulations
            //if (xml.Length > 5 && xml.Substring(0, 5) == "<?xml")
            //{
            //    // remove the first line: <?xml version="1.0"?>/n
            //    int posEol = xml.IndexOf("\n");
            //    if (posEol != -1)
            //        return xml.Substring(posEol + 1);
            //}
            return xml;
        }

        /// <summary>Return a C:N ratio for the specified stubble type.</summary>
        /// <param name="StubbleType">Type of the stubble.</param>
        /// <returns></returns>
        public static int GetStubbleCNRatio(string StubbleType)
        {
            string[] StubbleTypes = {"barley", "canola", "chickpea", "fababean", "fieldpea", "grass", "lentils",
                                     "lucerne", "lupin", "medic", "oats", "sorghum", "triticale", "vetch",
                                     "weeds",   "wheat"};
            int[] CNRatios = { 80, 120, 42, 42, 29, 80, 42, 42, 42, 42, 80, 80, 80, 42, 80, 80 };

            int PosStubble = StringUtilities.IndexOfCaseInsensitive(StubbleTypes, StubbleType);
            if (PosStubble != -1)
                return CNRatios[PosStubble];
            else
                return 80;
        }

        /// <summary>Gets the crop being sown or null if no crop</summary>
        /// <param name="paddock">The paddock.</param>
        /// <returns></returns>
        public static Sow GetCropBeingSown(IEnumerable<Management> managerActions)
        {
            // Loop through all management actions and create an operations list
            foreach (Management management in managerActions)
            {
                if (management is Sow)
                    return (management as Sow);
            }

            return null;
        }

        /// <summary>Calculates the average tillage percent.</summary>
        /// <param name="tillages">The tillages.</param>
        /// <returns>The percentage stubble incorporated in the top 10cm</returns>
        public static double CalculateAverageTillagePercent(IEnumerable<Tillage> tillages)
        {
            double sum = 0;
            foreach (Tillage tillage in tillages)
            {
                if (tillage.Disturbance == Tillage.DisturbanceEnum.Low)
                    sum += 20;
                else if (tillage.Disturbance == Tillage.DisturbanceEnum.Medium)
                    sum += 50;
                else if (tillage.Disturbance == Tillage.DisturbanceEnum.High)
                    sum += 80;
            }
            return sum / tillages.Count();
        }

        /// <summary>Fills the auto-calculated fields.</summary>
        /// <param name="paddock">The paddock.</param>
        /// <param name="observedData">The observed data.</param>
        /// <param name="weatherData">The weather data.</param>
        public static void FillInCalculatedFields(Paddock paddock, DataTable observedData, string workingFolder)
        {
            IEnumerable<Tillage> tillages = paddock.Management.OfType<Tillage>();
            if (tillages.Count() > 0)
                paddock.StubbleIncorporatedPercent = YieldProphetUtility.CalculateAverageTillagePercent(tillages);

            DateTime lastRainfallDate = GetLastRainfallDate(observedData);
            if (lastRainfallDate != DateTime.MinValue)
                paddock.DateOfLastRainfallEntry = lastRainfallDate.ToString("dd/MM/yyyy");

            string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
            if (metFiles.Length > 0)
            {
                string firstMetFile = Path.Combine(workingFolder, metFiles[0]);
                ApsimTextFile textFile = new ApsimTextFile();
                textFile.Open(firstMetFile);
                DataTable data = textFile.ToTable();
                textFile.Close();
                paddock.RainfallSinceSoilWaterSampleDate = SumTableAfterDate(data, "Rain", paddock.SoilWaterSampleDate);
                if (data.Rows.Count > 0)
                {
                    DataRow lastweatherRow = data.Rows[data.Rows.Count - 1];
                    paddock.LastClimateDate = DataTableUtilities.GetDateFromRow(lastweatherRow);
                }
            }
        }

        /// <summary>Gets the last rainfall date in the observed data.</summary>
        /// <param name="observedData">The observed data.</param>
        /// <returns>The date of the last rainfall row or DateTime.MinValue if no data.</returns>
        private static DateTime GetLastRainfallDate(DataTable observedData)
        {
            if (observedData == null || observedData.Rows.Count == 0)
                return DateTime.MinValue;

            int lastRowIndex = observedData.Rows.Count - 1;
            return DataTableUtilities.GetDateFromRow(observedData.Rows[lastRowIndex]);
        }

        /// <summary>Sums a column of a data table between dates.</summary>
        /// <param name="observedData">The data table.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="date1">The date1.</param>
        /// <param name="date2">The date2.</param>
        /// <returns>The sum of rainfall.</returns>
        private static double SumTableAfterDate(DataTable data, string columnName, DateTime date1)
        {
            double sum = 0;
            if (data.Columns.Contains("Rain"))
            {
                foreach (DataRow row in data.Rows)
                {
                    DateTime rowDate = DataTableUtilities.GetDateFromRow(row);
                    if (rowDate >= date1)
                        sum += Convert.ToDouble(row[columnName]);
                }
            }

            return sum;
        }
    }
}
