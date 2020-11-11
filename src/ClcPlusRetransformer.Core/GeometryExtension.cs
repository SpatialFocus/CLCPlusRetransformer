// <copyright file="GeometryExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class GeometryExtension
	{
		public static IEnumerable<TGeometryType> FlattenAndIgnore<TGeometryType>(this Geometry geometry)
		{
			if (geometry == null || geometry.IsEmpty)
			{
				yield break;
			}

			switch (geometry)
			{
				case TGeometryType geometry1:
					yield return geometry1;

					break;

				case GeometryCollection geometryCollection:
				{
					foreach (Geometry geometryCollectionGeometry in geometryCollection.Geometries)
					{
						if (geometryCollectionGeometry is TGeometryType geometry2)
						{
							yield return geometry2;
						}
					}

					break;
				}

				default:
					yield break;
			}
		}

		public static IEnumerable<TGeometryType> FlattenAndThrow<TGeometryType>(this Geometry geometry)
		{
			if (geometry == null || geometry.IsEmpty)
			{
				yield break;
			}

			switch (geometry)
			{
				case TGeometryType geometry1:
					yield return geometry1;

					break;

				case GeometryCollection geometryCollection:
				{
					foreach (Geometry geometryCollectionGeometry in geometryCollection.Geometries)
					{
						if (geometryCollectionGeometry is TGeometryType geometry2)
						{
							yield return geometry2;
						}
						else
						{
							throw new InvalidOperationException($"Unexpected geometry type {geometry.GetType().Name}");
						}
					}

					break;
				}
				default:
					throw new InvalidOperationException($"Unexpected geometry type {geometry.GetType().Name}");
			}
		}

		public static IEnumerable<TGeometryType> Intersect<TGeometryType>(this IEnumerable<TGeometryType> geometries,
			Geometry otherGeometry) where TGeometryType : Geometry
		{
			return geometries.SelectMany(geometry => geometry.Intersection(otherGeometry).FlattenAndIgnore<TGeometryType>());
		}

		public static void Save<TGeometryType>(this IEnumerable<TGeometryType> geometries, string fileName) where TGeometryType : Geometry
		{
			using ShapefileWriter writer = new ShapefileWriter(fileName, typeof(TGeometryType).ToShapeGeometryType());

			foreach (TGeometryType geometry in geometries)
			{
				writer.Write(geometry);
			}
		}
	}
}