using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace UnitTestProject1
{
    [TestClass]
    public class YPToAPSIMSpecTests
    {
        [TestMethod]
        public void YPToAPSIMSpecTests_EnsureSingleSeasonWorks()
        {
            YieldProphet yp = new YieldProphet()
            {
                Paddock = new List<Paddock>()
                {
                    new Paddock()
                    {
                        Name = "NameOfPaddock",
                        StartSeasonDate = new DateTime(2016, 4, 1),
                        NowDate = new DateTime(2016, 7, 1),
                        SoilWaterSampleDate = new DateTime(2016, 3, 1),
                        SoilNitrogenSampleDate = new DateTime(2016, 6, 1),
                        StationNumber = 41023,
                        StationName = "Toowoomba",
                        RunType = Paddock.RunTypeEnum.SingleSeason,
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
                                SWUnits = Sample.SWUnitsEnum.Gravimetric
                            }
                        },
                        SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)",
                        Management = new List<Management>()
                        {
                            new Sow()
                            {
                                Crop = "Wheat",
                                Date = new DateTime(2016, 5, 1)
                            }
                        }
                    }
                }
            };

            List<APSIMSpecification> simulations = YieldProphetToAPSIM.ToAPSIM(yp);

            Assert.AreEqual(simulations.Count, 1);
            Assert.AreEqual(simulations[0].Name, "NameOfPaddock");
            Assert.AreEqual(simulations[0].StartDate, new DateTime(2016, 3, 1));
            Assert.AreEqual(simulations[0].EndDate, new DateTime(2016, 6, 30));
            Assert.AreEqual(simulations[0].RunType, APSIMSpecification.RunTypeEnum.Normal);
            Assert.AreEqual(simulations[0].Management.Count, 5);
            Assert.IsTrue(simulations[0].Management[0] is ResetWater);
            Assert.AreEqual(simulations[0].Management[0].Date, new DateTime(2016, 3, 1));
            Assert.IsTrue(simulations[0].Management[1] is ResetSurfaceOrganicMatter);
            Assert.AreEqual(simulations[0].Management[1].Date, new DateTime(2016, 3, 1));
            Assert.IsTrue(simulations[0].Management[2] is ResetNitrogen);
            Assert.AreEqual(simulations[0].Management[2].Date, new DateTime(2016, 5, 1));
            Assert.IsTrue(simulations[0].Management[3] is Sow);
            Assert.AreEqual(simulations[0].Management[3].Date, new DateTime(2016, 5, 1));
            Assert.IsTrue(simulations[0].Management[4] is ResetNitrogen);
            Assert.AreEqual(simulations[0].Management[4].Date, new DateTime(2016, 6, 1));

            Assert.IsNotNull(simulations[0].SoilPath);
            Assert.AreEqual(simulations[0].Samples.Count, 1);
            Assert.AreEqual(simulations[0].Samples[0].Thickness, yp.Paddock[0].Samples[0].Thickness);
            Assert.AreEqual(simulations[0].Samples[0].NO3, yp.Paddock[0].Samples[0].NO3);
            Assert.AreEqual(simulations[0].Samples[0].NH4, yp.Paddock[0].Samples[0].NH4);
            Assert.AreEqual(simulations[0].Samples[0].SW, yp.Paddock[0].Samples[0].SW);
            Assert.AreEqual(simulations[0].Samples[0].SWUnits, Sample.SWUnitsEnum.Gravimetric);
        }

        [TestMethod]
        public void YPToAPSIMSpecTests_EnsureLongtermWorks()
        {
            YieldProphet yp = new YieldProphet()
            {
                Paddock = new List<Paddock>()
                {
                    new Paddock()
                    {
                        Name = "NameOfPaddock",
                        StartSeasonDate = new DateTime(2016, 4, 1),
                        NowDate = new DateTime(2016, 7, 1),
                        SoilWaterSampleDate = new DateTime(2016, 3, 1),
                        SoilNitrogenSampleDate = new DateTime(2016, 6, 1),
                        StationNumber = 41023,
                        StationName = "Toowoomba",
                        RunType = Paddock.RunTypeEnum.LongTerm,
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
                                SWUnits = Sample.SWUnitsEnum.Gravimetric
                            }
                        },
                        SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)",
                        Management = new List<Management>()
                        {
                            new Sow()
                            {
                                Crop = "Wheat",
                                Date = new DateTime(2016, 5, 1)
                            }
                        }
                    }
                }
            };

            List<APSIMSpecification> simulations = YieldProphetToAPSIM.ToAPSIM(yp);

            Assert.AreEqual(simulations.Count, 1);
            Assert.AreEqual(simulations[0].Name, "NameOfPaddock");
            Assert.AreEqual(simulations[0].StartDate, new DateTime(1957, 1, 1));
            Assert.AreEqual(simulations[0].EndDate, new DateTime(2016, 6, 30));
            Assert.AreEqual(simulations[0].RunType, APSIMSpecification.RunTypeEnum.Normal);
            Assert.AreEqual(simulations[0].Management.Count, 5);
            Assert.IsTrue(simulations[0].Management[0] is ResetWater);
            Assert.AreEqual(simulations[0].Management[0].Date, new DateTime(2016, 3, 1));
            Assert.IsTrue(simulations[0].Management[0].IsEveryYear);
            Assert.IsTrue(simulations[0].Management[1] is ResetSurfaceOrganicMatter);
            Assert.AreEqual(simulations[0].Management[1].Date, new DateTime(2016, 3, 1));
            Assert.IsTrue(simulations[0].Management[1].IsEveryYear);
            Assert.IsTrue(simulations[0].Management[2] is ResetNitrogen);
            Assert.AreEqual(simulations[0].Management[2].Date, new DateTime(2016, 5, 1));
            Assert.IsTrue(simulations[0].Management[2].IsEveryYear);
            Assert.IsTrue(simulations[0].Management[3] is Sow);
            Assert.AreEqual(simulations[0].Management[3].Date, new DateTime(2016, 5, 1));
            Assert.IsTrue(simulations[0].Management[3].IsEveryYear);
            Assert.IsTrue(simulations[0].Management[4] is ResetNitrogen);
            Assert.AreEqual(simulations[0].Management[4].Date, new DateTime(2016, 6, 1));
            Assert.IsTrue(simulations[0].Management[4].IsEveryYear);

            Assert.IsNotNull(simulations[0].SoilPath);
            Assert.AreEqual(simulations[0].Samples.Count, 1);
            Assert.AreEqual(simulations[0].Samples[0].Thickness, yp.Paddock[0].Samples[0].Thickness);
            Assert.AreEqual(simulations[0].Samples[0].NO3, yp.Paddock[0].Samples[0].NO3);
            Assert.AreEqual(simulations[0].Samples[0].NH4, yp.Paddock[0].Samples[0].NH4);
            Assert.AreEqual(simulations[0].Samples[0].SW, yp.Paddock[0].Samples[0].SW);
            Assert.AreEqual(simulations[0].Samples[0].SWUnits, Sample.SWUnitsEnum.Gravimetric);
        }

        [TestMethod]
        public void APSIMSpecificationTests_CreateJobFromApsimSpecXML()
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

            List<APSIMSpecification> simulations = new List<APSIMSpecification>()
            {
                spec
            };
            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMRevision = "Apsim7.8-R4013"
            };

            string xml = XmlUtilities.Serialise(simulations, true);
            RunYPJob job = new RunYPJob(xml, environment);
            Assert.IsNotNull(job.GetNextJobToRun());
        }

    }
}
