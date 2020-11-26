// <copyright file="ProcessorExtension.Node.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Noding.Snapround;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Node(this IProcessor<LineString> container, PrecisionModel precisionModel)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Node", (geometries) => ProcessorExtension.LineStrings(geometries, precisionModel).ToList());
		}

		public static IEnumerable<LineString> LineStrings(ICollection<LineString> geometries, PrecisionModel precisionModel)
		{
			// TODO: Do we need to create a MultiLineString?
			MultiLineString multiLineString = new MultiLineString(geometries.ToArray());
			return new GeometryNoder(precisionModel).Node(multiLineString.Geometries);
		}
	}
}