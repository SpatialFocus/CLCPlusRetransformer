// <copyright file="GeometryExtension.LineStringsToPolygon.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Operation.Polygonize;

	public static partial class GeometryExtension
	{
		public static IProcessor<Polygon> LineStringsToPolygon(this IProcessor<LineString> container)
		{
			return container.Chain("LineStringsToPolygon", (geometries) =>
			{
				GeometryCollection lineStringsCollection =
					new GeometryCollection(geometries.Cast<Geometry>().ToArray(), new GeometryFactory());

				Polygonizer polygonizer = new Polygonizer();
				polygonizer.Add(lineStringsCollection.Union());

				return polygonizer.GetPolygons().Cast<Polygon>().ToList();
			});
		}
	}
}