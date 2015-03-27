using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using APSIM.Cloud.Services;
using APSIM.Cloud.Services.Specification;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    /// <summary>
    /// Test class
    /// </summary>
    [TestClass]
    public class Test
    {
        /// <summary>The files directory</summary>
        private const string filesDirectory = @"C:\Users\hol353\Work\APSIM.Cloud\Test\Files";

        /// <summary>The working directory</summary>
        private const string workingDirectory = @"C:\Users\hol353\Work\APSIM.Cloud\Test\Generated";

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

        /// <summary>Get the contents of the specified file.</summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        private static string GetFileContents(string fileName)
        {
            StreamReader reader = new StreamReader(fileName);
            string newXMLContents = reader.ReadToEnd();
            reader.Close();
            return newXMLContents;
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
        //[TestMethod]
        public void TestConverter()
        {
            // Get our file name
            string fileName = Path.Combine(filesDirectory, "TestConverter.xml");
            
            // Create a YieldProphet object from our file.
            YieldProphet spec = YieldProphetServices.Create(GetFileContents(fileName));

            Assert.AreEqual(spec.PaddockList.Length, 1);
            Assert.AreEqual(spec.PaddockList[0].Name, "2011;mcvp;Grain Filling Baudin");
            Assert.AreEqual(spec.PaddockList[0].StartSeasonDate, new DateTime(2011, 4, 1));
            Assert.AreEqual(spec.PaddockList[0].Management.Count, 6);
            Assert.IsTrue(spec.PaddockList[0].Management[0] is Sow);
            Assert.AreEqual((spec.PaddockList[0].Management[0] as Sow).Date, new DateTime(2011, 6, 23));
            Assert.AreEqual((spec.PaddockList[0].Management[0] as Sow).Crop, "Barley");
            Assert.IsTrue(spec.PaddockList[0].Management[1] is Fertilise);
            Assert.IsTrue(spec.PaddockList[0].Management[2] is Fertilise);
            Assert.IsTrue(spec.PaddockList[0].Management[3] is Irrigate);
            Assert.IsTrue(spec.PaddockList[0].Management[4] is Tillage);
            Assert.IsTrue(spec.PaddockList[0].Management[5] is StubbleRemoved);
            Assert.AreEqual(spec.PaddockList[0].Samples.Count, 1);
        }

        /// <summary>Tests the ability to generate a single paddock apsim file. Used in validation</summary>
        //[TestMethod]
        public void TestValidation()
        {
            // NB: This is a real YP validation XML file from 2004.
            string validationFileName = Path.Combine(filesDirectory, "TestValidation.xml"); 

            // Copy the rainfall file to the working directory.
            string rainFileName = Path.Combine(filesDirectory, "TestValidation.rain");

            // Run the files.
            YieldProphetServices.Run(GetFileContents(validationFileName), 
                                     Utility.ApsimTextFile.ToTable(rainFileName), 
                                     new DateTime(2014, 12, 1), workingDirectory);

            // Make sure we got an output file.
            string outputFileName = Path.Combine(workingDirectory, "2004;A and R Weidemann;Wep 1 Yearly.out");
            Utility.ApsimTextFile outputFile = new Utility.ApsimTextFile();
            outputFile.Open(outputFileName);
            DataTable outputData = outputFile.ToTable();
            outputFile.Close();

            // Test some numbers in the output file.
            Assert.AreEqual(outputData.Rows.Count, 1);
            Assert.IsTrue(Convert.ToDouble(outputData.Rows[0]["Biomass"]) > 8000);
        }

        /// <summary>Tests the longterm weather file generation</summary>
        //[TestMethod]
        public void TestWeatherFile50Years()
        {
            // name of old style validation xml file.
            // NB: This is a real YP validation XML file from 2004.
            string fileName = Path.Combine(filesDirectory, "TestValidation.xml");

            // Create a YieldProphet object from our YP xml file
            YieldProphet spec = YieldProphetServices.Create(GetFileContents(fileName));

            // Read in the rainfall data
            Utility.ApsimTextFile observedDataFile = new Utility.ApsimTextFile();
            observedDataFile.Open(Path.Combine(filesDirectory, "TestValidation.rain"));
            DataTable observedData = observedDataFile.ToTable();
            observedDataFile.Close();

            // Write the weather file.
            string rainFileName = Path.Combine(workingDirectory, spec.PaddockList[0].Name) + ".met";
            WeatherFile weatherFiles = new WeatherFile();
            weatherFiles.CreateLongTerm(rainFileName, spec.PaddockList[0].StationNumber,
                                        spec.PaddockList[0].StartSeasonDate, 
                                        spec.PaddockList[0].StartSeasonDate.AddDays(300),
                                        new DateTime(2004, 6, 1),
                                        observedData, 50);

            Assert.AreEqual(weatherFiles.FilesCreated.Length, 50);

            Assert.AreEqual(Path.GetFileName(weatherFiles.FilesCreated[0]), "2004;A and R Weidemann;Wep 11954.met");
            Utility.ApsimTextFile weatherFile = new Utility.ApsimTextFile();
            weatherFile.Open(weatherFiles.FilesCreated[0]);
            DataTable weatherData = weatherFile.ToTable();
            weatherFile.Close();
            Assert.AreEqual(weatherData.Rows.Count, 301);
            Assert.AreEqual(weatherData.Rows[0]["codes"], "SSSO");
            Assert.AreEqual(weatherData.Rows[61]["codes"], "SSSO");
            Assert.AreEqual(weatherData.Rows[62]["codes"], "HHHH");
        }

        /// <summary>Tests the ability to generate a crop report</summary>
        //[TestMethod]
        public void TestCropReport()
        {
            // NB: This is a real YP validation XML file from 2004.
            string sourceApsimFileName = Path.Combine(filesDirectory, "CropReport.xml");
            
            // Copy the rainfall file to the working directory.
            string rainFileName = Path.Combine(filesDirectory, "CropReport.rain");
            Utility.ApsimTextFile rainFile = new Utility.ApsimTextFile();
            rainFile.Open(rainFileName);
            DataTable rainData = rainFile.ToTable();
            rainFile.Close();

            // Get the YP job XML
            string xml = GetFileContents(sourceApsimFileName);

            YieldProphetServices.Run(xml, rainData, new DateTime(2014, 6, 1), workingDirectory);

            // Make sure we got an output file.
            string outputFileName = Path.Combine(workingDirectory, "Base Yearly.out");
            Utility.ApsimTextFile outputFile = new Utility.ApsimTextFile();
            outputFile.Open(outputFileName);
            DataTable outputData = outputFile.ToTable();
            outputFile.Close();

            // Test some numbers in the output file.
            Assert.AreEqual(outputData.Rows.Count, 30);
            Assert.IsTrue(Convert.ToDouble(outputData.Rows[0]["Biomass"]) > 6000);
        }
        
        [TestMethod]
        public void TestSowingOpportunityReport()
        {
            // NB: This is a real YP validation XML file from 2004.
            string sourceApsimFileName = Path.Combine(filesDirectory, "SowingOpportunity.xml");

            // Copy the rainfall file to the working directory.
            string rainFileName = Path.Combine(filesDirectory, "CropReport.rain");
            Utility.ApsimTextFile rainFile = new Utility.ApsimTextFile();
            rainFile.Open(rainFileName);
            DataTable rainData = rainFile.ToTable();
            rainFile.Close();

            // Get the YP job XML
            string xml = GetFileContents(sourceApsimFileName);

            YieldProphetServices.Run(xml, rainData, new DateTime(2014, 1, 1), workingDirectory);

            // Make sure we got an output file.
            string outputFileName = Path.Combine(workingDirectory, "Base Yearly.out");
            Utility.ApsimTextFile outputFile = new Utility.ApsimTextFile();
            outputFile.Open(outputFileName);
            DataTable outputData = outputFile.ToTable();
            outputFile.Close();

            // Test some numbers in the output file.
            Assert.AreEqual(outputData.Rows.Count, 30);
            Assert.IsTrue(Convert.ToDouble(outputData.Rows[0]["Biomass"]) > 6000);
        }

    }
}
