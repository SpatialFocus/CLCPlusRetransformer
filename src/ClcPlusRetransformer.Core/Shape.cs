// <copyright file="Shape.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Microsoft.Extensions.DependencyInjection;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class Shape
	{
		public static IProcessor<TGeometryType> Load<TGeometryType>(this IServiceProvider serviceProvider, string fileName)
			where TGeometryType : Geometry
		{
			ProcessorFactory factory = serviceProvider.GetRequiredService<ProcessorFactory>();
			return factory.CreateProcessor<TGeometryType>("Load from file", Path.GetFileNameWithoutExtension(fileName),
				() => Shape.Read<TGeometryType>(fileName).ToList());
		}

		public static IEnumerable<TGeometryType> Read<TGeometryType>(string fileName) where TGeometryType : Geometry
		{
			using ShapefileDataReader reader = new ShapefileDataReader(fileName, GeometryFactory.Fixed);

			while (reader.Read())
			{
				foreach (TGeometryType geometry in reader.Geometry.FlattenAndThrow<TGeometryType>())
				{
					yield return geometry;
				}
			}
		}
	}
}