// <copyright file="GeometryExtension.Merge.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;

	public static partial class GeometryExtension
	{
		public static IProcessor<LineString> Merge(this IProcessor<LineString> container, ICollection<LineString> others)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Merge", (geometries) =>
			{
				List<LineString> lineStrings = geometries.ToList();
				lineStrings.AddRange(others);
				return lineStrings;
			});
		}
	}
}