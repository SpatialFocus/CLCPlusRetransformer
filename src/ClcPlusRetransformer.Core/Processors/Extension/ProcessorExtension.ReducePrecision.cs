// <copyright file="ProcessorExtension.ReducePrecision.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Precision;

	public static partial class ProcessorExtension
	{
		public static IProcessor<TGeometry> ReducePrecision<TGeometry>(this IProcessor<TGeometry> container, PrecisionModel precisionModel)
			where TGeometry : Geometry
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("RemovePrecision", (geometries) => ProcessorExtension.Geometries(geometries, precisionModel).ToList());
		}

		public static IEnumerable<TGeometry> Geometries<TGeometry>(ICollection<TGeometry> geometries, PrecisionModel precisionModel) where TGeometry : Geometry
		{
			GeometryPrecisionReducer reducer = new GeometryPrecisionReducer(precisionModel);

			return geometries.Select(geometry => reducer.Reduce(geometry)).Cast<TGeometry>();
		}
	}
}