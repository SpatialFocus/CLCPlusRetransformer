// <copyright file="Program.Process.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Cli.Entities;
	using ClcPlusRetransformer.Core;
	using ClcPlusRetransformer.Core.Processors;
	using ClcPlusRetransformer.Core.Processors.Extension;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public partial class Program
	{
		// TODO: Configuration variable
		private const int BufferDistance = 1000;

		public static async Task<IProcessor<Polygon>> ProcessShapesAsync(IServiceProvider provider, IConfigurationRoot config,
			PrecisionModel precisionModel, ILogger<Program> logger)
		{
			logger.LogInformation("Processing shapes");

			Input baselineInput = config.GetSection("Baseline").Get<Input>();
			Input hardboneInput = config.GetSection("Hardbone").Get<Input>();
			Input backboneInput = config.GetSection("Backbone").Get<Input>();

			IProcessor<LineString> baselineProcessor;
			IProcessor<LineString> hardboneProcessor;
			IProcessor<Polygon> backboneProcessor;

			Geometry border = null;
			IConfigurationSection aoiSection = config.GetSection("Aoi");

			if (aoiSection.Exists())
			{
				Geometry aoi;

				if (aoiSection.GetChildren().Count() > 1)
				{
					(double x1, double y1, double x2, double y2) = aoiSection.Get<double[]>();

					border = (Polygon)new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2)).ToGeometry();
					Envelope bufferedEnvelope = border.EnvelopeInternal.Copy();
					bufferedEnvelope.ExpandBy(Program.BufferDistance);

					aoi = bufferedEnvelope.ToGeometry();
				}
				else
				{
					aoi = provider.LoadFromFile<Polygon>(aoiSection.Get<Input>(), precisionModel).Execute().Single();
				}

				baselineProcessor = provider.LoadFromFileAndClip<LineString>(baselineInput, precisionModel, aoi,
					provider.GetRequiredService<ILogger<Processor>>());
				hardboneProcessor = provider
					.LoadFromFile<LineString>(hardboneInput, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
					.Clip(aoi);
				backboneProcessor = provider
					.LoadFromFile<Polygon>(backboneInput, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
					.Buffer(0)
					.Clip(aoi);
			}
			else
			{
				baselineProcessor = provider.LoadFromFile<LineString>(baselineInput, precisionModel,
					provider.GetRequiredService<ILogger<Processor>>());
				hardboneProcessor = provider.LoadFromFile<LineString>(hardboneInput, precisionModel,
					provider.GetRequiredService<ILogger<Processor>>());
				backboneProcessor = provider.LoadFromFile<Polygon>(backboneInput, precisionModel,
					provider.GetRequiredService<ILogger<Processor>>());
			}

			IProcessor<Polygon> processedPolygons = await Program.ProcessInternalAsync(baselineProcessor, hardboneProcessor,
				backboneProcessor, null, provider, precisionModel);

			if (border != null)
			{
				processedPolygons = processedPolygons.Clip(border);
			}

			return processedPolygons;
		}

		public static async Task ProcessTilesAsync(IServiceProvider provider, IConfigurationRoot configuration,
			PrecisionModel precisionModel, ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			logger.LogInformation("Partitioning and processing tiles");

			(double x1, double y1, double x2, double y2) = configuration.GetSection("Aoi").Get<double[]>();
			int numberOfSplits = int.Parse(configuration["PartitionCount"]);
			await EnvelopeExtension.Split(new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2)), numberOfSplits)
				.ForEachAsync(int.Parse(configuration["DegreeOfParallelism"]),
					async (envelope, innerCancellationToken) => await Program.ProcessTileAsync(provider.CreateScope().ServiceProvider,
						configuration, precisionModel, envelope, innerCancellationToken), cancellationToken);
		}

		protected static async Task ProcessTileAsync(IServiceProvider provider, IConfigurationRoot configuration,
			PrecisionModel precisionModel, Envelope tileEnvelope, CancellationToken cancellationToken = default)
		{
			SpatialContext spatialContext = provider.GetRequiredService<SpatialContext>();

			Source source = await spatialContext.Sources.SingleAsync(x => x.Name == configuration["SourceName"], cancellationToken);

			Tile tile = new()
			{
				EastOfOrigin = (int)tileEnvelope.MinX,
				NorthOfOrigin = (int)tileEnvelope.MinY,
				CellSizeInMeters = (int)Math.Abs(tileEnvelope.MinX - tileEnvelope.MaxX),
				TileStatus = TileStatus.Created,
				Locked = true,
				Source = source,
			};

			await spatialContext.Tiles.AddAsync(tile, cancellationToken);
			await spatialContext.SaveChangesAsync(cancellationToken);

			Envelope tileEnvelopeBuffered = tileEnvelope.Copy();
			tileEnvelopeBuffered.ExpandBy(Program.BufferDistance);

			IProcessor<LineString> baselineProcessor = provider.FromGeometries("baseline",
				spatialContext.Set<Baseline>()
					.Where(x => x.Source == source && x.Geometry.Intersects(tileEnvelopeBuffered.ToGeometry()))
					.Select(x => x.Geometry)
					.ToArray());

			IProcessor<LineString> hardboneProcessor = provider.FromGeometries("hardbone",
					spatialContext.Set<Hardbone>()
						.Where(x => x.Source == source && x.Geometry.Intersects(tileEnvelopeBuffered.ToGeometry()))
						.Select(x => x.Geometry)
						.ToArray())
				.Buffer(0)
				.Clip(tileEnvelopeBuffered.ToGeometry());

			IProcessor<Polygon> backboneProcessor = provider.FromGeometries("backbones",
					spatialContext.Set<Backbone>()
						.Where(x => x.Source == source && x.Geometry.Intersects(tileEnvelopeBuffered.ToGeometry()))
						.Select(x => x.Geometry)
						.ToArray())
				.Buffer(0)
				.Clip(tileEnvelopeBuffered.ToGeometry());

			IProcessor<Polygon> eliminatePolygons = await Program.ProcessInternalAsync(baselineProcessor, hardboneProcessor,
				backboneProcessor, tileEnvelopeBuffered, provider, precisionModel);

			if (eliminatePolygons == null)
			{
				// TODO: Store "empty" or something?
				tile.Locked = false;
				tile.TileStatus = TileStatus.Exported;
				await spatialContext.SaveChangesAsync(cancellationToken);

				return;
			}

			IProcessor<Polygon> cleanedAndClippedToAoi = eliminatePolygons.Clip(tileEnvelope.ToGeometry());

			tile.Locked = false;
			tile.TileStatus = TileStatus.Processed;

			string exportFileName = configuration["ProcessedOutputFileName"];
			string fileName = Path.Combine(Path.GetDirectoryName(exportFileName),
				$"{Path.GetFileNameWithoutExtension(exportFileName)}_tile{tile.Id}{Path.GetExtension(exportFileName)}");
			cleanedAndClippedToAoi.Execute()
				.Save(fileName, precisionModel, ShapeProjection.ReadProjectionInfo(configuration["BaselineFileName"]));

			await spatialContext.Set<TileGeometry>()
				.AddRangeAsync(cleanedAndClippedToAoi.Execute().Select(x => new TileGeometry() { TileId = tile.Id, Polygon = x, }),
					cancellationToken);

			await spatialContext.Set<TileGeometryBuffered>()
				.AddRangeAsync(eliminatePolygons.Execute().Select(x => new TileGeometryBuffered() { TileId = tile.Id, Polygon = x, }),
					cancellationToken);

			await spatialContext.SaveChangesAsync(cancellationToken);
		}

		private static async Task<IProcessor<Polygon>> ProcessInternalAsync(IProcessor<LineString> baselineProcessor,
			IProcessor<LineString> hardboneProcessor, IProcessor<Polygon> backboneProcessor, Envelope tileEnvelopeBuffered,
			IServiceProvider provider, PrecisionModel precisionModel, Polygon envelope = null)
		{
			Task task1 = Task.Run(baselineProcessor.Execute);
			Task task2 = Task.Run(hardboneProcessor.Execute);
			Task task3 = Task.Run(backboneProcessor.Execute);

			await Task.WhenAll(task1, task2, task3);

			IProcessor<LineString> hardboneProcessorLines = hardboneProcessor.Dissolve();
			IProcessor<LineString> backboneProcessorLines = backboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> differenceProcessor = backboneProcessorLines
				.Difference(hardboneProcessorLines.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();

			if (hardboneProcessorLines.Execute().Count == 0)
			{
				return null;
			}

			IProcessor<LineString> mergedProcessor = differenceProcessor.Simplify()
				.Smooth()
				.SnapTo(baselineProcessor.Execute())
				.Merge(baselineProcessor.Execute());

			Envelope geometriesEnvelope = new GeometryCollection(mergedProcessor.Execute().Cast<Geometry>().ToArray()).EnvelopeInternal;

			Envelope minimumEnvelope = geometriesEnvelope;

			if (tileEnvelopeBuffered != null)
			{
				minimumEnvelope = new Envelope(Math.Max(geometriesEnvelope.MinX, tileEnvelopeBuffered.MinX),
					Math.Min(geometriesEnvelope.MaxX, tileEnvelopeBuffered.MaxX),
					Math.Max(geometriesEnvelope.MinY, tileEnvelopeBuffered.MinY),
					Math.Min(geometriesEnvelope.MaxY, tileEnvelopeBuffered.MaxY));
			}

			IProcessor<Polygon> polygonized = mergedProcessor
				.Merge(provider.FromGeometries("tileEnvelope", envelope ?? (Polygon)minimumEnvelope.ToGeometry())
					.PolygonsToLines()
					.Execute())
				.Node(precisionModel)
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			return polygonized.EliminatePolygons(baselineProcessor.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.EliminateMergeSmallPolygons(provider.GetRequiredService<ILogger<Processor>>())
				.EliminatePolygons(Array.Empty<LineString>(), provider.GetRequiredService<ILogger<Processor>>());
		}
	}
}