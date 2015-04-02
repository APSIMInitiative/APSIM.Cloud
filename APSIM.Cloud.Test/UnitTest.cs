using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using System.Xml;
using System.Diagnostics;

namespace Test
{
    /// <summary>
    /// Test class
    /// </summary>
    [TestClass]
    public class TestAPSIMCloud
    {
        /// <summary>The files directory</summary>
        private const string filesDirectory = @"D:\APSIM.Cloud\Test\Files";

        /// <summary>The working directory</summary>
        private const string workingDirectory = @"D:\APSIM.Cloud\Test\Generated";

        /// <summary>Compares the specified file in both the files and working directory.</summary>
        /// <param name="fileName">Name of the file.</param>
        private void CompareFiles(string fileName)
        {
            StreamReader file1 = new StreamReader(Path.Combine(filesDirectory, fileName));
            string fileContents1 = file1.ReadToEnd();
            file1.Close();

            StreamReader file2 = new StreamReader(Path.Combine(workingDirectory, fileName));
            string fileContents2 = file2.ReadToEnd();
            file2.Close();

            Assert.AreEqual(fileContents1, fileContents2);
        }

        /// <summary>Runs APSIM on the given xml file and makes sure no errors.</summary>
        /// <param name="xmlFileName">Name of the XML file.</param>
        private static void RunAPSIM(string xmlFileName)
        {
            // Create a YieldProphet object from our file.
            YieldProphet spec = YieldProphetUtility.YieldProphetFromFile(xmlFileName);
            foreach (Paddock paddock in spec.Paddock)
                paddock.NowDate = new DateTime(2015, 3, 31);

            // Create an APSIM spec
            List<APSIMSpec> simulations = YieldProphetToAPSIM.ToAPSIM(spec);

            // Now create all the files.
            APSIMFiles.Create(simulations, workingDirectory);

            // Make sure we got a .apsim file.
            string apsimFileName = Path.Combine(workingDirectory, "YieldProphet.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);

            List<XmlNode> simulationNodes = Utility.Xml.ChildNodes(doc.DocumentElement, "");
            Assert.AreEqual(simulationNodes.Count, 6);
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[0]), "ThisYear");
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[1]), "Base");
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[2]), "NUnlimited");
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[3]), "NUnlimitedFromToday");
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[4]), "Next10DaysDry");
            Assert.AreEqual(Utility.Xml.NameAttr(simulationNodes[5]), "Factorials");

            doc.DocumentElement.RemoveChild(simulationNodes[1]);
            doc.DocumentElement.RemoveChild(simulationNodes[2]);
            doc.DocumentElement.RemoveChild(simulationNodes[3]);
            doc.DocumentElement.RemoveChild(simulationNodes[4]);
            doc.Save(apsimFileName);

            Process p = Utility.Process.RunProcess(@"D:\APSIM\Model\Apsim.exe",
                                                   apsimFileName,
                                                   workingDirectory);
            string errors = Utility.Process.CheckProcessExitedProperly(p);
            Assert.IsFalse(errors.Contains("Fail"));

            StreamReader reader = new StreamReader(Path.Combine(workingDirectory, "ThisYear_1.sum"));
            string sumFileContents = reader.ReadToEnd();
            reader.Close();
            Assert.IsFalse(sumFileContents.Contains("APSIM  Fatal  Error"));
        }

        /// <summary>Called before every test to initialise tests.</summary>
        [TestInitialize]
        public void Setup()
        {
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
            Directory.CreateDirectory(workingDirectory);
        }

        /// <summary>Tests the XML converter to go to the new format</summary>
        [TestMethod]
        public void ConvertOldYPToNew()
        {
            // Get our file name
            string fileName = Path.Combine(filesDirectory, "TestConverter.xml");
            
            // Create a YieldProphet object from our file.
            YieldProphet spec = YieldProphetUtility.YieldProphetFromFile(fileName);

            Assert.AreEqual(spec.Paddock.Count, 2);
            Assert.AreEqual(spec.Paddock[0].Name, "2011;mcvp;Grain Filling Baudin");
            Assert.AreEqual(spec.Paddock[0].StartSeasonDate, new DateTime(2011, 4, 1));
            Assert.AreEqual(spec.Paddock[0].Management.Count, 6);
            Assert.IsTrue(spec.Paddock[0].Management[0] is Sow);
            Assert.AreEqual((spec.Paddock[0].Management[0] as Sow).Date, new DateTime(2011, 6, 23));
            Assert.AreEqual((spec.Paddock[0].Management[0] as Sow).Crop, "Barley");
            Assert.IsTrue(spec.Paddock[0].Management[1] is Fertilise);
            Assert.IsTrue(spec.Paddock[0].Management[2] is Fertilise);
            Assert.IsTrue(spec.Paddock[0].Management[3] is Irrigate);
            Assert.IsTrue(spec.Paddock[0].Management[4] is Tillage);
            Assert.IsTrue(spec.Paddock[0].Management[5] is StubbleRemoved);
            Assert.AreEqual(spec.Paddock[0].Samples.Count, 1);
            Assert.AreEqual(spec.Paddock[0].ObservedData.Rows.Count, 3);
            Assert.AreEqual(spec.Paddock[0].ObservedData.Rows[0]["Rain"], 50.0f);
            Assert.AreEqual(spec.Paddock[0].ObservedData.Rows[1]["Rain"], 0.0f);
            Assert.AreEqual(spec.Paddock[0].ObservedData.Rows[2]["Rain"], 80.0f);

            Assert.AreEqual((spec.Paddock[1].Management[0] as Sow).Date, new DateTime(2011, 7, 23));
            Assert.AreEqual((spec.Paddock[1].Management[1] as Fertilise).Date, new DateTime(2011, 7, 23));
            
        }

        /// <summary>Tests the longterm weather file generation</summary>
        [TestMethod]
        public void GenerateWeatherFile50Years()
        {
            // name of old style validation xml file.
            // NB: This is a real YP validation XML file from 2004.
            string fileName = Path.Combine(filesDirectory, "TestValidation.xml");

            // Create a YieldProphet object from our YP xml file
            YieldProphet spec = YieldProphetUtility.YieldProphetFromFile(fileName);

            // Read in the rainfall data
            Utility.ApsimTextFile observedDataFile = new Utility.ApsimTextFile();
            observedDataFile.Open(Path.Combine(filesDirectory, "TestValidation.rain"));
            DataTable observedData = observedDataFile.ToTable();
            observedDataFile.Close();

            // Write the weather file.
            string rainFileName = Path.Combine(workingDirectory, spec.Paddock[0].Name) + ".met";
            WeatherFile weatherFiles = new WeatherFile();
            weatherFiles.CreateLongTerm(rainFileName, spec.Paddock[0].StationNumber,
                                        spec.Paddock[0].StartSeasonDate,
                                        spec.Paddock[0].StartSeasonDate.AddDays(300),
                                        new DateTime(2004, 6, 1),
                                        observedData, 50);

            Assert.AreEqual(weatherFiles.FilesCreated.Length, 50);

            Assert.AreEqual(Path.GetFileName(weatherFiles.FilesCreated[0]), "2004;A and R Weidemann;Wep 11954.met");
            Utility.ApsimTextFile weatherFile = new Utility.ApsimTextFile();
            weatherFile.Open(Path.Combine(workingDirectory, weatherFiles.FilesCreated[0]));
            DataTable weatherData = weatherFile.ToTable();
            weatherFile.Close();
            Assert.AreEqual(weatherData.Rows.Count, 301);
            Assert.AreEqual(weatherData.Rows[0]["codes"], "SSSO");
            Assert.AreEqual(weatherData.Rows[61]["codes"], "SSSO");
            Assert.AreEqual(weatherData.Rows[62]["codes"], "HHHH");
        }

        /// <summary>Tests the ability to convert a YP validation specfication to an APSIM one.</summary>
        [TestMethod]
        public void ConvertYPValidationSpecToAPSIM()
        {
            // NB: This is a real YP validation XML file from 2004.
            string validationFileName = Path.Combine(filesDirectory, "TestValidation.xml"); 

            // Create a YieldProphet object from our file.
            YieldProphet spec = YieldProphetUtility.YieldProphetFromFile(validationFileName);

            // Run the files.
            List<APSIMSpec> simulations = YieldProphetToAPSIM.ToAPSIM(spec);

            Assert.AreEqual(simulations.Count, 2);
            Assert.AreEqual(simulations[0].Name, "2004;A and R Weidemann;Wep 1");
            Assert.AreEqual(simulations[1].Name, "2004;A and R Weidemann;Wep 2");
            Assert.AreEqual(simulations[0].StartDate, new DateTime(2004, 4, 1));
            Assert.AreEqual(simulations[0].EndDate, new DateTime(2005, 1, 26));
            Assert.AreEqual(simulations[0].Management.Count, 6);
            Assert.IsTrue(simulations[0].Management[0] is Sow);
            Assert.AreEqual((simulations[0].Management[0] as Sow).Date, new DateTime(2004, 6, 6));
            Assert.AreEqual((simulations[0].Management[0] as Sow).Crop, "Wheat");
            Assert.IsTrue(simulations[0].Management[1] is Fertilise);
            Assert.IsTrue(simulations[0].Management[2] is Fertilise);
            Assert.IsTrue(simulations[0].Management[3] is ResetWater);
            Assert.IsTrue(simulations[0].Management[4] is ResetSurfaceOrganicMatter);
            Assert.IsTrue(simulations[0].Management[5] is ResetNitrogen);
            Assert.AreEqual(simulations[0].ObservedData.Rows.Count, 275);

            Assert.AreEqual(simulations[0].Samples.Count, 1);
            Assert.AreEqual(simulations[0].Samples[0].SWUnits, Sample.SWUnitsEnum.Gravimetric);
            Assert.AreEqual(simulations[0].Samples[0].NO3Units, Sample.NUnitsEnum.ppm);
            Assert.AreEqual(simulations[0].Samples[0].NH4Units, Sample.NUnitsEnum.ppm);
            Assert.AreEqual(simulations[0].Samples[0].PHUnits, Sample.PHSampleUnitsEnum.CaCl2);
            Assert.AreEqual(simulations[0].Samples[0].OCUnits, Sample.OCSampleUnitsEnum.WalkleyBlack);
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].Thickness, new double[] { 100.0, 300.0, 300.0, 300.0 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].SW, new double[] { 0.139, 0.247, 0.215, 0.191 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].NH4, new double[] { 1.1, 0.5, 0.5, 0.5 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].NO3, new double[] { 3, 3, 1.8, 0.5 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].OC, new double[] { 1.2, double.NaN, double.NaN, double.NaN }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].EC, new double[] { 0.09, 0.19, 0.24, 0.3 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].PH, new double[] { 6, 7.9, 8.4, 8.5 }));
            Assert.IsTrue(Utility.Math.AreEqual(simulations[0].Soil.Samples[0].CL, new double[] { 29, 11, 10, 10 }));
        }

        /// <summary>Tests the ability to generate a crop report</summary>
        [TestMethod]
        public void ConvertYPCropReportSpecToAPSIM()
        {
            // NB: This is a real YP crop report XML file from 2014.
            string cropReportFileName = Path.Combine(filesDirectory, "CropReport.xml");

            // Create a YieldProphet object from our file.
            YieldProphet spec = YieldProphetUtility.YieldProphetFromFile(cropReportFileName);

            // Create an APSIM spec
            List<APSIMSpec> simulations = YieldProphetToAPSIM.ToAPSIM(spec);

            Assert.AreEqual(simulations.Count, 5);
            Assert.AreEqual(simulations[0].Name, "ThisYear");
            Assert.AreEqual(simulations[1].Name, "Base");
            Assert.AreEqual(simulations[2].Name, "NUnlimited");
            Assert.AreEqual(simulations[3].Name, "NUnlimitedFromToday");
            Assert.AreEqual(simulations[4].Name, "Next10DaysDry");
            Assert.AreEqual(simulations[0].StartDate, new DateTime(2014, 3, 25));
            Assert.AreEqual(simulations[0].EndDate, new DateTime(2014, 6, 1));
            Assert.AreEqual(simulations[0].Management.Count, 8);
            Assert.IsTrue(simulations[0].Management[0] is Sow);
            Assert.AreEqual((simulations[0].Management[0] as Sow).Date, new DateTime(2014, 4, 1));
            Assert.AreEqual((simulations[0].Management[0] as Sow).Crop, "Wheat");
            Assert.IsTrue(simulations[0].Management[1] is Fertilise);
            Assert.IsTrue(simulations[0].Management[2] is Fertilise);
            Assert.IsTrue(simulations[0].Management[3] is Fertilise);
            Assert.IsTrue(simulations[0].Management[4] is Tillage);
            Assert.IsTrue(simulations[0].Management[5] is ResetWater);
            Assert.IsTrue(simulations[0].Management[6] is ResetSurfaceOrganicMatter);
            Assert.IsTrue(simulations[0].Management[7] is ResetNitrogen);
            Assert.AreEqual(simulations[0].ObservedData, null);
        }

        /// <summary>Tests the ability to create and run the apsim files for a given specification</summary>
        [TestMethod]
        public void RunAPSIMWheat()
        {
            RunAPSIM(Path.Combine(filesDirectory, "CropReport.xml"));
        }

        /// <summary>Tests the ability to create and run the apsim files for a given specification</summary>
        [TestMethod]
        public void RunAPSIMSorghum()
        {
            RunAPSIM(Path.Combine(filesDirectory, "Sorghum.xml"));
        }


        
        //[TestMethod]
        //public void TestSowingOpportunityReport()
        //{
        //    // NB: This is a real YP validation XML file from 2004.
        //    string sourceApsimFileName = Path.Combine(filesDirectory, "SowingOpportunity.xml");

        //    // Copy the rainfall file to the working directory.
        //    string rainFileName = Path.Combine(filesDirectory, "CropReport.rain");
        //    Utility.ApsimTextFile rainFile = new Utility.ApsimTextFile();
        //    rainFile.Open(rainFileName);
        //    DataTable rainData = rainFile.ToTable();
        //    rainFile.Close();

        //    // Get the YP job XML
        //    string xml = GetFileContents(sourceApsimFileName);

        //    YieldProphetServices.Run(xml, rainData, new DateTime(2014, 1, 1), workingDirectory);

        //    // Make sure we got an output file.
        //    string outputFileName = Path.Combine(workingDirectory, "Base Yearly.out");
        //    Utility.ApsimTextFile outputFile = new Utility.ApsimTextFile();
        //    outputFile.Open(outputFileName);
        //    DataTable outputData = outputFile.ToTable();
        //    outputFile.Close();

        //    // Test some numbers in the output file.
        //    Assert.AreEqual(outputData.Rows.Count, 30);
        //    Assert.IsTrue(Convert.ToDouble(outputData.Rows[0]["Biomass"]) > 6000);
        //}

    }
}
