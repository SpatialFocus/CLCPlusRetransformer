// <copyright file="GeopackageWriteContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage
{
	using Geopackage.Entities;
	using Microsoft.EntityFrameworkCore;

	public class GeopackageWriteContext : GeopackageContext
	{
		public GeopackageWriteContext(DbContextOptions<GeopackageContext> options) : base(options)
		{
		}

		public DbSet<Output> Outputs { get; set; } = null!;

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSnakeCaseNamingConvention();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Output>(e =>
			{
				e.ToTable("Output");
				e.HasKey(x => x.Fid);

				// Would require .UseNetTopologySuite() in context
				////e.Property(x => x.Geom).HasColumnType("MULTIPOLYGON");
			});

			modelBuilder.Entity<Content>()
				.HasData(new Content
				{
					TableName = "Output", DataType = "features", Identifier = "Output", SrsId = 3035,
				});

			modelBuilder.Entity<GeometryColumn>()
				.HasData(new GeometryColumn()
				{
					TableName = "Output", ColumnName = "geom", GeometryTypeName = "MULTIPOLYGON", SrsId = 3035,
				});
		}
	}
}