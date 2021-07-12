// <copyright file="GeoPackageContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using Microsoft.EntityFrameworkCore;

	public class GeoPackageContext : DbContext
	{
		public GeoPackageContext(DbContextOptions options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<FeatureRow>().HasNoKey();
			modelBuilder.Entity<GeometryColumn>().HasNoKey();
		}
	}
}