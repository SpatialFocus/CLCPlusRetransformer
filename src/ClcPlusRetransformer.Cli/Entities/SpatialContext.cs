// <copyright file="SpatialContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;
	using System.Linq;
	using Microsoft.EntityFrameworkCore;

	public class SpatialContext : DbContext
	{
		public SpatialContext(DbContextOptions<SpatialContext> options) : base(options)
		{
		}

		public DbSet<Source> Sources { get; protected set; }

		public DbSet<Tile> Tiles { get; protected set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Source>(entityTypeBuilder =>
			{
				entityTypeBuilder.HasIndex(x => x.Name).IsUnique();

				entityTypeBuilder.HasMany(x => x.Baselines).WithOne(x => x.Source).HasForeignKey(x => x.SourceId);
				entityTypeBuilder.HasMany(x => x.Backbones).WithOne(x => x.Source).HasForeignKey(x => x.SourceId);
				entityTypeBuilder.HasMany(x => x.Hardbones).WithOne(x => x.Source).HasForeignKey(x => x.SourceId);

				entityTypeBuilder.HasMany<Tile>().WithOne(x => x.Source).HasForeignKey(x => x.SourceId);
				entityTypeBuilder.HasMany<ResultGeometry>().WithOne(x => x.Source).HasForeignKey(x => x.SourceId);
			});

			modelBuilder.Entity<Tile>(entityTypeBuilder =>
			{
				entityTypeBuilder.Property<int>("Version").IsRowVersion().HasDefaultValue(0);

				entityTypeBuilder.HasMany(x => x.Geometries).WithOne(x => x.Tile).HasForeignKey(x => x.TileId);
				entityTypeBuilder.HasMany(x => x.GeometriesBuffered).WithOne(x => x.Tile).HasForeignKey(x => x.TileId);
			});

			modelBuilder.Entity<TileGeometry>(entityTypeBuilder =>
			{
				entityTypeBuilder.Property(x => x.RelatedGeometries)
					.HasConversion(ids => string.Join(';', ids),
						text => text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
						ListValueComparer<Guid>.Default());
			});

			modelBuilder.Entity<ResultGeometry>(entityTypeBuilder =>
			{
				entityTypeBuilder.Property<int>("Version").IsRowVersion().HasDefaultValue(0);

				entityTypeBuilder.HasIndex(x => x.OriginId).IsUnique();

				entityTypeBuilder.Property(x => x.RelatedGeometries)
					.HasConversion(ids => string.Join(';', ids.Select(x => x.ToString().ToUpper())),
						text => text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
						ListValueComparer<Guid>.Default());
			});
		}
	}
}