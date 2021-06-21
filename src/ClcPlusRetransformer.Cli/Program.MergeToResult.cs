// <copyright file="Program.MergeToResult.cs" company="Spatial Focus GmbH">
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
	using ClcPlusRetransformer.Core;
	using ClcPlusRetransformer.Core.Processors;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public partial class Program
	{
		public static async Task<IProcessor<Polygon>> MergeToResultAsync(IServiceProvider provider, IConfigurationRoot configuration,
			ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			SpatialContext spatialContext = provider.CreateScope().ServiceProvider.GetRequiredService<SpatialContext>();
			Source source = await spatialContext.Sources.SingleAsync(x => x.Name == configuration["SourceName"], cancellationToken);

			logger.LogInformation("Copying to result table");

			if (!spatialContext.Set<ResultGeometry>().Any(x => x.Source == source))
			{
				foreach (Tile tile in await spatialContext.Tiles.Where(x => x.Source == source).ToListAsync(cancellationToken))
				{
					logger.LogDebug($"Copying tile #{tile.Id}");

					await spatialContext.Set<ResultGeometry>()
						.AddRangeAsync(
							spatialContext.Set<TileGeometry>()
								.Where(x => x.TileId == tile.Id)
								.Select(x => new ResultGeometry()
								{
									Polygon = x.Polygon, OriginId = x.Id, RelatedGeometries = x.RelatedGeometries, SourceId = source.Id,
								}), cancellationToken);
				}
			}
			else
			{
				logger.LogDebug("Geometries existing, skipping...");
			}

			logger.LogInformation("Saving...");
			await spatialContext.SaveChangesAsync(cancellationToken);

			logger.LogInformation("Merging border polygons");

			bool completedAll;

			do
			{
				List<ResultGeometry> resultGeometries = spatialContext.Set<ResultGeometry>()
					.FromSqlRaw(
						"SELECT * FROM ResultGeometry WHERE RelatedGeometries IS NOT NULL AND RelatedGeometries <> '' AND Completed = false")
					.ToList();

				logger.LogDebug($"Remaining geometries {resultGeometries.Count}");

				completedAll = (await resultGeometries.ForEachAsync(resultGeometries.Count > 64 ? 8 : 1,
					(tileGeometry, innerCancellationToken) =>
						Program.ProcessGeometry(provider, tileGeometry.Id, logger, innerCancellationToken), cancellationToken)).All(
					completed => completed);
			}
			while (!completedAll);

			return provider.FromGeometries("result",
				spatialContext.Set<ResultGeometry>().Where(x => x.Source == source).Select(x => x.Polygon).ToArray());
		}

		private static async Task<bool> ProcessGeometry(IServiceProvider provider, int id, ILogger<Program> logger,
			CancellationToken cancellationToken)
		{
			SpatialContext spatialContext = provider.CreateScope().ServiceProvider.GetRequiredService<SpatialContext>();
			ResultGeometry resultGeometry =
				await spatialContext.Set<ResultGeometry>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

			logger.LogDebug($"Processing geometry #{id}");

			// Locked by other thread
			if (resultGeometry != null && resultGeometry.Locked)
			{
				logger.LogDebug($"Geometry #{id} locked by other thread");
				return false;
			}

			// Processed by other thread
			if (resultGeometry == null)
			{
				logger.LogDebug($"Geometry #{id} already processed");
				return true;
			}

			try
			{
				resultGeometry.Locked = true;
				await spatialContext.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateConcurrencyException)
			{
				logger.LogDebug($"Couldn't lock geometry #{resultGeometry.Id}");
				return false;
			}

			ICollection<Guid> relatedGeometryIds;

			bool completed = true;

			do
			{
				relatedGeometryIds = resultGeometry.ExtendedRelatedGeometryIds(spatialContext);

				foreach (Guid relatedGeometryId in relatedGeometryIds)
				{
					ResultGeometry relatedGeometry = await spatialContext.Set<ResultGeometry>()
						.SingleOrDefaultAsync(x => x.OriginId == relatedGeometryId, cancellationToken);

					if (relatedGeometry != null && relatedGeometry.Locked)
					{
						logger.LogDebug($"Geometry #{relatedGeometry.Id} locked by other thread");
						completed = false;
					}

					if (relatedGeometry?.Locked != false)
					{
						continue;
					}

					logger.LogDebug($"Processing geometry #{resultGeometry.Id} and #{relatedGeometry.Id}");

					try
					{
						relatedGeometry.Locked = true;
						await spatialContext.SaveChangesAsync(cancellationToken);
					}
					catch (DbUpdateConcurrencyException)
					{
						logger.LogDebug($"Couldn't lock geometry #{relatedGeometry.Id}");

						completed = false;
						await spatialContext.Entry(relatedGeometry).ReloadAsync(cancellationToken);

						continue;
					}

					resultGeometry.RelatedGeometries = resultGeometry.RelatedGeometries
						.Union(relatedGeometry.RelatedGeometries.Except(new[] { resultGeometry.OriginId }))
						.ToList();
					resultGeometry.Polygon = (Polygon)resultGeometry.Polygon.Union(relatedGeometry.Polygon);

					spatialContext.Set<ResultGeometry>().Remove(relatedGeometry);

					logger.LogDebug($"Deleting geometry #{relatedGeometry.Id}");

					await spatialContext.SaveChangesAsync(cancellationToken);
				}
			}
			while (!resultGeometry.ExtendedRelatedGeometryIds(spatialContext).SequenceEqual(relatedGeometryIds));

			resultGeometry.Locked = false;

			if (completed)
			{
				resultGeometry.Completed = true;
			}

			await spatialContext.SaveChangesAsync(cancellationToken);

			return completed;
		}
	}
}