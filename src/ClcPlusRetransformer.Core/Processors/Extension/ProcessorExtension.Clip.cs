// <copyright file="ProcessorExtension.Clip.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;

	public static partial class ProcessorExtension
	{
		public static IProcessor<TGeometryType> Clip<TGeometryType>(this IProcessor<TGeometryType> container, Geometry otherGeometry)
			where TGeometryType : Geometry
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Clip", (geometries) => ProcessorExtension.Clip(geometries, otherGeometry).ToList());
		}

		public static IEnumerable<TGeometryType> Clip<TGeometryType>(ICollection<TGeometryType> geometries, Geometry otherGeometry)
			where TGeometryType : Geometry
		{
			return geometries.AsParallel().SelectMany(geometry => geometry.Intersection(otherGeometry).FlattenAndIgnore<TGeometryType>());
		}
	}
}