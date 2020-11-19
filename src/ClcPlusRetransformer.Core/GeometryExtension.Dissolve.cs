// <copyright file="GeometryExtension.Dissolve.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Linq;
	using NetTopologySuite.Dissolve;
	using NetTopologySuite.Geometries;

	public static partial class GeometryExtension
	{
		public static IProcessor<LineString> Dissolve(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Dissolve", (geometries) =>
			{
				LineDissolver lineDissolver = new LineDissolver();
				lineDissolver.Add(geometries);
				MultiLineString multi = (MultiLineString)lineDissolver.GetResult();
				return multi.FlattenAndThrow<LineString>().ToList();
			});
		}
	}
}