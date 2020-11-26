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
	using Microsoft.Extensions.DependencyInjection;
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

		public static IProcessor<TGeometryType> LoadFromFile<TGeometryType>(this IServiceProvider serviceProvider, string fileName, PrecisionModel precisionModel)
			where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();
			return factory.CreateProcessor<TGeometryType>("Load from file", Path.GetFileNameWithoutExtension(fileName),
				() => ServiceProviderExtension.Read<TGeometryType>(fileName, precisionModel).ToList());
		}

		public static IProcessor<TGeometryType> LoadFromFileAndClip<TGeometryType>(this IServiceProvider serviceProvider, string fileName,
			PrecisionModel precisionModel, Geometry otherGeometry) where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();

			return factory.CreateProcessor<TGeometryType>("Load from file and clip", Path.GetFileNameWithoutExtension(fileName),
				() => ServiceProviderExtension.ReadAndClip<TGeometryType>(fileName, precisionModel, otherGeometry).ToList());
		}

		public static IEnumerable<TGeometryType> Read<TGeometryType>(string fileName, PrecisionModel precisionModel) where TGeometryType : Geometry
		{
			ShapefileReader reader = new ShapefileReader(fileName, new GeometryFactory(precisionModel));

			foreach (TGeometryType geometry in reader.ReadAll().FlattenAndThrow<TGeometryType>())
			{
				yield return geometry;
			}
		}

		public static IEnumerable<TGeometryType> ReadAndClip<TGeometryType>(string fileName, PrecisionModel precisionModel,
			Geometry otherGeometry) where TGeometryType : Geometry
		{
			ShapefileReader reader = new ShapefileReader(fileName, new GeometryFactory(precisionModel));

			ICollection<TGeometryType> geometriesUnprocessed = new List<TGeometryType>();
			ICollection<Geometry> geometriesToProcess = new List<Geometry>();

			foreach (TGeometryType geometry in reader.ReadAll().FlattenAndThrow<TGeometryType>())
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