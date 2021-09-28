// <copyright file="GeopackageContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage
{
	using Geopackage.Entities;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.EntityFrameworkCore.Infrastructure;

	public abstract class GeopackageContext : DbContext
	{
		protected GeopackageContext(DbContextOptions<GeopackageContext> options) : base(options)
		{
		}

		public DbSet<GeometryColumn> GeometryColumns { get; set; } = null!;

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
			optionsBuilder.UseSnakeCaseNamingConvention().ReplaceService<IModelCacheKeyFactory, DynamicModelCacheKeyFactory>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Content>(e =>
			{
				e.ToTable("gpkg_contents");
				e.HasKey(x => x.TableName);
				e.HasOne<ReferenceSystem>(x => x.ReferenceSystem!).WithMany().HasForeignKey(x => x.SrsId);
				e.Property(x => x.MinX).HasColumnType("DOUBLE");
				e.Property(x => x.MaxX).HasColumnType("DOUBLE");
				e.Property(x => x.MinY).HasColumnType("DOUBLE");
				e.Property(x => x.MaxY).HasColumnType("DOUBLE");

				e.Property(x => x.LastChange).HasColumnType("DATETIME").HasDefaultValueSql("strftime('%Y-%m-%dT%H:%M:%fZ', 'now')");
			});

			modelBuilder.Entity<GeometryColumn>(e =>
			{
				e.ToTable("gpkg_geometry_columns");
				e.HasKey(x => new { x.TableName, x.ColumnName, });
				e.HasOne<ReferenceSystem>(x => x.ReferenceSystem!).WithMany().HasForeignKey(x => x.SrsId);
				e.Property(x => x.Z).HasColumnType("TINYINT");
				e.Property(x => x.M).HasColumnType("TINYINT");
			});

			modelBuilder.Entity<ReferenceSystem>(e =>
			{
				e.ToTable("gpkg_spatial_ref_sys");
				e.HasKey(x => x.SrsId);
			});

			modelBuilder.Entity<ReferenceSystem>()
				.HasData(
					new ReferenceSystem()
					{
						SrsName = "Undefined cartesian SRS",
						SrsId = -1,
						Organization = "NONE",
						OrganizationCoordsysId = -1,
						Definition = "undefined",
						Description = "undefined cartesian coordinate reference system",
					},
					new ReferenceSystem()
					{
						SrsName = "Undefined geographic SRS",
						SrsId = -2,
						Organization = "NONE",
						OrganizationCoordsysId = -2,
						Definition = "undefined",
						Description = "undefined geographic coordinate reference system",
					},
					new ReferenceSystem()
					{
						SrsName = "WGS 84 geodetic",
						SrsId = 4326,
						Organization = "EPSG",
						OrganizationCoordsysId = 4326,
						Definition =
							"GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AXIS[\"Latitude\",NORTH],AXIS[\"Longitude\",EAST],AUTHORITY[\"EPSG\",\"4326\"]]",
						Description = "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid",
					},
					new ReferenceSystem()
					{
						SrsName = "ETRS89-extended / LAEA Europe",
						SrsId = 3035,
						Organization = "EPSG",
						OrganizationCoordsysId = 3035,
						Definition =
							"PROJCS[\"ETRS89-extended / LAEA Europe\",GEOGCS[\"ETRS89\",DATUM[\"European_Terrestrial_Reference_System_1989\",SPHEROID[\"GRS 1980\",6378137,298.257222101]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4258\"]],PROJECTION[\"Lambert_Azimuthal_Equal_Area\"],PARAMETER[\"latitude_of_center\",52],PARAMETER[\"longitude_of_center\",10],PARAMETER[\"false_easting\",4321000],PARAMETER[\"false_northing\",3210000],UNIT[\"metre\",1],AXIS[\"Northing\",NORTH],AXIS[\"Easting\",EAST],AUTHORITY[\"EPSG\",\"3035\"]]",
						Description = null,
					});
		}
	}
}