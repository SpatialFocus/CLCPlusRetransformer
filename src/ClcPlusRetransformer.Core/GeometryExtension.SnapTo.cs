// <copyright file="GeometryExtension.SnapTo.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index.Strtree;
	using NetTopologySuite.Operation.Distance;

	public static partial class GeometryExtension
	{
		public static IProcessor<LineString> SnapTo(this IProcessor<LineString> source, ICollection<LineString> target)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			return source.Chain<LineString>("SnapTo", (geometries) =>
			{
				GeometryCollection targetGeometryCollection = new GeometryCollection(target.Cast<Geometry>().ToArray());

				ICollection<LineString> lineStrings = source.Execute().Select(x => x.Copy()).Cast<LineString>().ToList();

				STRtree<Point> tree = new STRtree<Point>();

				foreach (Coordinate coordinate in target.SelectMany(x => x.Coordinates))
				{
					tree.Insert(new Envelope(coordinate), new Point(coordinate));
				}

				PointItemDistance pointItemDistance = new PointItemDistance();

				Parallel.ForEach(lineStrings, (lineString) =>
				{
					Coordinate startCoordinate = GeometryExtension.FindNearestVertexOrPoint(lineString.StartPoint, tree, pointItemDistance,
						targetGeometryCollection);

					if (startCoordinate != null)
					{
						lineString.Coordinates[0] = startCoordinate;
					}

					Coordinate endCoordinate =
						GeometryExtension.FindNearestVertexOrPoint(lineString.EndPoint, tree, pointItemDistance, targetGeometryCollection);

					if (endCoordinate != null)
					{
						lineString.Coordinates[^1] = endCoordinate;
					}
				});

				return lineStrings;
			});
		}

		private static Coordinate FindNearestVertexOrPoint(Point point, STRtree<Point> tree, PointItemDistance pointItemDistance,
			GeometryCollection targetGeometryCollection)
		{
			Envelope envelope = new Envelope(point.Coordinate);
			envelope.ExpandBy(20);

			Point vertex = tree.NearestNeighbour(envelope, point, pointItemDistance);

			// Check if vertex exists ...
			if (point.IsWithinDistance(vertex, 20))
			{
				return vertex.Coordinate;
			}

			// ... fallback to a nearest point on lineStringCollection
			DistanceOp distanceOperation = new DistanceOp(point, targetGeometryCollection);

			if (distanceOperation.Distance() <= 20)
			{
				return distanceOperation.NearestPoints()[1];
			}

			return null;
		}

		private class PointItemDistance : IItemDistance<Envelope, Point>
		{
			public double Distance(IBoundable<Envelope, Point> item1, IBoundable<Envelope, Point> item2)
			{
				Point point1 = item1.Item;
				Point point2 = item2.Item;

				return point1.Distance(point2);
			}
		}
	}
}