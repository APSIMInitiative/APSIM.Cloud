using APSIM.Cloud.Runner.RunnableJobs;
using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class ApsimFileGenerationTests
    {
        [TestMethod]
        public void EnsureApsimFileGenerationWorks()
        {
            Sample sample = new Sample();
            sample.Thickness = new double[] { 100, 300, 300, 300 };
            sample.NO3 = new double[] { 34, 6.9, 3.1, 1.8 };
            sample.NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 };
            sample.SW = new double[] { 0.13, 0.18, 0.20, 0.24 };
            sample.SWUnits = Sample.SWUnitsEnum.Gravimetric;

            Sow sow = new Sow();
            sow.Crop = "Wheat";
            sow.Date = new DateTime(2016, 5, 1);

            APSIMSpec simulation = new APSIMSpec();
            simulation.Name = "NameOfPaddock";
            simulation.StartDate = new DateTime(2016, 4, 1);
            simulation.EndDate = new DateTime(2016, 7, 1);
            simulation.NowDate = new DateTime(2016, 7, 1);
            simulation.LongtermStartYear = 2000;
            simulation.StationNumber = 41023;
            simulation.StubbleMass = 100;
            simulation.StubbleType = "Wheat";
            simulation.Samples = new List<Sample>();
            simulation.Samples.Add(sample);
            simulation.SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)";
            simulation.Management = new List<Management>();
            simulation.Management.Add(sow);

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

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
        public void RunAPSIMGetError()
        {
            Sample sample = new Sample();
            sample.Thickness = new double[] { 100, 300, 300, 300 };
            sample.NO3 = new double[] { 34, 6.9, 3.1, 1.8 };
            sample.NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 };
            sample.SW = new double[] { 0.13, 0.18, 0.20, 0.24 };
            sample.SWUnits = Sample.SWUnitsEnum.Gravimetric;

            Sow sow = new Sow();
            sow.Crop = "Wheat";
            sow.Date = new DateTime(2016, 5, 1);

            APSIMSpec simulation = new APSIMSpec();
            simulation.Name = "NameOfPaddock";
            simulation.StartDate = new DateTime(2016, 4, 1);
            simulation.EndDate = new DateTime(2016, 7, 1);
            simulation.NowDate = new DateTime(2016, 7, 1);
            simulation.LongtermStartYear = 2000;
            simulation.StationNumber = 41023;
            simulation.StubbleMass = 100;
            simulation.StubbleType = "Wheat";
            simulation.Samples = new List<Sample>();
            simulation.Samples.Add(sample);
            simulation.SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)";
            simulation.Management = new List<Management>();
            simulation.Management.Add(sow);

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

            RunYPJob job = new RunYPJob(simulations);
            JobManager runner = new JobManager();
            runner.AddJob(job);
            runner.Start(waitUntilFinished:true);

            // Make sure we have an error.
            List<Exception> errors = runner.Errors(job);
            Assert.AreEqual(errors.Count, 1);
            Assert.AreEqual(errors[0].Message, "Cannot sow plant - : Cultivar not specified\r\n     Component name: Paddock.Wheat");
        }

        [TestMethod]
        public void RunAPSIMGetOutputs()
        {
            Sample sample = new Sample();
            sample.Thickness = new double[] { 100, 300, 300, 300 };
            sample.NO3 = new double[] { 34, 6.9, 3.1, 1.8 };
            sample.NH4 = new double[] { 5.5, 1.8, 1.8, 1.5 };
            sample.SW = new double[] { 0.13, 0.18, 0.20, 0.24 };
            sample.SWUnits = Sample.SWUnitsEnum.Gravimetric;

            Sow sow = new Sow();
            sow.Crop = "Wheat";
            sow.Cultivar = "Hartog";
            sow.RowSpacing = 100;
            sow.SeedDepth = 30;
            sow.SowingDensity = 100;
            sow.Date = new DateTime(2016, 5, 1);

            APSIMSpec simulation = new APSIMSpec();
            simulation.Name = "NameOfPaddock";
            simulation.StartDate = new DateTime(2016, 4, 1);
            simulation.EndDate = new DateTime(2016, 7, 1);
            simulation.NowDate = new DateTime(2016, 7, 1);
            simulation.TypeOfRun = Paddock.RunTypeEnum.SingleSeason;
            simulation.LongtermStartYear = 2000;
            simulation.DailyOutput = true;
            simulation.StationNumber = 41023;
            simulation.StubbleMass = 100;
            simulation.StubbleType = "Wheat";
            simulation.WriteDepthFile = true;
            simulation.Samples = new List<Sample>();
            simulation.Samples.Add(sample);
            simulation.SoilPath = "Soils/Australia/Victoria/Wimmera/Clay (Rupanyup North No742)";
            simulation.Management = new List<Management>();
            simulation.Management.Add(sow);

            List<APSIMSpec> simulations = new List<APSIMSpec>();
            simulations.Add(simulation);

            RunYPJob job = new RunYPJob(simulations);
            JobManager runner = new JobManager();
            runner.AddJob(job);
            runner.Start(waitUntilFinished: true);

            // Make sure we don't have an error.
            List<Exception> errors = runner.Errors(job);
            Assert.AreEqual(errors.Count, 0);

            // Make sure we have a daily output table.
            Assert.AreEqual(job.Outputs.Tables.Count, 2);
            Assert.AreEqual(job.Outputs.Tables[0].TableName, "YieldProphetDaily");
            Assert.AreEqual(job.Outputs.Tables[0].Rows.Count, 92);
            double[] biomass = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[0], "biomass");
            Assert.IsTrue(MathUtilities.Max(biomass) > 20.0); // make sure something is growing.

            // Make sure we have a depth table.
            Assert.AreEqual(job.Outputs.Tables[1].TableName, "YieldProphetDepth");
            Assert.AreEqual(job.Outputs.Tables[1].Rows.Count, 8);
            double[] sw = DataTableUtilities.GetColumnAsDoubles(job.Outputs.Tables[1], "sw");
            Assert.IsTrue(MathUtilities.Max(biomass) > 0.0); // make sure there are sw values
        }
    }
}
