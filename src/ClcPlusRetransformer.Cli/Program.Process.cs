// <copyright file="Program.Process.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
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

			Geometry aoi = null;
			Geometry eeaBorder = null;

			if (aoiSection.Exists())
			{
				if (aoiSection.GetChildren().Count() == 4)
				{
					(double x1, double y1, double x2, double y2) = aoiSection.Get<double[]>();

					border = (Polygon)new Envelope(new Coordinate(x1, y1), new Coordinate(x2, y2)).ToGeometry();
					Envelope bufferedEnvelope = border.EnvelopeInternal.Copy();
					bufferedEnvelope.ExpandBy(Program.BufferDistance);

					aoi = bufferedEnvelope.ToGeometry();
				}
				else
				{
					aoi = new MultiPolygon(provider.LoadFromFile<Polygon>(aoiSection.Get<Input>(), precisionModel).Execute().ToArray());
				}

				Geometry bufferedAoi = aoi.Buffer(0.001);
				baselineProcessor = provider.LoadFromFile<LineString>(baselineInput, precisionModel,
						provider.GetRequiredService<ILogger<Processor>>())
					.Clip(bufferedAoi);
				hardboneProcessor = provider
					.LoadFromFile<LineString>(hardboneInput, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
					.Clip(bufferedAoi);
				backboneProcessor = provider
					.LoadFromFile<Polygon>(backboneInput, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
					.Buffer(0)
					.Clip(bufferedAoi);

				IConfigurationSection eeaSection = config.GetSection("BorderEEA");

				if (eeaSection.Exists())
				{
					eeaBorder = new MultiPolygon(provider
						.LoadFromFile<Polygon>(eeaSection.Get<Input>(), precisionModel, provider.GetRequiredService<ILogger<Processor>>())
						.Buffer(0)
						.Clip(aoi)
						.Execute()
						.ToArray());
				}
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
				backboneProcessor, null, provider, precisionModel, logger, aoi, eeaBorder);

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

		private static int FindDecimalPlaces(ICollection<LineString> differences)
		{
			int num = 0;

			foreach (LineString d in differences)
			{
				foreach (Coordinate c in d.Coordinates)
				{
					string sX = c.X.ToString(CultureInfo.InvariantCulture);
					string sY = c.Y.ToString(CultureInfo.InvariantCulture);

					int xDecimalPlaces = sX.Split('.').Count() > 1 ? sX.Split('.').ToList().ElementAt(1).Length : 0;
					int yDecimalPlaces = sY.Split('.').Count() > 1 ? sY.Split('.').ToList().ElementAt(1).Length : 0;

					if (xDecimalPlaces > 4 || yDecimalPlaces > 4)
					{
						num++;
						break;
					}
				}
			}

			return num;
		}

		private static async Task<IProcessor<Polygon>> ProcessInternalAsync(IProcessor<LineString> baselineProcessor,
			IProcessor<LineString> hardboneProcessor, IProcessor<Polygon> backboneProcessor, Envelope tileEnvelopeBuffered,
			IServiceProvider provider, PrecisionModel precisionModel, ILogger<Program> logger = null, Geometry envelope = null,
			Geometry eeaBorder = null)
		{
			Task task1 = Task.Run(baselineProcessor.Execute);
			Task task2 = Task.Run(hardboneProcessor.Execute);
			Task task3 = Task.Run(backboneProcessor.Execute);

			await Task.WhenAll(task1, task2, task3);
			logger?.LogInformation("Too precise geometries in {ProcessorName}: {Progress}", "HB Baselines",
				Program.FindDecimalPlaces(baselineProcessor.Execute()));

			IProcessor<LineString> hardboneProcessorLines = hardboneProcessor.Dissolve();
			IProcessor<LineString> backboneProcessorLines = backboneProcessor.PolygonsToLines().Dissolve();

			IProcessor<LineString> differenceProcessor = backboneProcessorLines
				.Difference(hardboneProcessorLines.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.Union(provider.GetRequiredService<ILogger<Processor>>())
				.Dissolve();
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "Dissolve",
				Program.FindDecimalPlaces(differenceProcessor.Execute()));

			if (hardboneProcessorLines.Execute().Count == 0)
			{
				return null;
			}

			IProcessor<LineString> simplify = differenceProcessor.Simplify();
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "Simplify",
				Program.FindDecimalPlaces(simplify.Execute()));

			IProcessor<LineString> smooth = simplify.Smooth();
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "Smooth",
				Program.FindDecimalPlaces(smooth.Execute()));

			IProcessor<LineString> snapTo = smooth.SnapTo(baselineProcessor.Execute(), precisionModel);
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "SnapTo",
				Program.FindDecimalPlaces(snapTo.Execute()));

			IProcessor<LineString> merged = snapTo.Merge(baselineProcessor.Execute());
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "Merge",
				Program.FindDecimalPlaces(merged.Execute()));

			IProcessor<LineString> union = merged.Node(precisionModel).Union(provider.GetRequiredService<ILogger<Processor>>());
			logger?.LogInformation("Too precise geometries after {ProcessorName}: {Progress}", "Node/Union",
				Program.FindDecimalPlaces(union.Execute()));

			IProcessor<Polygon> polygonized = union.Polygonize();

			if (eeaBorder != null)
			{
				polygonized = polygonized.Clip(eeaBorder);
			}

			return polygonized.EliminatePolygons(baselineProcessor.Execute(), provider.GetRequiredService<ILogger<Processor>>())
				.EliminateMergeSmallPolygons(provider.GetRequiredService<ILogger<Processor>>())
				.EliminatePolygons(Array.Empty<LineString>(), provider.GetRequiredService<ILogger<Processor>>());
		}
	}
}