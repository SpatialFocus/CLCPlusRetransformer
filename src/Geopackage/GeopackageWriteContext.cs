// <copyright file="GeopackageWriteContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage
{
	using Geopackage.Entities;
	using Microsoft.EntityFrameworkCore;

	public class GeopackageWriteContext : GeopackageContext
	{
		public GeopackageWriteContext(DbContextOptions<GeopackageContext> options, string layerName) : base(options)
		{
			LayerName = layerName;
		}

		public string LayerName { get; }

		public DbSet<Output> Outputs { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Output>(e =>
			{
				e.ToTable(LayerName);
				e.HasKey(x => x.Fid);

				// Would require .UseNetTopologySuite() in context
				////e.Property(x => x.Geom).HasColumnType("MULTIPOLYGON");
			});

			modelBuilder.Entity<Content>()
				.HasData(new Content
				{
					TableName = LayerName, DataType = "features", Identifier = LayerName, SrsId = 3035,
				});

			modelBuilder.Entity<GeometryColumn>()
				.HasData(new GeometryColumn()
				{
					TableName = LayerName, ColumnName = "geom", GeometryTypeName = "MULTIPOLYGON", SrsId = 3035,
				});
		}
	}
}