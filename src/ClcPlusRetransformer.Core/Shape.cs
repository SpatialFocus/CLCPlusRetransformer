// <copyright file="Shape.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class Shape
	{
		public static IEnumerable<TGeometryType> Load<TGeometryType>(string fileName) where TGeometryType : Geometry
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