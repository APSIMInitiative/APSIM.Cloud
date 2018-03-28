using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace UnitTestProject1
{
    [TestClass]
    public class APSIMSpecificationTests
    {
        [TestMethod]
        public void APSIMSpecificationTests_SWBoundedtoCLL()
        {
            APSIMSpecification spec = new APSIMSpecification()
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2016, 12, 1),
                NowDate = new DateTime(2016, 7, 1),
                StationNumber = 41023,
                RunType = APSIMSpecification.RunTypeEnum.Normal,
                StubbleMass = 100,
                StubbleType = "Wheat",
                Samples = new List<Sample>()
                {
                    new Sample()
                    {
                        Thickness = new double[] { 100, 300, 300, 300 },
                        NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                        NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                        SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                        SWUnits = Sample.SWUnitsEnum.Gravimetric,
                    }
                },
                SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)",
                Management = new List<Management>()
                {
                    new Sow()
                    {
                    Crop = "Wheat",
                    Date = new DateTime(2016, 5, 1),
                    }
                }
            };  

            string workingDirectory = Path.GetTempFileName();
            File.Delete(workingDirectory);
            Directory.CreateDirectory(workingDirectory);
            string apsimFileName = APSIMFiles.Create(spec, workingDirectory, "test.apsim");

            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);

            // The third layer SW should be changed.
            XmlNode swNode = XmlUtilities.Find(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/Sample/SW");
            double[] SW = MathUtilities.StringsToDoubles(XmlUtilities.Values(swNode, "double"));

            Assert.AreEqual(SW[0], 0.13, 0.0001);
            Assert.AreEqual(SW[1], 0.18, 0.0001);
            Assert.AreEqual(SW[2], 0.21880064829821716, 0.0001); // bounded to CLL
            Assert.AreEqual(SW[3], 0.24, 0.0001);

            Directory.Delete(workingDirectory, recursive: true);
        }

        [TestMethod]
        public void APSIMSpecificationTestss_ConaUCorrected()
        {
            // Using a northern soil in southern australia should change the 
            // CONA and U values to southern australian ones.

            APSIMSpecification spec = new APSIMSpecification()
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2016, 12, 1),
                NowDate = new DateTime(2016, 7, 1),
                StationNumber = 77007,   // Birchip post office - victoria.
                RunType = APSIMSpecification.RunTypeEnum.Normal,
                StubbleMass = 100,
                StubbleType = "Wheat",
                Samples = new List<Sample>()
                {
                    new Sample()
                    {
                        Thickness = new double[] { 100, 300, 300, 300 },
                        NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                        NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                        SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                        SWUnits = Sample.SWUnitsEnum.Gravimetric,
                    }
                },
                SoilPath = "Soils/Australia/Queensland/Darling Downs and Granite Belt/Black Vertosol (Formartin No622-YP)",
                Management = new List<Management>()
                {
                    new Sow()
                    {
                    Crop = "Wheat",
                    Date = new DateTime(2016, 5, 1),
                    }
                }
            };

            string workingDirectory = Path.GetTempFileName();
            File.Delete(workingDirectory);
            Directory.CreateDirectory(workingDirectory);
            string apsimFileName = APSIMFiles.Create(spec, workingDirectory, "test.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);

            // CONA and U should be for southern australia
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/SoilWater/SummerU"), "6");
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/SoilWater/SummerCona"), "3.5");
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/SoilWater/WinterU"), "2");
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/SoilWater/WinterCona"), "2");

            Directory.Delete(workingDirectory, recursive:true);
        }

        [TestMethod]
        public void APSIMSpecificationTests_SoilLandscapeGrid()
        {
            // Make sure we can reference a soil and landscape grid soil.

            APSIMSpecification spec = new APSIMSpecification()
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2016, 12, 1),
                NowDate = new DateTime(2016, 7, 1),
                StationNumber = 77007,   // Birchip post office - victoria.
                RunType = APSIMSpecification.RunTypeEnum.Normal,
                StubbleMass = 100,
                StubbleType = "Wheat",
                Samples = new List<Sample>()
                {
                    new Sample()
                    {
                        Thickness = new double[] { 100, 300, 300, 300 },
                        NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                        NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                        SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                        SWUnits = Sample.SWUnitsEnum.Gravimetric,
                    }
                },
                SoilPath = "http://ternsoils.nexus.csiro.au:8080/ASRISApi/api/APSIM/getApsoilTypeMap?longitude=147&latitude=-29.5&numToReturn=0",
                Management = new List<Management>()
                {
                    new Sow()
                    {
                    Crop = "Wheat",
                    Date = new DateTime(2016, 5, 1),
                    }
                }
            };

            string workingDirectory = Path.GetTempFileName();
            File.Delete(workingDirectory);
            Directory.CreateDirectory(workingDirectory);
            string apsimFileName = APSIMFiles.Create(spec, workingDirectory, "test.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Paddock/Soil/NearestTown"), "Walgett, NSW 2400");

            Directory.Delete(workingDirectory, recursive: true);
        }

        [TestMethod]
        public void APSIMSpecificationTests_MultiYearRunNormal()
        {
            // Do a normal multi year run

            APSIMSpecification spec = new APSIMSpecification()
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2017, 12, 1),
                NowDate = new DateTime(2017, 7, 1),
                StationNumber = 77007,   // Birchip post office - victoria.
                RunType = APSIMSpecification.RunTypeEnum.Normal,
                StubbleMass = 100,
                StubbleType = "Wheat",
                Samples = new List<Sample>()
                {
                    new Sample()
                    {
                        Thickness = new double[] { 100, 300, 300, 300 },
                        NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                        NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                        SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                        SWUnits = Sample.SWUnitsEnum.Gravimetric,
                    }
                },
                SoilPath = "Soils/Australia/Queensland/Darling Downs and Granite Belt/Black Vertosol (Formartin No622-YP)",
                Management = new List<Management>()
                {
                    new Sow()
                    {
                        Crop = "Wheat",
                        Date = new DateTime(2016, 5, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2016, 5, 1),
                        Amount = 50
                    },
                    new ResetWater()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new ResetNitrogen()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new ResetSurfaceOrganicMatter()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2016, 8, 1),
                        Amount = 55
                    },
                    new Sow()
                    {
                        Crop = "Barley",
                        Date = new DateTime(2017, 6, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2017, 6, 1),
                        Amount = 50
                    }
                }
            };

            string workingDirectory = Path.GetTempFileName();
            File.Delete(workingDirectory);
            Directory.CreateDirectory(workingDirectory);
            string apsimFileName = APSIMFiles.Create(spec, workingDirectory, "test.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);

            // Check the start date and end date.
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Clock/start_date"), "1/04/2016");
            Assert.AreEqual(XmlUtilities.Value(doc.DocumentElement, "NameOfPaddock/Clock/end_date"), "1/12/2017");

            // Check operations
            XmlNode operationsNode = XmlUtilities.Find(doc.DocumentElement, "NameOfPaddock/Paddock/Operations");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[0], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[0], "action"), "Wheat sow plants = 0, sowing_depth = 50, cultivar = , row_spacing = 250, crop_class = plant, skip = solid");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[1], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[1], "action"), "SurfaceOM tillage type = planter, f_incorp = 0.1, tillage_depth = 50");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[2], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[2], "action"), "Fertiliser apply amount = 50 (kg/ha), depth = 20 (mm), type = no3_n");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[3], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[3], "action"), "'Soil Water' reset");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[4], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[4], "action"), "act_mods reseting");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[5], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[5], "action"), "'Soil Nitrogen' reset");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[6], "date"), "01-May-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[6], "action"), "SurfaceOM reset");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[7], "date"), "01-Aug-2016");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[7], "action"), "Fertiliser apply amount = 55 (kg/ha), depth = 20 (mm), type = no3_n");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[8], "date"), "01-Jun-2017");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[8], "action"), "Barley sow plants = 0, sowing_depth = 50, cultivar = , row_spacing = 250, crop_class = plant, skip = solid");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[9], "date"), "01-Jun-2017");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[9], "action"), "SurfaceOM tillage type = planter, f_incorp = 0.1, tillage_depth = 50");

            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[10], "date"), "01-Jun-2017");
            Assert.AreEqual(XmlUtilities.Value(operationsNode.ChildNodes[10], "action"), "Fertiliser apply amount = 50 (kg/ha), depth = 20 (mm), type = no3_n");

            // Check that 2 crops are in the simulation
            Assert.IsNotNull(XmlUtilities.Find(doc.DocumentElement, "NameOfPaddock/Paddock/Wheat"));
            Assert.IsNotNull(XmlUtilities.Find(doc.DocumentElement, "NameOfPaddock/Paddock/Barley"));

            Directory.Delete(workingDirectory, recursive: true);

        }

        [TestMethod]
        public void APSIMSpecificationTests_MultipleSimulations()
        {
            // Do a normal single simulation and a long term patched run
            // This mimics a crop report.
            // Make sure the factorial factors are right. We have had the bug
            // where ThisYearDaily data isn't produced - sim wasn't run because
            // it wasn't in factorial factors.

            APSIMSpecification shortSimSpec = CreateAPSIMSpecification();
            APSIMSpecification longtermPatchedSim = CreateAPSIMSpecification();
            longtermPatchedSim.Name = "NameOfPaddock2";
            longtermPatchedSim.RunType = APSIMSpecification.RunTypeEnum.LongTermPatched;
            longtermPatchedSim.LongtermStartYear = 1957;

            List<APSIMSpecification> simulations = new List<APSIMSpecification>()
            {
                shortSimSpec,
                longtermPatchedSim
            };

            string workingDirectory = Path.GetTempFileName();
            File.Delete(workingDirectory);
            Directory.CreateDirectory(workingDirectory);
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory, "test.apsim");
            XmlDocument doc = new XmlDocument();
            doc.Load(apsimFileName);

            // Check the factorial factors
            XmlNode factorialsNode = XmlUtilities.Find(doc.DocumentElement, "Factorials");
            Assert.AreEqual(XmlUtilities.Value(factorialsNode, "active"), "1");
            Assert.AreEqual(XmlUtilities.Value(factorialsNode.ChildNodes[1], "targets/Target"), "/Simulations/NameOfPaddock/Met");
            Assert.AreEqual(XmlUtilities.Value(factorialsNode.ChildNodes[2], "targets/Target"), "/Simulations/NameOfPaddock2/Met");

            Directory.Delete(workingDirectory, recursive: true);

        }

        private static APSIMSpecification CreateAPSIMSpecification()
        {
            APSIMSpecification spec = new APSIMSpecification()
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2017, 12, 1),
                NowDate = new DateTime(2017, 7, 1),
                StationNumber = 77007,   // Birchip post office - victoria.
                RunType = APSIMSpecification.RunTypeEnum.Normal,
                StubbleMass = 100,
                StubbleType = "Wheat",
                ObservedData = new System.Data.DataTable("ObsData"),
                Samples = new List<Sample>()
                {
                    new Sample()
                    {
                        Thickness = new double[] { 100, 300, 300, 300 },
                        NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                        NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                        SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                        SWUnits = Sample.SWUnitsEnum.Gravimetric,
                    }
                },
                SoilPath = "Soils/Australia/Queensland/Darling Downs and Granite Belt/Black Vertosol (Formartin No622-YP)",
                Management = new List<Management>()
                {
                    new Sow()
                    {
                        Crop = "Wheat",
                        Date = new DateTime(2016, 5, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2016, 5, 1),
                        Amount = 50
                    },
                    new ResetWater()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new ResetNitrogen()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new ResetSurfaceOrganicMatter()
                    {
                        Date = new DateTime(2016, 5, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2016, 8, 1),
                        Amount = 55
                    },
                    new Sow()
                    {
                        Crop = "Barley",
                        Date = new DateTime(2017, 6, 1),
                    },
                    new Fertilise()
                    {
                        Date = new DateTime(2017, 6, 1),
                        Amount = 50
                    }
                }
            };
            spec.ObservedData.Columns.Add("Date", typeof(DateTime));
            return spec;
        }
    }
}
