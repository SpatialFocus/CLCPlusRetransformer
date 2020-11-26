// <copyright file="ProcessorExtension.Dissolve.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Dissolve;
	using NetTopologySuite.Geometries;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Dissolve(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Dissolve", (geometries)
				=> ProcessorExtension.Dissolve(geometries).ToList());
		}

		public static IEnumerable<LineString> Dissolve(ICollection<LineString> geometries)
		{
			LineDissolver lineDissolver = new LineDissolver();
			lineDissolver.Add(geometries);
			Geometry geometry = lineDissolver.GetResult();

			return geometry.FlattenAndThrow<LineString>();
		}
	}
}