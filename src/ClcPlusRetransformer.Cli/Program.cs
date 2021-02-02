// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
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
		public static async Task Main()
		{
			IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

			IConfigurationRoot config = builder.Build();

			ServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(loggingBuilder =>
				loggingBuilder.AddSerilog(new LoggerConfiguration().ReadFrom.Configuration(config.GetSection("Logging")).CreateLogger()));

			serviceCollection.AddTransient(typeof(Processor<>));
			serviceCollection.AddTransient(typeof(ChainedProcessor<,>));
			serviceCollection.AddTransient<ProcessorFactory>();

			ServiceProvider provider = serviceCollection.BuildServiceProvider();
			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

			PrecisionModel precisionModel = new PrecisionModel(10);

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
				provider.LoadFromFileAndClip<LineString>(baselineFileName, precisionModel, bufferedAoi, provider.GetRequiredService<ILogger<Processor>>());

			IProcessor<Polygon> hardboneProcessor = provider.LoadFromFileAndClip<Polygon>(hardboneFileName, precisionModel, bufferedAoi, provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> backboneProcessor = provider.LoadFromFileAndClip<Polygon>(backboneFileName, precisionModel, bufferedAoi, provider.GetRequiredService<ILogger<Processor>>());

			Task task1 = Task.Run(() => aoiProcessorBuffered.PolygonsToLines().Execute());
			Task task2 = Task.Run(() => baselineProcessor.Execute());
			Task task3 = Task.Run(() =>
			{
				try
				{
					hardboneProcessor.Execute();
				}
				catch (Exception)
				{
					logger.LogWarning("Loading and intersecting hardbones failed, falling back to Buffer(0)");

					hardboneProcessor = provider.LoadFromFile<Polygon>(hardboneFileName, precisionModel).Buffer(0).Clip(bufferedAoi);
					hardboneProcessor.Execute();
				}
			});

			Task task4 = Task.Run(() =>
			{
				try
				{
					backboneProcessor.Execute();
				}
				catch (Exception)
				{
					logger.LogWarning("Loading and intersecting backbones failed, falling back to Buffer(0)");

					backboneProcessor = provider.LoadFromFile<Polygon>(backboneFileName, precisionModel).Buffer(0).Clip(bufferedAoi);
					backboneProcessor.Execute();
				}
			});

			await Task.WhenAll(task1, task2, task3, task4).ConfigureAwait(true);

			IProcessor<LineString> hardboneProcessorLines = hardboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> backboneProcessorLines = backboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> difference = backboneProcessorLines
				.Difference(hardboneProcessorLines.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();

			IProcessor<LineString> smoothed = difference.Smooth();
			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());

			IProcessor<Polygon> polygonized = smoothedAndSnapped.Merge(baselineProcessor.Execute())
				.Merge(aoiProcessor.PolygonsToLines().Execute())
				.Node(new PrecisionModel(10000))
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			IProcessor<Polygon> cleanedAndClippedToAoi = polygonized.EliminatePolygons(provider.GetRequiredService<ILogger<Processor>>()).Clip(aoi);

			string projectionInfo = ShapeProjection.ReadProjectionInfo(aoiFileName);
			cleanedAndClippedToAoi.Execute().Save(config["OutputFileName"], projectionInfo);

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);

			if (bool.Parse(config["WaitForUserInputAfterCompletion"]))
			{
				Console.WriteLine("=== PRESS A KEY TO PROCEED ===");
				Console.ReadKey();
			}
		}
	}
}