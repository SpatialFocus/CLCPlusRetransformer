// <copyright file="GeometryExtension.Polygonize.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.ObjectModel;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Noding.Snapround;
	using NetTopologySuite.Operation.Polygonize;

	public static partial class GeometryExtension
	{
		public static IProcessor<Polygon> Polygonize(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Polygonize", (geometries) =>
			{
				MultiLineString multiLineString = new MultiLineString(geometries.ToArray());

				PrecisionModel precisionModel = new PrecisionModel(10 * 1000);
				////var roundedGeom = (MultiLineString)NetTopologySuite.Precision.GeometryPrecisionReducer.ReducePointwise(multiLineString, precisionModel);
				ReadOnlyCollection<LineString> nodedLines = new GeometryNoder(precisionModel).Node(multiLineString.Geometries);

				Polygonizer polygonizer = new Polygonizer();
				polygonizer.Add(new MultiLineString(nodedLines.ToArray()).Union());

				return polygonizer.GetPolygons().Cast<Polygon>().ToList();
			});
		}
	}
}