// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Core;
	using ClcPlusRetransformer.Core.Processors;
	using ClcPlusRetransformer.Core.Processors.Extension;
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
			serviceCollection.AddLogging(x =>
				x.AddSerilog(new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger()));

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

			PrecisionModel precisionModel = new PrecisionModel(PrecisionModels.Floating);

			string aoiFileName = config["AoiFileName"];

			string baselineFileName = config["BaselineFileName"];
			string hardboneFileName = config["HardboneFileName"];
			string backboneFileName = config["BackboneFileName"];

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			IProcessor<Polygon> aoiProcessor = provider.LoadFromFile<Polygon>(aoiFileName, precisionModel);
			IProcessor<Polygon> aoiProcessorBuffered = aoiProcessor.Buffer(100);

			Polygon aoi = aoiProcessor.Execute().Single();
			Polygon bufferedAoi = aoiProcessorBuffered.Execute().Single();

			IProcessor<LineString> baselineProcessor =
				provider.LoadFromFileAndClip<LineString>(baselineFileName, precisionModel, bufferedAoi);
			IProcessor<LineString> hardboneProcessor = provider.LoadFromFileAndClip<Polygon>(hardboneFileName, precisionModel, bufferedAoi)
				.PolygonsToLines()
				.Dissolve();
			IProcessor<LineString> backboneProcessor = provider.LoadFromFileAndClip<Polygon>(backboneFileName, precisionModel, bufferedAoi)
				.PolygonsToLines()
				.Dissolve();

			Task.Run(() => aoiProcessorBuffered.PolygonsToLines().Execute());
			Task.Run(() => baselineProcessor.Execute());
			Task.Run(() => hardboneProcessor.Execute());
			Task.Run(() => backboneProcessor.Execute());

			IProcessor<LineString> difference = backboneProcessor
				.Difference(hardboneProcessor.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();

			IProcessor<LineString> smoothed = difference.Smooth();
			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());

			IProcessor<Polygon> polygonized = smoothedAndSnapped.Merge(baselineProcessor.Execute())
				.Merge(aoiProcessor.PolygonsToLines().Execute())
				.Node(new PrecisionModel(10000))
				.ReducePrecision(new PrecisionModel(1000))
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			IProcessor<Polygon> cleanedAndClippedToAoi = polygonized.EliminatePolygons().Clip(aoi);

			cleanedAndClippedToAoi.Execute().Save(config["OutputFileName"]);

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);
		}
	}
}