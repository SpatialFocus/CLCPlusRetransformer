// <copyright file="TypeExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class TypeExtension
	{
		public static ShapeGeometryType ToShapeGeometryType(this Type type)
		{
			if (type == typeof(Point))
			{
				return ShapeGeometryType.Point;
			}

			if (type == typeof(LineString) || type == typeof(MultiLineString))
			{
				return ShapeGeometryType.LineString;
			}

			if (type == typeof(Polygon))
			{
				return ShapeGeometryType.Polygon;
			}

			throw new InvalidOperationException();
		}
	}
}