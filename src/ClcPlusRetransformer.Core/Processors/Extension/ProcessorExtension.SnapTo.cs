// <copyright file="ProcessorExtension.SnapTo.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index.Strtree;
	using NetTopologySuite.Operation.Distance;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> SnapTo(this IProcessor<LineString> source, ICollection<LineString> targetLineStrings)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			return source.Chain<LineString>("SnapTo", (geometries) => ProcessorExtension.SnapTo(geometries, targetLineStrings).ToList());
		}

		public static IEnumerable<LineString> SnapTo(ICollection<LineString> lineStrings, ICollection<LineString> targetLineStrings)
		{
			lineStrings = lineStrings.Select(x => x.Copy()).Cast<LineString>().ToList();
			STRtree<Point> tree = new STRtree<Point>();

			foreach (LineString lineString in targetLineStrings.SelectMany(x => x.Explode()))
			{
				foreach (Coordinate coordinate in lineString.Coordinates)
				{
					tree.Insert(lineString.EnvelopeInternal, new PointWithLine(coordinate) { LineString = lineString, });
				}
			}

			PointItemDistance pointItemDistance = new PointItemDistance();

			Parallel.ForEach(lineStrings, (lineString) =>
			{
				Coordinate startCoordinate =
					ProcessorExtension.FindNearestVertexOrPoint(lineString.StartPoint, lineString.GetPointN(1), tree, pointItemDistance);

				if (startCoordinate != null)
				{
					lineString.Coordinates[0] = startCoordinate;
				}

				Coordinate endCoordinate = ProcessorExtension.FindNearestVertexOrPoint(lineString.EndPoint,
					lineString.GetPointN(lineString.NumPoints - 2), tree, pointItemDistance);

				if (endCoordinate != null)
				{
					lineString.Coordinates[^1] = endCoordinate;
				}
			});

			return lineStrings;
		}

		private static Coordinate FindNearestVertexOrPoint(Point point, Point point2, STRtree<Point> tree,
			PointItemDistance pointItemDistance)
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
			ICollection<LineString> lineStrings = tree.Query(envelope).Cast<PointWithLine>().Select(x => x.LineString).Distinct().ToList();

			if (lineStrings.Any(x => new DistanceOp(point, x).Distance() <= 20))
			{
				DistanceOp distanceOperation = lineStrings.Select(x => new DistanceOp(point2, x))
					.OrderBy(distanceOp => distanceOp.Distance())
					.First();

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

		private class PointWithLine : Point
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
	}
}