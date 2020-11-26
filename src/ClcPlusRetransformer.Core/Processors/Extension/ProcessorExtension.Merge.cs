// <copyright file="ProcessorExtension.Merge.cs" company="Spatial Focus GmbH">
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
		public static IProcessor<LineString> Merge(this IProcessor<LineString> container, ICollection<LineString> others)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Merge", (geometries) => ProcessorExtension.Merge(geometries, others).ToList());
		}

		public static IEnumerable<LineString> Merge(ICollection<LineString> geometries, ICollection<LineString> others)
		{
			List<LineString> lineStrings = geometries.ToList();
			lineStrings.AddRange(others);

			return lineStrings.Select(x => x.Copy()).Union(others.Select(x => x.Copy())).Cast<LineString>();
		}
	}
}