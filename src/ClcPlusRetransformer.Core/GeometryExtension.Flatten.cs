// <copyright file="GeometryExtension.Flatten.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public static partial class GeometryExtension
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
						foreach (TGeometryType geometry2 in geometryCollectionGeometry.FlattenAndIgnore<TGeometryType>())
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
						foreach (TGeometryType geometry2 in geometryCollectionGeometry.FlattenAndThrow<TGeometryType>())
						{
							yield return geometry2;
						}
					}

					break;
				}

				default:
					throw new InvalidOperationException($"Unexpected geometry type {geometry.GetType().Name}");
			}
		}
	}
}