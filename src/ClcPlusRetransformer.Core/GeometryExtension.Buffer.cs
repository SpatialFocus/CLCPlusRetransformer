// <copyright file="GeometryExtension.Buffer.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Operation.Buffer;

	public static partial class GeometryExtension
	{
		public static IProcessor<TGeometryType> Buffer<TGeometryType>(this IProcessor<TGeometryType> container, double distance)
			where TGeometryType : Geometry
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Buffer",
				(geometries) => geometries.AsParallel()
					.SelectMany(geometry => geometry.Buffer(distance, new BufferParameters(1, EndCapStyle.Round, JoinStyle.Round, 2)).FlattenAndIgnore<TGeometryType>())
					.ToList());
		}
	}
}