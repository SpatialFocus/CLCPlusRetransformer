// <copyright file="ServiceProviderExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using ClcPlusRetransformer.Core.Processors;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class ServiceProviderExtension
	{
		public static IProcessor<TGeometryType> FromGeometries<TGeometryType>(this IServiceProvider serviceProvider, string dataName,
			params TGeometryType[] geometries) where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();

			return factory.CreateProcessor("From geometries", dataName, () => geometries);
		}

		public static IProcessor<TGeometryType> LoadFromFile<TGeometryType>(this IServiceProvider serviceProvider, Input input,
			PrecisionModel precisionModel, ILogger<Processor> logger = null) where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();
			return factory.CreateProcessor<TGeometryType>("Load from file", Path.GetFileNameWithoutExtension(input.FileName), () =>
			{
				List<TGeometryType> geometries = ServiceProviderExtension.Read<TGeometryType>(input, precisionModel).ToList();

				logger?.LogDebug("{ProcessorName} [{DataName}] {Count} geometries loaded", "Union", input.FileName, geometries.Count);

				return geometries;
			});
		}

		public static IProcessor<TGeometryType> LoadFromFileAndClip<TGeometryType>(this IServiceProvider serviceProvider, Input input,
			PrecisionModel precisionModel, Geometry otherGeometry, ILogger<Processor> logger = null) where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();

			return factory.CreateProcessor<TGeometryType>("Load from file and clip", Path.GetFileNameWithoutExtension(input.FileName),
				() =>
				{
					List<TGeometryType> geometries = ServiceProviderExtension
						.ReadAndClip<TGeometryType>(input, precisionModel, otherGeometry)
						.ToList();

					logger?.LogDebug("{ProcessorName} [{DataName}] {Count} geometries loaded", "Union", input.FileName, geometries.Count);

					return geometries;
				});
		}

		public static IEnumerable<TGeometryType> Read<TGeometryType>(Input input, PrecisionModel precisionModel)
			where TGeometryType : Geometry
		{
			IEnumerable<TGeometryType> geometries;

			if (input.FileName.EndsWith(".shp"))
			{
				ShapefileReader reader = new ShapefileReader(input.FileName, new GeometryFactory(precisionModel));

				geometries = reader.ReadAll().FlattenAndThrow<TGeometryType>();
			}
			else
			{
				DbContext dbContext =
					new GeoPackageContext(new DbContextOptionsBuilder<DbContext>().UseSqlite($"Data Source={input.FileName}").Options);
				GeometryColumn geometryColumn = dbContext.Set<GeometryColumn>()
					.FromSqlRaw(
						$"SELECT table_name as tablename, column_name as columnname FROM gpkg_geometry_columns WHERE table_name = '{input.LayerName}'")
					.Single();

				GeoPackageGeoReader reader =
					new GeoPackageGeoReader(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory, precisionModel);

				geometries = dbContext.Set<FeatureRow>()
					.FromSqlRaw($"SELECT {geometryColumn.ColumnName} as geometry FROM {input.LayerName}")
					.ToList()
					.SelectMany(x => reader.Read(x.Geometry).FlattenAndThrow<TGeometryType>());
			}

			foreach (TGeometryType geometry in geometries)
			{
				yield return geometry;
			}
		}

		public static IEnumerable<TGeometryType> ReadAndClip<TGeometryType>(Input input, PrecisionModel precisionModel,
			Geometry otherGeometry) where TGeometryType : Geometry
		{
			IEnumerable<TGeometryType> geometries;

			ICollection<TGeometryType> geometriesUnprocessed = new List<TGeometryType>();
			ICollection<Geometry> geometriesToProcess = new List<Geometry>();

			if (input.FileName.EndsWith(".shp"))
			{
				ShapefileReader reader = new ShapefileReader(input.FileName, new GeometryFactory(precisionModel));

				geometries = reader.ReadAll().FlattenAndThrow<TGeometryType>();
			}
			else
			{
				DbContext dbContext =
					new GeoPackageContext(new DbContextOptionsBuilder<DbContext>().UseSqlite($"Data Source={input.FileName}").Options);
				GeometryColumn geometryColumn = dbContext.Set<GeometryColumn>()
					.FromSqlRaw(
						$"SELECT table_name as tablename, column_name as columnname FROM gpkg_geometry_columns WHERE table_name = '{input.LayerName}'")
					.Single();

				GeoPackageGeoReader reader =
					new GeoPackageGeoReader(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory, precisionModel);

				geometries = dbContext.Set<FeatureRow>()
					.FromSqlRaw($"SELECT {geometryColumn.ColumnName} as geometry FROM {input.LayerName}")
					.ToList()
					.SelectMany(x => reader.Read(x.Geometry).FlattenAndThrow<TGeometryType>());
			}

			foreach (TGeometryType geometry in geometries)
			{
				IntersectionMatrix intersectionMatrix = otherGeometry.Relate(geometry);

				if (intersectionMatrix.IsCovers())
				{
					geometriesUnprocessed.Add(geometry);
				}
				else if (intersectionMatrix.IsOverlaps(Dimension.Surface, Dimension.Surface))
				{
					geometriesToProcess.Add(geometry);
				}
			}

			// TODO: Run AsParallel?
			return new GeometryCollection(geometriesToProcess.ToArray()).Intersection(otherGeometry)
				.FlattenAndIgnore<TGeometryType>()
				.Union(geometriesUnprocessed);
		}
	}
}