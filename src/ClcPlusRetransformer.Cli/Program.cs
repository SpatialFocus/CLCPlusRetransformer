// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
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
	using ILogger = Microsoft.Extensions.Logging.ILogger;

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

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			PrecisionModel precisionModel = new PrecisionModel(10);

			(double x1, double y1, double x2, double y2) = config.GetSection("Aoi").Get<double[]>();
			IProcessor<Polygon> aoiProcessor = provider.FromGeometries("AreaOfInterest",
				(Polygon)new GeometryFactory().ToGeometry(new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2))));

			int numberOfSplits = int.Parse(config["PartitionCount"]);

			Polygon aoi = aoiProcessor.Execute().Single();

			TileResult[,] tileResults = new TileResult[numberOfSplits, numberOfSplits];

			foreach (var split in aoi.EnvelopeInternal.Split(numberOfSplits)
				.OrderBy(x => x.MinX)
				.ThenByDescending(x => x.MinY)
				.Select((value, index) => new { X = index / numberOfSplits, Y = index % numberOfSplits, Envelope = value, }))
			{
				tileResults[split.X, split.Y] = await Program.CleanedAndClippedToAoi(provider, config, precisionModel,
					(Polygon)new GeometryFactory().ToGeometry(split.Envelope), logger);
			}

			// Save
			Program.SaveTileResults(config, tileResults, precisionModel);

			// Load
			////Program.LoadTileResults(provider, config, precisionModel, numberOfSplits);

			// Process
			TileResult[] lines = Program.MergeColumnsToLines(config, tileResults, precisionModel);
			TileResult result = Program.MergeLinesToResult(lines, precisionModel);

			// Save final result
			result.Polygons.Save(config["OutputFileName"], precisionModel, ShapeProjection.ReadProjectionInfo(config["BaselineFileName"]));

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);

			if (bool.Parse(config["WaitForUserInputAfterCompletion"]))
			{
				Console.WriteLine("=== PRESS A KEY TO PROCEED ===");
				Console.ReadKey();
			}
		}

		public static TileResult Merge(TileResult first, TileResult second, LineString border, PrecisionModel precisionModel)
		{
			Envelope envelope = first.Envelope.EnvelopeInternal.Copy();
			envelope.ExpandToInclude(second.Envelope.EnvelopeInternal);

			TileResult result = new TileResult
			{
				Envelope = (Polygon)new GeometryFactory().ToGeometry(envelope),
				UnclippedPolygons = first.UnclippedPolygons.Concat(second.UnclippedPolygons).ToList(),
			};

			IList<Polygon> firstCandidatesUnclipped = first.UnclippedPolygons.Where(x => x.Intersects(border)).ToList();
			IList<Polygon> secondCandidates = second.Polygons.Where(x => x.Intersects(border)).ToList();
			second.Polygons = second.Polygons.Except(secondCandidates).ToList();

			foreach (Polygon leftPolygon in first.Polygons)
			{
				IntersectionMatrix intersectionMatrix = leftPolygon.Relate(border);

				if (intersectionMatrix[Location.Boundary, Location.Interior] != Dimension.Curve)
				{
					result.Polygons.Add(leftPolygon);
					continue;
				}

				ICollection<LineString> sharedBorders = leftPolygon.Intersection(border).FlattenAndIgnore<LineString>().ToList();

				// Should not be empty
				if (!sharedBorders.Any())
				{
					throw new InvalidOperationException();
				}

				List<Polygon> polygons = firstCandidatesUnclipped.Where(x => x.Contains(leftPolygon.InteriorPoint)).ToList();
				////List<Polygon> polygons = firstCandidatesUnclipped.Where(x => x.Covers(leftPolygon)).ToList();

				Polygon? unclippedPolygon = polygons.FirstOrDefault();

				if (unclippedPolygon == default)
				{
					result.Polygons.Add(leftPolygon);
					continue;
				}

				sharedBorders = sharedBorders.Where(sharedBorder =>
						unclippedPolygon.Relate(sharedBorder)[Location.Interior, Location.Interior] == Dimension.Curve)
					.ToList();

				if (!sharedBorders.Any())
				{
					result.Polygons.Add(leftPolygon);
					continue;
				}

				List<Polygon> polygonsToMerge = sharedBorders.SelectMany(sharedBorder =>
						secondCandidates.Where(candidate =>
							candidate.Relate(sharedBorder)[Location.Boundary, Location.Interior] == Dimension.Curve))
					.Distinct()
					.ToList();

				secondCandidates = secondCandidates.Except(polygonsToMerge).ToList();

				Polygon unionedPolygon = null;

				try
				{
					unionedPolygon = (Polygon)leftPolygon;

					foreach (Polygon polygonToMerge in polygonsToMerge)
					{
						unionedPolygon = (Polygon)unionedPolygon.Union(polygonToMerge);
					}
				}
				catch (Exception e)
				{
					continue;
				}

				secondCandidates.Add(unionedPolygon);
			}

			foreach (Polygon candidate in secondCandidates)
			{
				result.Polygons.Add(candidate);
			}

			foreach (Polygon polygon in second.Polygons)
			{
				result.Polygons.Add(polygon);
			}

			return result;
		}

		private static async Task<TileResult> CleanedAndClippedToAoi(ServiceProvider provider, IConfigurationRoot config,
			PrecisionModel precisionModel, Polygon aoi, ILogger logger)
		{
			string baselineFileName = config["BaselineFileName"];
			string hardboneFileName = config["HardboneFileName"];
			string backboneFileName = config["BackboneFileName"];

			IProcessor<Polygon> aoiProcessor = provider.FromGeometries("AreaOfInterest_Clipped", aoi);

			IProcessor<Polygon> bufferedAoiProcessor = aoiProcessor.Buffer(1000);
			Polygon bufferedAoi = bufferedAoiProcessor.Execute().Single();

			IProcessor<LineString> baselineProcessor = provider.LoadFromFileAndClip<LineString>(baselineFileName, precisionModel,
				bufferedAoi, provider.GetRequiredService<ILogger<Processor>>());

			IProcessor<Polygon> hardboneProcessor = provider.LoadFromFileAndClip<Polygon>(hardboneFileName, precisionModel, bufferedAoi,
				provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> backboneProcessor = provider.LoadFromFileAndClip<Polygon>(backboneFileName, precisionModel, bufferedAoi,
				provider.GetRequiredService<ILogger<Processor>>());

			Task task1 = Task.Run(() => baselineProcessor.Execute());
			Task task2 = Task.Run(() =>
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

			Task task3 = Task.Run(() =>
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

			await Task.WhenAll(task1, task2, task3).ConfigureAwait(true);

			IProcessor<LineString> hardboneProcessorLines = hardboneProcessor.PolygonsToLines().Dissolve();
			IProcessor<LineString> backboneProcessorLines = backboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> difference = backboneProcessorLines
				.Difference(hardboneProcessorLines.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();

			IProcessor<LineString> smoothed = difference.Smooth();
			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());

			IProcessor<Polygon> polygonized = smoothedAndSnapped.Merge(baselineProcessor.Execute())
				.Merge(bufferedAoiProcessor.PolygonsToLines().Execute())
				.Node(new PrecisionModel(10000))
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			IProcessor<Polygon> eliminatePolygons = polygonized.EliminatePolygons(provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> cleanedAndClippedToAoi = eliminatePolygons.Clip(aoi);

			return new TileResult()
			{
				Envelope = aoi, Polygons = cleanedAndClippedToAoi.Execute(), UnclippedPolygons = eliminatePolygons.Execute(),
			};
		}

		private static TileResult[,] LoadTileResults(ServiceProvider provider, IConfigurationRoot config, PrecisionModel precisionModel,
			int numberOfSplits)
		{
			TileResult[,] tileResults;
			tileResults = new TileResult[numberOfSplits, numberOfSplits];

			// Load
			for (int i = 0; i < numberOfSplits; i++)
			{
				for (int j = 0; j < numberOfSplits; j++)
				{
					tileResults[i, j] = new TileResult()
					{
						Polygons =
							provider.LoadFromFile<Polygon>(Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_polygons.shp"),
									precisionModel)
								.Execute(),
						UnclippedPolygons =
							provider.LoadFromFile<Polygon>(
									Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_polygons_unclipped.shp"), precisionModel)
								.Execute(),
						Envelope = provider
							.LoadFromFile<Polygon>(Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_envelope.shp"),
								precisionModel)
							.Execute()
							.Single(),
					};
				}
			}

			return tileResults;
		}

		private static TileResult[] MergeColumnsToLines(IConfigurationRoot config, TileResult[,] tileResults, PrecisionModel precisionModel)
		{
			TileResult[] lines = new TileResult[tileResults.GetLength(1)];

			for (int row = 0; row < tileResults.GetLength(1); row++)
			{
				TileResult line = tileResults[0, row];

				for (int column = 1; column < tileResults.GetLength(0); column++)
				{
					TileResult next = tileResults[column, row];

					LineString border = new LineString(new[]
					{
						new Coordinate { X = next.Envelope.EnvelopeInternal.MinX, Y = next.Envelope.EnvelopeInternal.MinY, },
						new Coordinate { X = next.Envelope.EnvelopeInternal.MinX, Y = next.Envelope.EnvelopeInternal.MaxY, },
					});

					line = Program.Merge(line, next, border, precisionModel);
				}

				line.Polygons.Save(Path.Combine(config["IntermediateFolderPath"], $"row_{row}_polygons.shp"), precisionModel);
				line.UnclippedPolygons.Save(Path.Combine(config["IntermediateFolderPath"], $"row_{row}_polygons_unclipped.shp"),
					precisionModel);
				line.Envelope.Save(Path.Combine(config["IntermediateFolderPath"], $"row_{row}_envelopes.shp"));

				lines[row] = line;
			}

			return lines;
		}

		private static TileResult MergeLinesToResult(TileResult[] lines, PrecisionModel precisionModel)
		{
			TileResult result = lines[0];

			for (int row = 1; row < lines.Length; row++)
			{
				TileResult next = lines[row];

				LineString border = new LineString(new[]
				{
					new Coordinate { X = next.Envelope.EnvelopeInternal.MinX, Y = next.Envelope.EnvelopeInternal.MaxY, },
					new Coordinate { X = next.Envelope.EnvelopeInternal.MaxX, Y = next.Envelope.EnvelopeInternal.MaxY, },
				});

				result = Program.Merge(result, next, border, precisionModel);
			}

			return result;
		}

		private static void SaveTileResults(IConfigurationRoot config, TileResult[,] tileResults, PrecisionModel precisionModel)
		{
			for (int i = 0; i < tileResults.GetLength(0); i++)
			{
				for (int j = 0; j < tileResults.GetLength(1); j++)
				{
					tileResults[i, j]
						.Polygons.Save(Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_polygons.shp"), precisionModel);
					tileResults[i, j]
						.UnclippedPolygons.Save(Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_polygons_unclipped.shp"),
							precisionModel);
					tileResults[i, j].Envelope.Save(Path.Combine(config["IntermediateFolderPath"], $"tile_{i}_{j}_envelope.shp"));
				}
			}
		}
	}
}