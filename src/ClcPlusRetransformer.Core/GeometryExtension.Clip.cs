// <copyright file="GeometryExtension.Clip.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Linq;
	using NetTopologySuite.Geometries;

	public static partial class GeometryExtension
	{
		public static IProcessor<TGeometryType> Clip<TGeometryType>(this IProcessor<TGeometryType> container, Geometry otherGeometry)
			where TGeometryType : Geometry
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			// TODO: Compare .AsParallel with Parallel.ForEach
			return container.Chain("Clip",
				(geometries) => geometries.AsParallel()
					.SelectMany(geometry => geometry.Intersection(otherGeometry).FlattenAndIgnore<TGeometryType>())
					.ToList());
		}
	}
}