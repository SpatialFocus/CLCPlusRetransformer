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
	using NetTopologySuite.Features;
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
			ShapefileReader reader = new ShapefileReader(fileName, GeometryFactory.Default);

			foreach (TGeometryType geometry in reader.ReadAll().FlattenAndThrow<TGeometryType>())
			{
				yield return geometry;
			}

			//while (reader.Read())
			//{
			//	foreach (TGeometryType geometry in reader.Geometry.FlattenAndThrow<TGeometryType>())
			//	{
			//		yield return geometry;
			//	}
			//}
		}

		public static IEnumerable<IFeature> ReadFeatures<TGeometryType>(string fileName) where TGeometryType : Geometry
		{
			using ShapefileDataReader reader = new ShapefileDataReader(fileName, GeometryFactory.Default);
			DbaseFileHeader header = reader.DbaseHeader;

			while (reader.Read())
			{
				AttributesTable attributesTable = new AttributesTable();

				for (int i = 0; i < header.NumFields; i++)
				{
					DbaseFieldDescriptor fldDescriptor = header.Fields[i];

					attributesTable.Add(fldDescriptor.Name, reader.GetValue(i + 1));
				}

				foreach (TGeometryType geometry in reader.Geometry.FlattenAndThrow<TGeometryType>())
				{
					Feature feature = new Feature
					{
						Attributes = attributesTable,
						Geometry = geometry,
					};

					yield return feature;
				}
			}
		}
	}
}