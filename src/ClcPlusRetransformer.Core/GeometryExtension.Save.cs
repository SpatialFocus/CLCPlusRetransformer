// <copyright file="GeometryExtension.Save.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static partial class GeometryExtension
	{
		public static void Save<TGeometryType>(this IEnumerable<TGeometryType> geometries, string fileName) where TGeometryType : Geometry
		{
			using ShapefileWriter writer = new ShapefileWriter(fileName, typeof(TGeometryType).ToShapeGeometryType());

			foreach (TGeometryType geometry in geometries)
			{
				writer.Write(geometry);
			}
		}

		public static void Save<TGeometryType>(this TGeometryType geometry, string fileName) where TGeometryType : Geometry
		{
			using ShapefileWriter writer = new ShapefileWriter(fileName, typeof(TGeometryType).ToShapeGeometryType());

			writer.Write(geometry);
		}
	}
}