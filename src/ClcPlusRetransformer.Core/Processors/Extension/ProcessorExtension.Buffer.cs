// <copyright file="ProcessorExtension.Buffer.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Operation.Buffer;

	public static partial class ProcessorExtension
	{
		public static IProcessor<TGeometryType> Buffer<TGeometryType>(this IProcessor<TGeometryType> container, double distance)
			where TGeometryType : Geometry
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Buffer", (geometries) => ProcessorExtension.Buffer(geometries, distance).ToList());
		}

		public static IEnumerable<TGeometryType> Buffer<TGeometryType>(ICollection<TGeometryType> geometries, double distance)
			where TGeometryType : Geometry
		{
			return geometries.AsParallel()
				.SelectMany(geometry => geometry.Buffer(distance, new BufferParameters(1, EndCapStyle.Round, JoinStyle.Round, 2))
					.FlattenAndIgnore<TGeometryType>());
		}
	}
}