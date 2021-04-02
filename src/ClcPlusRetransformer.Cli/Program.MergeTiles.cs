// <copyright file="Program.MergeTiles.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Cli.Entities;
	using ClcPlusRetransformer.Core.Processors;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public partial class Program
	{
		public static async Task MergeTiles(IServiceProvider provider, IConfigurationRoot configuration, ILogger<Program> logger,
			CancellationToken cancellationToken = default)
		{
			int remainingTiles;

			logger.LogInformation($"Merging tiles horizontally");

			SpatialContext spatialContext = provider.CreateScope().ServiceProvider.GetRequiredService<SpatialContext>();
			Source source = await spatialContext.Sources.SingleAsync(x => x.Name == configuration["SourceName"], cancellationToken);

			do
			{
				// Calculate remaining tiles (regardless of locking state)
				remainingTiles = await spatialContext.Tiles.Where(x => x.Source == source)
					.SelectMany(t1 => spatialContext.Tiles, (tile1, tile2) => new { Tile1 = tile1, Tile2 = tile2 })
					.Where(tuple =>
						!tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedEast) &&
						tuple.Tile1.EastOfOrigin + tuple.Tile1.CellSizeInMeters == tuple.Tile2.EastOfOrigin &&
						tuple.Tile1.NorthOfOrigin == tuple.Tile2.NorthOfOrigin)
					.CountAsync(cancellationToken);

				logger.LogDebug($"{remainingTiles} tile tuples remaining");

				IQueryable<TileTuple> tiles = spatialContext.Tiles.Where(x => x.Source == source)
					.SelectMany(t1 => spatialContext.Tiles, (tile1, tile2) => new { Tile1 = tile1, Tile2 = tile2 })
					.Where(tuple => tuple.Tile1.Locked == false && tuple.Tile2.Locked == false &&
						!tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedEast) &&
						tuple.Tile1.EastOfOrigin + tuple.Tile1.CellSizeInMeters == tuple.Tile2.EastOfOrigin &&
						tuple.Tile1.NorthOfOrigin == tuple.Tile2.NorthOfOrigin)
					.Select(t => new TileTuple(t.Tile1, t.Tile2));

				await tiles.ForEachAsync(4, Program.MergeTuple(provider, true, logger), cancellationToken);
			}
			while (remainingTiles != 0);

			logger.LogInformation($"Merging tiles vertically");

			do
			{
				// Calculate remaining tiles (regardless of locking state)
				remainingTiles = await spatialContext.Tiles.Where(x => x.Source == source)
					.SelectMany(t1 => spatialContext.Tiles, (tile1, tile2) => new { Tile1 = tile1, Tile2 = tile2 })
					.Where(tuple =>
						!tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedNorth) && tuple.Tile1.EastOfOrigin == tuple.Tile2.EastOfOrigin &&
						tuple.Tile1.NorthOfOrigin + tuple.Tile1.CellSizeInMeters == tuple.Tile2.NorthOfOrigin)
					.CountAsync(cancellationToken);

				logger.LogDebug($"{remainingTiles} tile tuples remaining");

				IQueryable<TileTuple> tiles2 = spatialContext.Tiles.Where(x => x.Source == source)
					.SelectMany(t1 => spatialContext.Tiles, (tile1, tile2) => new { Tile1 = tile1, Tile2 = tile2 })
					.Where(tuple => tuple.Tile1.Locked == false && tuple.Tile2.Locked == false &&
						!tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedNorth) && tuple.Tile1.EastOfOrigin == tuple.Tile2.EastOfOrigin &&
						tuple.Tile1.NorthOfOrigin + tuple.Tile1.CellSizeInMeters == tuple.Tile2.NorthOfOrigin)
					.Select(t => new TileTuple(t.Tile1, t.Tile2));

				await tiles2.ForEachAsync(4, Program.MergeTuple(provider, false, logger), cancellationToken);
			}
			while (remainingTiles != 0);
		}

		private static void Merge(SpatialContext context, Tile first, Tile second, LineString border)
		{
			IList<TileGeometryBuffered> firstCandidatesBuffer = first.GeometriesBuffered.Where(x => x.Polygon.Intersects(border)).ToList();
			IList<TileGeometry> secondCandidates = second.Geometries.Where(x => x.Polygon.Intersects(border)).ToList();

			second.Geometries = second.Geometries.Except(secondCandidates).ToList();

			IList<TileGeometry> mergedGeometries = new List<TileGeometry>();

			foreach (TileGeometry firstPolygon in first.Geometries.ToList())
			{
				IntersectionMatrix intersectionMatrix = firstPolygon.Polygon.Relate(border);

				if (intersectionMatrix[Location.Boundary, Location.Interior] != Dimension.Curve)
				{
					continue;
				}

				ICollection<LineString> sharedBorders =
					firstPolygon.Polygon.Boundary.Intersection(border).FlattenAndIgnore<LineString>().ToList();

				// Should not be empty
				if (!sharedBorders.Any())
				{
					throw new InvalidOperationException();
				}

				TileGeometryBuffered? bufferedPolygon =
					firstCandidatesBuffer.FirstOrDefault(x => x.Polygon.Contains(firstPolygon.Polygon.InteriorPoint));

				if (bufferedPolygon == default)
				{
					continue;
				}

				sharedBorders = sharedBorders.Where(sharedBorder =>
					{
						IntersectionMatrix relate = bufferedPolygon.Polygon.Relate(sharedBorder);

						return relate[Location.Interior, Location.Interior] == Dimension.Curve ||
							(relate[Location.Boundary, Location.Interior] == Dimension.Point &&
								sharedBorder.Intersection(bufferedPolygon.Polygon).Dimension == Dimension.Curve);
					})
					.ToList();

				if (!sharedBorders.Any())
				{
					continue;
				}

				List<TileGeometry> polygonsToMerge = sharedBorders.SelectMany(sharedBorder =>
						secondCandidates.Concat(mergedGeometries)
							.Where(candidate =>
								candidate.Polygon.Relate(sharedBorder)[Location.Boundary, Location.Interior] == Dimension.Curve))
					.Distinct()
					.ToList();

				secondCandidates = secondCandidates.Except(polygonsToMerge).ToList();
				mergedGeometries = mergedGeometries.Except(polygonsToMerge).ToList();

				first.Geometries.Remove(firstPolygon);

				TileGeometry mergedGeometry = new TileGeometry()
				{
					Polygon = (Polygon)firstPolygon.Polygon,
					RelatedGeometries = firstPolygon.RelatedGeometries.Union(polygonsToMerge.SelectMany(x => x.RelatedGeometries))
						.ToList(),
				};

				try
				{
					foreach (Polygon polygonToMerge in polygonsToMerge.Select(x => x.Polygon))
					{
						mergedGeometry.Polygon = (Polygon)mergedGeometry.Polygon.Union(polygonToMerge);
					}
				}
				catch (Exception e)
				{
					continue;
				}

				mergedGeometries.Add(mergedGeometry);
			}

			second.Geometries = second.Geometries.Concat(secondCandidates).ToList();

			foreach (TileGeometry mergedGeometry in mergedGeometries)
			{
				TileGeometry tileGeometry = new TileGeometry()
				{
					Polygon = mergedGeometry.Polygon, RelatedGeometries = mergedGeometry.RelatedGeometries.ToList(),
				};

				mergedGeometry.RelatedGeometries.Add(tileGeometry.Id);
				tileGeometry.RelatedGeometries.Add(mergedGeometry.Id);

				mergedGeometry.TileId = first.Id;
				tileGeometry.TileId = second.Id;

				context.Add(mergedGeometry);
				context.Add(tileGeometry);
			}
		}

		private static void MergeHorizontal(SpatialContext context, Tile left, Tile right)
		{
			Program.Merge(context, left, right,
				new LineString(new[]
				{
					new Coordinate { X = left.EastOfOrigin + left.CellSizeInMeters, Y = left.NorthOfOrigin, },
					new Coordinate { X = left.EastOfOrigin + left.CellSizeInMeters, Y = left.NorthOfOrigin + left.CellSizeInMeters, },
				}));

			left.TileStatus |= TileStatus.MergedEast;
			right.TileStatus |= TileStatus.MergedWest;
		}

		private static Func<TileTuple, CancellationToken, Task> MergeTuple(IServiceProvider provider, bool horizontal,
			ILogger<Program> logger) =>
			async (tuple, innerCancellationToken) =>
			{
				SpatialContext spatialContext = provider.CreateScope().ServiceProvider.GetRequiredService<SpatialContext>();

				logger.LogDebug($"Processing tile #{tuple.Tile1.Id} & #{tuple.Tile2.Id}");

				Tile tile1 = spatialContext.Tiles.SingleOrDefault(x =>
					x.Id == tuple.Tile1.Id && (horizontal
						? !tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedEast)
						: !tuple.Tile1.TileStatus.HasFlag(TileStatus.MergedNorth)));
				Tile tile2 = spatialContext.Tiles.SingleOrDefault(x =>
					x.Id == tuple.Tile2.Id && (horizontal
						? !tuple.Tile2.TileStatus.HasFlag(TileStatus.MergedWest)
						: !tuple.Tile2.TileStatus.HasFlag(TileStatus.MergedSouth)));

				// Locked by other thread
				if (tile1 != null && tile1.Locked)
				{
					logger.LogDebug($"Tile #{tile1.Id} locked by other thread");
					return;
				}

				// Locked by other thread
				if (tile2 != null && tile2.Locked)
				{
					logger.LogDebug($"Tile #{tile2.Id} locked by other thread");
					return;
				}

				// Processed by other thread
				if (tile1 == null || tile2 == null)
				{
					logger.LogDebug($"Tile #{tuple.Tile1.Id} and #{tuple.Tile2.Id} already processed");
					return;
				}

				try
				{
					tile1.Locked = true;
					tile2.Locked = true;
					await spatialContext.SaveChangesAsync(innerCancellationToken);
				}
				catch (DbUpdateConcurrencyException)
				{
					logger.LogDebug($"Couldn't lock tile #{tile1.Id} or tile #{tile2.Id}");
					return;
				}

				if (horizontal)
				{
					Program.MergeHorizontal(spatialContext, tile1, tile2);
				}
				else
				{
					Program.MergeVertical(spatialContext, tile1, tile2);
				}

				tile1.Locked = false;
				tile2.Locked = false;
				await spatialContext.SaveChangesAsync(innerCancellationToken);
			};

		private static void MergeVertical(SpatialContext context, Tile bottom, Tile top)
		{
			Program.Merge(context, bottom, top,
				new LineString(new[]
				{
					new Coordinate { X = bottom.EastOfOrigin, Y = bottom.NorthOfOrigin + bottom.CellSizeInMeters, },
					new Coordinate
					{
						X = bottom.EastOfOrigin + bottom.CellSizeInMeters, Y = bottom.NorthOfOrigin + bottom.CellSizeInMeters,
					},
				}));

			bottom.TileStatus |= TileStatus.MergedNorth;
			top.TileStatus |= TileStatus.MergedSouth;
		}
	}
}