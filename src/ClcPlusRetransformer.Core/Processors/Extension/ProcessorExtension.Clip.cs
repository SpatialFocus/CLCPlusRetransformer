// <copyright file="ProcessorExtension.Clip.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Geometries.Prepared;

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
			ConcurrentBag<Geometry> geometriesProcessed = new ConcurrentBag<Geometry>();

			IPreparedGeometry otherGeometryPrepared = new PreparedGeometryFactory().Create(otherGeometry);

			Parallel.ForEach(geometries, geometry =>
			{
				if (otherGeometryPrepared.Covers(geometry))
				{
					geometriesProcessed.Add(geometry);
				}
				else if (otherGeometryPrepared.Intersects(geometry))
				{
					geometriesProcessed.Add(otherGeometry.Intersection(geometry));
				}
			});

			return geometriesProcessed.ToArray().FlattenAndIgnore<TGeometryType>();
		}
	}
}