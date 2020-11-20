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

	public class PointWithLine : Point
	{
		public PointWithLine(Coordinate coordinate) : base(coordinate)
		{
		}

		public PointWithLine(CoordinateSequence coordinates, GeometryFactory factory) : base(coordinates, factory)
		{
		}

		public PointWithLine(double x, double y, double z) : base(x, y, z)
		{
		}

		public PointWithLine(double x, double y) : base(x, y)
		{
		}

		public LineString LineString { get; set; }
	}

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
				ICollection<LineString> lineStrings = source.Execute().Select(x => x.Copy()).Cast<LineString>().ToList();

				STRtree<Point> tree = new STRtree<Point>();

				foreach (LineString lineString in GeometryExtension.Explode(target))
				{
					foreach (Coordinate coordinate in lineString.Coordinates)
					{
						tree.Insert(lineString.EnvelopeInternal, new PointWithLine(coordinate)
						{
							LineString = lineString,
						});
					}
				}

				PointItemDistance pointItemDistance = new PointItemDistance();

				Parallel.ForEach(lineStrings, (lineString) =>
				{
					Coordinate startCoordinate = GeometryExtension.FindNearestVertexOrPoint(lineString.StartPoint, tree, pointItemDistance);

					if (startCoordinate != null)
					{
						lineString.Coordinates[0] = startCoordinate;
					}

					Coordinate endCoordinate =
						GeometryExtension.FindNearestVertexOrPoint(lineString.EndPoint, tree, pointItemDistance);

					if (endCoordinate != null)
					{
						lineString.Coordinates[^1] = endCoordinate;
					}
				});

				return lineStrings;
			});
		}

		public static IEnumerable<LineString> Explode(IEnumerable<LineString> lineString)
		{
			foreach (LineString s in lineString)
			{
				for (int i = 0; i < s.NumPoints - 1; i++)
				{
					yield return new LineString(new[] { s[i], s[i + 1] });
				}
			}
		}

		private static Coordinate FindNearestVertexOrPoint(Point point, STRtree<Point> tree, PointItemDistance pointItemDistance)
		{
			Envelope envelope = new Envelope(point.Coordinate);
			envelope.ExpandBy(20);

			Point vertex = tree.NearestNeighbour(envelope, point, pointItemDistance);

			// Check if vertex exists ...
			if (point.IsWithinDistance(vertex, 20))
			{
				return vertex.Coordinate;
			}

			// ... fallback to the nearest point on all lineStringCollection
			DistanceOp distanceOperation = tree.Query(envelope)
				.Cast<PointWithLine>()
				.Distinct()
				.Select(x => new DistanceOp(point, x.LineString))
				.OrderBy(distanceOp => distanceOp.Distance()).FirstOrDefault();

			if (distanceOperation?.Distance() <= 20)
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