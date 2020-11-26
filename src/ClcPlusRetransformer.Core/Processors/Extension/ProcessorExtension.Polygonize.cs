// <copyright file="ProcessorExtension.Polygonize.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Operation.Polygonize;

	public static partial class ProcessorExtension
	{
		public static IProcessor<Polygon> Polygonize(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Polygonize", strings => ProcessorExtension.Polygonize(strings).ToList());
		}

		private static IEnumerable<Polygon> Polygonize(ICollection<LineString> geometries)
		{
			Polygonizer polygonizer = new Polygonizer();
			polygonizer.Add(geometries.ToArray());

			return polygonizer.GetPolygons().Cast<Polygon>();
		}
	}
}