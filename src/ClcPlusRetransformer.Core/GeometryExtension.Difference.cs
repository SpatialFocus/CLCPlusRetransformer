// <copyright file="GeometryExtension.Difference.cs" company="Spatial Focus GmbH">
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
		public static IProcessor<LineString> Difference(this IProcessor<LineString> container, ICollection<LineString> others)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Difference", (geometries) =>
			{
				MultiLineString collection = new MultiLineString(geometries.ToArray());

				return collection.Difference(new MultiLineString(others.ToArray())).FlattenAndThrow<LineString>().ToList();
			});
		}
	}
}