﻿// <copyright file="Program.Process.cs" company="Spatial Focus GmbH">
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
		private static readonly int bufferDistance = 1000;

		public static async Task ProcessShapesAsync(IServiceProvider provider, IConfigurationRoot config, PrecisionModel precisionModel,
			ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			logger.LogInformation("Processing shapes");

			string baselineFileName = config["BaselineFileName"];
			string hardboneFileName = config["HardboneFileName"];
			string backboneFileName = config["BackboneFileName"];

			IProcessor<LineString> baselineProcessor = provider.LoadFromFile<LineString>(baselineFileName, precisionModel,
				provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> hardboneProcessor =
				provider.LoadFromFile<Polygon>(hardboneFileName, precisionModel, provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> backboneProcessor =
				provider.LoadFromFile<Polygon>(backboneFileName, precisionModel, provider.GetRequiredService<ILogger<Processor>>());

			Envelope envelope = null;
			IConfigurationSection aoiSection = config.GetSection("Aoi");

			if (aoiSection != null)
			{
				(double x1, double y1, double x2, double y2) = aoiSection.Get<double[]>();

				envelope = new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2));
				Envelope bufferedEnvelope = envelope.Copy();
				bufferedEnvelope.ExpandBy(Program.bufferDistance);

				baselineProcessor = baselineProcessor.Clip(bufferedEnvelope.ToGeometry());
				hardboneProcessor = hardboneProcessor.Clip(bufferedEnvelope.ToGeometry());
				backboneProcessor = backboneProcessor.Clip(bufferedEnvelope.ToGeometry());
			}

			IProcessor<Polygon> processedPolygons = await Program.ProcessInternalAsync(baselineProcessor, hardboneProcessor,
				backboneProcessor, new Envelope(), provider, precisionModel);

			if (envelope != null)
			{
				processedPolygons.Clip(envelope.ToGeometry());
			}

			string exportFileName = config["ProcessedTileOutputFileName"];
			processedPolygons.Execute()
				.Save(exportFileName, precisionModel, ShapeProjection.ReadProjectionInfo(config["BaselineFileName"]));
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

			Tile tile = new Tile
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

			// TODO: Configuration variable
			Envelope tileEnvelopeBuffered = tileEnvelope.Copy();
			tileEnvelopeBuffered.ExpandBy(Program.bufferDistance);

			IProcessor<LineString> baselineProcessor = provider.FromGeometries("baseline",
				spatialContext.Set<Baseline>()
					.Where(x => x.Source == source && x.Geometry.Intersects(tileEnvelopeBuffered.ToGeometry()))
					.Select(x => x.Geometry)
					.ToArray());

			IProcessor<Polygon> hardboneProcessor = provider.FromGeometries("hardbone",
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
			////cleanedAndClippedToAoi.Execute().Save(@"C:\temp\geoville\RT_new_DP_and_smooth_half.shp", precisionModel);

			tile.Locked = false;
			tile.TileStatus = TileStatus.Processed;

			string exportFileName = configuration["ProcessedTileOutputFileName"];
			string fileName = Path.Combine(Path.GetDirectoryName(exportFileName),
				$"{Path.GetFileNameWithoutExtension(exportFileName)}_{tile.Id}{Path.GetExtension(exportFileName)}");
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
			IProcessor<Polygon> hardboneProcessor, IProcessor<Polygon> backboneProcessor, Envelope tileEnvelopeBuffered,
			IServiceProvider provider, PrecisionModel precisionModel)
		{
			Task task1 = Task.Run(() => baselineProcessor.Execute());
			Task task2 = Task.Run(() => hardboneProcessor.Execute());
			Task task3 = Task.Run(() => backboneProcessor.Execute());

			await Task.WhenAll(task1, task2, task3);

			IProcessor<LineString> hardboneProcessorLines = hardboneProcessor.PolygonsToLines().Dissolve();
			IProcessor<LineString> backboneProcessorLines = backboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> difference = backboneProcessorLines
				.Difference(hardboneProcessorLines.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();

			if (hardboneProcessorLines.Execute().Count == 0)
			{
				return null;
			}

			////IProcessor<LineString> difference = provider.LoadFromFile<LineString>(@"C:\temp\geoville\difference.shp", precisionModel);
			////difference.Execute().Save(@"C:\temp\geoville\difference.shp", precisionModel);

			IProcessor<LineString> simplified = difference.Simplify();
			////simplified.Execute().Save(@"C:\temp\geoville3\simplified.shp", precisionModel);

			IProcessor<LineString> smoothed = simplified.Smooth();
			////smoothed.Execute().Save(@"C:\temp\geoville3\smoothed.shp", precisionModel);

			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());

			IProcessor<LineString> processor = smoothedAndSnapped.Merge(baselineProcessor.Execute());

			Envelope geometriesEnvelope = new GeometryCollection(processor.Execute().Cast<Geometry>().ToArray()).EnvelopeInternal;

			Envelope minimumEnvelope = new Envelope(Math.Max(geometriesEnvelope.MinX, tileEnvelopeBuffered.MinX),
				Math.Min(geometriesEnvelope.MaxX, tileEnvelopeBuffered.MaxX), Math.Max(geometriesEnvelope.MinY, tileEnvelopeBuffered.MinY),
				Math.Min(geometriesEnvelope.MaxY, tileEnvelopeBuffered.MaxY));

			IProcessor<Polygon> polygonized = processor
				.Merge(provider.FromGeometries("tileEnvelope", (Polygon)minimumEnvelope.ToGeometry()).PolygonsToLines().Execute())
				.Node(precisionModel)
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			return polygonized.EliminatePolygons(baselineProcessor.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.EliminatePolygons(Array.Empty<LineString>(), provider.GetRequiredService<ILogger<Processor>>());
		}
	}
}