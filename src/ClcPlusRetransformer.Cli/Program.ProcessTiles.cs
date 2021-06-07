// <copyright file="Program.ProcessTilesAsync.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
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
		public static async Task ProcessTilesAsync(IServiceProvider provider, IConfigurationRoot configuration,
			ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			logger.LogInformation("Partitioning and processing tiles");

			(double x1, double y1, double x2, double y2) = configuration.GetSection("Aoi").Get<double[]>();
			int numberOfSplits = int.Parse(configuration["PartitionCount"]);
			await EnvelopeExtension.Split(new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2)), numberOfSplits)
				.ForEachAsync(4,
					async (envelope, innerCancellationToken) =>
						await Program.CleanedAndClippedToAoiAsync(provider.CreateScope().ServiceProvider, configuration, envelope, innerCancellationToken),
					cancellationToken);
		}

		private static async Task CleanedAndClippedToAoiAsync(IServiceProvider provider, IConfigurationRoot configuration, Envelope tileEnvelope,
			CancellationToken cancellationToken = default)
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
			tileEnvelopeBuffered.ExpandBy(1000);

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
				// TODO: Store "empty" or something?
				tile.Locked = false;
				tile.TileStatus = TileStatus.Exported;
				await spatialContext.SaveChangesAsync(cancellationToken);

				return;
			}

			////IProcessor<LineString> difference = provider.LoadFromFile<LineString>(@"C:\temp\geoville\difference.shp", new PrecisionModel(10));
			difference.Execute().Save(@"C:\temp\geoville\update_07_06\difference.shp", new PrecisionModel(10000));

			IProcessor<LineString> simplified = difference.Simplify();
			simplified.Execute().Save(@"C:\temp\geoville\update_07_06\simplified.shp", new PrecisionModel(10000));

			IProcessor<LineString> smoothed = simplified.Smooth();
			smoothed.Execute().Save(@"C:\temp\geoville\update_07_06\smoothed.shp", new PrecisionModel(10000));

			baselineProcessor.Execute().Save(@"C:\temp\geoville\update_07_06\baselineProcessor.shp", new PrecisionModel(10000));

			IProcessor<LineString> smoothedAndSnapped = smoothed.SnapTo(baselineProcessor.Execute());
			smoothedAndSnapped.Execute().Save(@"C:\temp\geoville\update_07_06\smoothedAndSnapped.shp", new PrecisionModel(10000));

			IProcessor<LineString> processor = smoothedAndSnapped.Merge(baselineProcessor.Execute());
			processor.Execute().Save(@"C:\temp\geoville\update_07_06\merged.shp", new PrecisionModel(10000));

			Envelope geometriesEnvelope = new GeometryCollection(processor.Execute().Cast<Geometry>().ToArray()).EnvelopeInternal;

			Envelope minimumEnvelope = new Envelope(Math.Max(geometriesEnvelope.MinX, tileEnvelopeBuffered.MinX),
				Math.Min(geometriesEnvelope.MaxX, tileEnvelopeBuffered.MaxX), Math.Max(geometriesEnvelope.MinY, tileEnvelopeBuffered.MinY),
				Math.Min(geometriesEnvelope.MaxY, tileEnvelopeBuffered.MaxY));

			IProcessor<Polygon> polygonized = processor
				.Merge(provider.FromGeometries("tileEnvelope", (Polygon)minimumEnvelope.ToGeometry()).PolygonsToLines().Execute())
				.Node(new PrecisionModel(10000))
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Polygonize();

			IProcessor<Polygon> eliminatePolygons = polygonized.EliminatePolygons(provider.GetRequiredService<ILogger<Processor>>());
			IProcessor<Polygon> cleanedAndClippedToAoi = eliminatePolygons.Clip(tileEnvelope.ToGeometry());
			cleanedAndClippedToAoi.Execute().Save(@"C:\temp\geoville\update_07_06\RT_new_DP_and_smooth.shp", new PrecisionModel(10000));

			tile.Locked = false;
			tile.TileStatus = TileStatus.Processed;

			await spatialContext.Set<TileGeometry>()
				.AddRangeAsync(cleanedAndClippedToAoi.Execute().Select(x => new TileGeometry() { TileId = tile.Id, Polygon = x, }),
					cancellationToken);

			await spatialContext.Set<TileGeometryBuffered>()
				.AddRangeAsync(eliminatePolygons.Execute().Select(x => new TileGeometryBuffered() { TileId = tile.Id, Polygon = x, }),
					cancellationToken);

			await spatialContext.SaveChangesAsync(cancellationToken);
		}
	}
}