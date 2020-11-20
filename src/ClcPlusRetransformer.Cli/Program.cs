// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Core;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using Serilog;

	public sealed class Program
	{
		public static void Main()
		{
			IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

			IConfigurationRoot config = builder.Build();

			ServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(x => x.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger()));

			// Logging should be configured in appsettings. Cannot publish single file unless this is resolved:
			// https://github.com/serilog/serilog-settings-configuration/issues/239
			////serviceCollection.AddLogging(loggingBuilder =>
			////{
			////	loggingBuilder.AddSerilog(new LoggerConfiguration().ReadFrom.Configuration(config.GetSection("Logging")).CreateLogger());
			////});

			serviceCollection.AddTransient(typeof(Processor<>));
			serviceCollection.AddTransient(typeof(ChainedProcessor<,>));
			serviceCollection.AddTransient<ProcessorFactory>();

			ServiceProvider provider = serviceCollection.BuildServiceProvider();

			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

			string aoiFileName = config["AoiFileName"];

			string baselineFileName = config["BaselineFileName"];
			string hardboneFileName = config["HardboneFileName"];
			string backboneFileName = config["BackboneFileName"];

			////(string fileName, Type geometryType)[] files =
			////{
			////	(baselineFileName, typeof(LineString)), (hardboneFileName, typeof(Polygon)), (backboneFileName, typeof(Polygon)),
			////};

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			IProcessor<Polygon> aoiProcessor = provider.Load<Polygon>(aoiFileName);
			IProcessor<Polygon> aoiProcessorBuffered = aoiProcessor.Buffer(100);

			Polygon aoi = aoiProcessor.Execute().Single();
			Polygon bufferedAoi = aoiProcessorBuffered.Execute().Single();

			IProcessor<LineString> baselineProcessor = provider.Load<LineString>(baselineFileName).Clip(bufferedAoi);
			IProcessor<LineString> hardboneProcessor =
				provider.Load<Polygon>(hardboneFileName).Clip(bufferedAoi).PolygonsToLines().Dissolve();
			IProcessor<LineString> backboneProcessor =
				provider.Load<Polygon>(backboneFileName).Clip(bufferedAoi).PolygonsToLines().Dissolve();

			Task.Run(() => baselineProcessor.Execute());
			Task.Run(() => hardboneProcessor.Execute());
			Task.Run(() => backboneProcessor.Execute());

			IProcessor<LineString> difference = backboneProcessor.Difference(hardboneProcessor.Execute()).Union().Dissolve();

			IProcessor<LineString> smoothed = difference.Smooth();

			////smoothed.Execute().Save(@"data/results/new_smoothed.shp");

			////smoothed.Dissolve().Execute().Save(@"data/results/old_smoothed_dissolved.shp");

			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());

			////smoothedAndSnapped.Execute().Save(@"data/results/new_with_old_snapped.shp");

			smoothedAndSnapped.Merge(baselineProcessor.Execute())
				.Merge(aoiProcessor.PolygonsToLines().Execute())
				.Polygonize()
				.EliminatePolygons()
				.Clip(aoi)
				.Execute()
				.Save(config["OutputFileName"]);

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);
		}
	}
}