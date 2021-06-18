// <copyright file="ProcessorExtension.Simplify.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Simplify;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Simplify(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Simplify", (geometries) => ProcessorExtension.Simplify(geometries).ToList());
		}

		public static IEnumerable<LineString> Simplify(ICollection<LineString> geometries)
		{
			return geometries.Select(x => (LineString)DouglasPeuckerSimplifier.Simplify(x, 15));
		}
	}
}