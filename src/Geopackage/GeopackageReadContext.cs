// <copyright file="GeopackageReadContext.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage
{
	using Geopackage.Entities;
	using Microsoft.EntityFrameworkCore;

	public class GeopackageReadContext : GeopackageContext
	{
		public GeopackageReadContext(DbContextOptions<GeopackageContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<FeatureRow>().HasNoKey();
		}
	}
}