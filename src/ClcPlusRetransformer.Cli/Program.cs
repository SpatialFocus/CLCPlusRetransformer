// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System.Diagnostics;
	using System.Linq;
	using ClcPlusRetransformer.Core;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Operation.Buffer;
	using Serilog;

	public class Program
	{
		public static void Main()
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

			////(string fileName, Type geometryType)[] files =
			////{
			////	(baselineFileName, typeof(LineString)), (hardboneFileName, typeof(Polygon)), (backboneFileName, typeof(Polygon)),
			////};

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			Polygon otherGeometry = (Polygon)provider.Load<Polygon>(aoiFileName)
				.Execute()
				.Single()
				.Buffer(100, new BufferParameters(1, EndCapStyle.Round, JoinStyle.Round, 2));

			IProcessor<LineString> baselineProcessor = provider.Load<LineString>(baselineFileName).Intersect(otherGeometry);
			IProcessor<LineString> hardboneProcessor =
				provider.Load<Polygon>(hardboneFileName).Intersect(otherGeometry).PolygonsToLines().Dissolve();
			IProcessor<LineString> backboneProcessor =
				provider.Load<Polygon>(backboneFileName).Intersect(otherGeometry).PolygonsToLines().Dissolve();

			IProcessor<LineString> difference = backboneProcessor.Difference(hardboneProcessor.Execute());

			IProcessor<LineString> smoothedAndSnapped = difference.Dissolve().Smooth().SnapTo(baselineProcessor.Execute());

			smoothedAndSnapped.Execute().Save(@"data\results\step2_snapped.shp");

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);
		}
	}
}