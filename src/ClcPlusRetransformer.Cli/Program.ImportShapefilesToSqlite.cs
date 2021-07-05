// <copyright file="Program.ImportShapefilesToSqlite.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Cli.Entities;
	using ClcPlusRetransformer.Core;
	using ClcPlusRetransformer.Core.Processors;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.EntityFrameworkCore.Infrastructure;
	using Microsoft.EntityFrameworkCore.Storage;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public partial class Program
	{
		private static async Task ImportShapefilesToSqliteAsync(ServiceProvider provider, IConfigurationRoot config,
			PrecisionModel precisionModel, ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			logger.LogInformation("Importing shapefiles to sqlite database");

			SpatialContext spatialContext = provider.GetRequiredService<SpatialContext>();

			if (await ((RelationalDatabaseCreator)spatialContext.GetService<IDatabaseCreator>()).ExistsAsync(cancellationToken))
			{
				logger.LogDebug("Existing database will be used");
			}
			else
			{
				logger.LogDebug("Creating sqlite database");

				await spatialContext.Database.EnsureCreatedAsync(cancellationToken);

				await spatialContext.Database.ExecuteSqlRawAsync(@"CREATE TRIGGER UpdateTileVersion
					AFTER UPDATE ON Tiles
					BEGIN
						UPDATE Tiles
						SET Version = Version + 1
						WHERE rowid = NEW.rowid;
					END;", cancellationToken);

				await spatialContext.Database.ExecuteSqlRawAsync(@"CREATE TRIGGER UpdateResultGeometry
					AFTER UPDATE ON ResultGeometry
					BEGIN
						UPDATE ResultGeometry
						SET Version = Version + 1
						WHERE rowid = NEW.rowid;
					END;", cancellationToken);
			}

			string sourceName = config["SourceName"];

			if (await spatialContext.Sources.SingleOrDefaultAsync(x => x.Name == sourceName, cancellationToken) != null)
			{
				logger.LogDebug("Source exists, skipping...");
				return;
			}

			logger.LogDebug("Importing shapefiles...");

			string baselineFileName = config["BaselineFileName"];
			string hardboneFileName = config["HardboneFileName"];
			string backboneFileName = config["BackboneFileName"];

			Source source = new Source() { Name = config["SourceName"] };

			await spatialContext.Sources.AddAsync(source, cancellationToken);
			await spatialContext.SaveChangesAsync(cancellationToken);

			Task<ICollection<LineString>> baselines =
				Task.Run(
					() => provider
						.LoadFromFile<LineString>(baselineFileName, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
						.Execute(), cancellationToken);

			Task<ICollection<LineString>> hardbones =
				Task.Run(
					() => provider
						.LoadFromFile<LineString>(hardboneFileName, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
						.Execute(), cancellationToken);

			Task<ICollection<Polygon>> backbones =
				Task.Run(
					() => provider
						.LoadFromFile<Polygon>(backboneFileName, precisionModel, provider.GetRequiredService<ILogger<Processor>>())
						.Execute(), cancellationToken);

			await spatialContext.Set<Baseline>()
				.AddRangeAsync((await baselines).Select(x => new Baseline() { Geometry = x, SourceId = source.Id, }), cancellationToken);

			await spatialContext.Set<Hardbone>()
				.AddRangeAsync((await hardbones).Select(x => new Hardbone() { Geometry = x, SourceId = source.Id, }), cancellationToken);

			await spatialContext.Set<Backbone>()
				.AddRangeAsync((await backbones).Select(x => new Backbone() { Geometry = x, SourceId = source.Id, }), cancellationToken);

			logger.LogInformation("Saving...");
			await spatialContext.SaveChangesAsync(cancellationToken);
		}
	}
}