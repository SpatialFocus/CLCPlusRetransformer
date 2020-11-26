// <copyright file="ProcessorExtension.PolygonsToLines.cs" company="Spatial Focus GmbH">
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
		public static IProcessor<LineString> PolygonsToLines(this IProcessor<Polygon> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Polygons to lines", (geometries) => ProcessorExtension.PolygonsToLines(geometries).ToList());
		}

		public static ParallelQuery<LineString> PolygonsToLines(ICollection<Polygon> geometries)
		{
			return geometries.AsParallel()
				.SelectMany(geometry => new List<LineString>(geometry.InteriorRings.Select(x => x)) { geometry.ExteriorRing })
				.Select(x => x.Copy())
				.Cast<LineString>();
		}
	}
}