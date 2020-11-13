// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Core;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using Serilog;

	internal class Program
	{
		private static async Task Main(string[] args)
		{
			ServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(x => x.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger()));
			serviceCollection.AddTransient(typeof(Processor<>));
			serviceCollection.AddTransient(typeof(ChainedProcessor<,>));
			serviceCollection.AddTransient<ProcessorFactory>();

			ServiceProvider provider = serviceCollection.BuildServiceProvider();

			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

			string aoiFileName = @"data\AOI_10k.shp";

			string baselineFileName = @"data\Hardbone_Baseline.shp";
			string hardboneFileName = @"data\Hardbone_Polygons.shp";
			string backboneFileName = @"data\Backbone_Polygons.shp";

			(string fileName, Type geometryType)[] files =
			{
				(baselineFileName, typeof(LineString)), (hardboneFileName, typeof(Polygon)), (backboneFileName, typeof(Polygon))
			};

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			Polygon otherGeometry = provider.Load<Polygon>(aoiFileName).Execute().Single();

			(ICollection<LineString> lineStrings, ICollection<Polygon> hardbones, ICollection<Polygon> backbones) = await When.All(
				Task.Run(() => provider.Load<LineString>(baselineFileName).Intersect(otherGeometry).Execute()),
				Task.Run(() => provider.Load<Polygon>(hardboneFileName).Intersect(otherGeometry).Execute()),
				Task.Run(() => provider.Load<Polygon>(backboneFileName).Intersect(otherGeometry).Execute()));

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);
		}
	}
}