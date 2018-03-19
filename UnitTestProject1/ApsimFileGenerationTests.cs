using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace UnitTestProject1
{
    [TestClass]
    public class ApsimFileGenerationTests
    {
        [TestMethod]
        public void EnsureApsimFileGenerationWorks()
        {
            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(GetDefaultSimulationSpec());

            // Create a working directory.
            string workingDirectory = Path.Combine(Path.GetTempPath(), "UnitTests");
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
            Directory.CreateDirectory(workingDirectory);

            // Create all the files needed to run APSIM.
            string apsimFileName = APSIMFiles.Create(simulations, workingDirectory, "test.apsim");
            Assert.IsTrue(File.Exists(Path.Combine(workingDirectory, "NameOfPaddock.met")));
            Assert.IsTrue(File.Exists(Path.Combine(workingDirectory, "test.apsim")));
            Assert.IsTrue(File.Exists(Path.Combine(workingDirectory, "test.spec")));

            // Get rid of working directory.
            Directory.Delete(workingDirectory, true);
        }

        [TestMethod]
        public void BuildAPSIMRuntimeEnvironmentWithRuntimePackage()
        {
            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string runtimeDirectory = Path.Combine(binDirectory, "Apsim7.8-R4000-Testing");

            // Remove old runtime directory if it exists.
            if (Directory.Exists(runtimeDirectory))
                Directory.Delete(runtimeDirectory, true);

            // Build the new runtime environment.
            APSIMSpec simulation = GetDefaultSimulationSpec();
            List<APSIMSpec> simulations = new List<APSIMSpec>();
            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMRevision = "Apsim7.8-R4000",
                RuntimePackages = new string[] { "Testing" }
            };
            simulations.Add(simulation);
            RunYPJob job = new RunYPJob(simulations, environment);

            // Make sure it is built correctly.
            Assert.IsTrue(Directory.Exists(runtimeDirectory));
            string apsimExe = Path.Combine(runtimeDirectory, "Temp", "Model", "Apsim.exe");
            Assert.IsTrue(File.Exists(apsimExe));

            string testFileName = Path.Combine(runtimeDirectory, "Temp", "Model", "Test.txt");
            Assert.AreEqual(File.ReadAllText(testFileName), "Test contents");

            // Remove newly created runtime directory
            Directory.Delete(runtimeDirectory, true);
        }

        [TestMethod]
        public void BuildAPSIMXRuntimeEnvironmentWithRuntimePackage()
        {
            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string runtimeDirectory = Path.Combine(binDirectory, "ApsimX-2473");

            // Remove old runtime directory if it exists.
            if (Directory.Exists(runtimeDirectory))
                Directory.Delete(runtimeDirectory, true);

            // Build the new runtime environment.
            APSIMSpec simulation = GetDefaultSimulationSpec();
            List<APSIMSpec> simulations = new List<APSIMSpec>();
            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMxBuildNumber = 2473 // issue number that was resovled.
            };
            simulations.Add(simulation);
            RunYPJob job = new RunYPJob(simulations, environment);

            // Make sure it is built correctly.
            Assert.IsTrue(Directory.Exists(runtimeDirectory));
            string modelsExe = Path.Combine(runtimeDirectory, "Bin", "Models.exe");
            Assert.IsTrue(File.Exists(modelsExe));

            // Remove newly created runtime directory
            Directory.Delete(runtimeDirectory, true);
        }

        [TestMethod]
        public void BuildAusfarmRuntimeEnvironment()
        {
            string binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string runtimeDirectory = Path.Combine(binDirectory, "AusFarm-1.4.12");

            // Remove old runtime directory if it exists.
            if (Directory.Exists(runtimeDirectory))
                Directory.Delete(runtimeDirectory, true);

            // Build the new runtime environment.
            Farm4Prophet f4p = GetDefaultF4PSimulationSpec();
            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                AusfarmRevision = "AusFarm-1.4.12",
            };
            RunF4PJob job = new RunF4PJob(f4p, environment);

            // Make sure it is built correctly.
            Assert.IsTrue(Directory.Exists(runtimeDirectory));
            string ausfarmExe = Path.Combine(runtimeDirectory, "Ausfarm", "ausfarm.exe");
            Assert.IsTrue(File.Exists(ausfarmExe));

            // Remove newly created runtime directory
            Directory.Delete(runtimeDirectory, true);
        }

        [TestMethod]
        public void RunAPSIMGetOutputs()
        {
            APSIMSpec simulation = GetDefaultSimulationSpec();

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMRevision = "Apsim7.8-R4013"
            };

            RunYPJob job = new RunYPJob(simulations, environment);
            IJobRunner runner = new JobRunnerAsync();
            runner.Run(job, wait: true);

            // Make sure we don't have an error.
            Assert.AreEqual(job.Errors.Count, 0);

            // Make sure we have a daily output table.
            Assert.AreEqual(job.Outputs.Tables.Count, 3);
            Assert.AreEqual(job.Outputs.Tables[0].TableName, "Summary");

            Assert.AreEqual(job.Outputs.Tables[1].TableName, "YieldProphetDaily");
            Assert.AreEqual(job.Outputs.Tables[1].Rows.Count, 92);
            double[] biomass = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[1], "biomass");
            Assert.IsTrue(MathUtilities.Max(biomass) > 20.0); // make sure something is growing.

            // Make sure we have a depth table.
            Assert.AreEqual(job.Outputs.Tables[2].TableName, "YieldProphetDepth");
            Assert.AreEqual(job.Outputs.Tables[2].Rows.Count, 8);
            double[] sw = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[2], "sw");
            Assert.IsTrue(MathUtilities.Max(sw) > 0.0); // make sure there are sw values
        }

        [TestMethod]
        public void RunAPSIMXGetOutputs()
        {
            APSIMSpec simulation = GetDefaultSimulationSpec();

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMxBuildNumber = 2473 // issue number that was resovled.
            };

            RunYPJob job = new RunYPJob(simulations, environment);
            IJobRunner runner = new JobRunnerAsync();
            runner.Run(job, wait: true);

            // Make sure we don't have an error.
            Assert.AreEqual(job.Errors.Count, 0);

            // Make sure we have a daily output table.
            Assert.AreEqual(job.Outputs.Tables.Count, 1);
            Assert.AreEqual(job.Outputs.Tables[0].TableName, "Daily");

            double[] biomass = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[0], "Wheat.Aboveground.Wt");
            Assert.IsTrue(MathUtilities.Max(biomass) > 5); // make sure something is growing.
        }

        [TestMethod]
        public void RunAPSIMGetError()
        {
            APSIMSpec simulation = GetDefaultSimulationSpec();
            (simulation.Management[0] as Sow).Cultivar = string.Empty;

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                APSIMRevision = "Apsim7.8-R4013"
            };

            RunYPJob job = new RunYPJob(simulations, environment);
            IJobRunner runner = new JobRunnerAsync();
            runner.Run(job, wait: true);

            // Make sure we have an error.
            Assert.AreEqual(job.Errors.Count, 1);
            Assert.AreEqual(job.Errors[0], "Cannot sow plant - : Cultivar not specified\r\n     Component name: Paddock.Wheat");
        }

        [TestMethod]
        public void RunAusfarmGetOutputs()
        {
            RuntimeEnvironment environment = new RuntimeEnvironment
            {
                AusfarmRevision = "AusFarm-1.4.12",
            };

            RunF4PJob job = new RunF4PJob(GetDefaultF4PSimulationSpec(), environment);
            IJobRunner runner = new JobRunnerAsync();
            runner.Run(job, wait: true);

            // Make sure we don't have an error.
            Assert.AreEqual(job.Errors.Count, 0);

            // Make sure we have a daily output table.
           //Assert.AreEqual(job.Outputs.Tables.Count, 3);
           //Assert.AreEqual(job.Outputs.Tables[0].TableName, "Summary");
           //
           //Assert.AreEqual(job.Outputs.Tables[1].TableName, "YieldProphetDaily");
           //Assert.AreEqual(job.Outputs.Tables[1].Rows.Count, 92);
           //double[] biomass = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[1], "biomass");
           //Assert.IsTrue(MathUtilities.Max(biomass) > 20.0); // make sure something is growing.
           //
           //// Make sure we have a depth table.
           //Assert.AreEqual(job.Outputs.Tables[2].TableName, "YieldProphetDepth");
           //Assert.AreEqual(job.Outputs.Tables[2].Rows.Count, 8);
           //double[] sw = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[2], "sw");
           //Assert.IsTrue(MathUtilities.Max(sw) > 0.0); // make sure there are sw values
        }


        private static APSIMSpec GetDefaultSimulationSpec()
        {
            Sample sample = new Sample
            {
                Thickness = new double[] { 100, 300, 300, 300 },
                NO3 = new double[] { 34, 6.9, 3.1, 1.8 },
                NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 },
                SW = new double[] { 0.13, 0.18, 0.20, 0.24 },
                SWUnits = Sample.SWUnitsEnum.Gravimetric
            };

            Sow sow = new Sow
            {
                Crop = "Wheat",
                Cultivar = "Hartog",
                RowSpacing = 100,
                SeedDepth = 30,
                SowingDensity = 100,
                Date = new DateTime(2016, 5, 1)
            };

            APSIMSpec simulation = new APSIMSpec
            {
                Name = "NameOfPaddock",
                StartDate = new DateTime(2016, 4, 1),
                EndDate = new DateTime(2016, 7, 1),
                NowDate = new DateTime(2016, 7, 1),
                TypeOfRun = Paddock.RunTypeEnum.SingleSeason,
                LongtermStartYear = 2000,
                DailyOutput = true,
                StationNumber = 41023,
                StubbleMass = 100,
                StubbleType = "Wheat",
                WriteDepthFile = true,
                Samples = new List<Sample>()
            };
            simulation.Samples.Add(sample);
            simulation.SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)";
            simulation.Management = new List<Management>();
            simulation.Management.Add(sow);
            return simulation;
        }

        private static Farm4Prophet GetDefaultF4PSimulationSpec()
        {
            Farm4Prophet simulation = new Farm4Prophet
            {
                TaskName = "NameOfPaddock",
                FarmList = new List<FarmSystem>()
            };

            //FlockDescr flock = new FlockDescr();
            //flock.BreedingEweCount = 10;
            //flock.BreedParams = new BreedParameters();
            //flock.BreedParams.

            FarmSystem farm = new FarmSystem();
            farm.Area = 10;
            farm.StationNumber = 41023;
            farm.Name = "TestFarm";
            farm.ReportName = "Test";
            farm.CroppingRegion = "Southern LRZ";
            //farm.LiveStock = new FarmLivestock();
            //farm.LiveStock.Flocks = new List<FlockDescr>();
            //farm.LiveStock.Flocks.Add(flock);
            farm.RunLength = 36;
            farm.SimTemplateType = SimulationType.stCropOnly;
            farm.StartSeasonDate = new DateTime(2015, 1, 1);
            simulation.FarmList.Add(farm);
            return simulation;
        }

    }
}
