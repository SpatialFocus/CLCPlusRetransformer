// <copyright file="ProcessorExtension.Simplify.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Simplify(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Simplify", (geometries) => ProcessorExtension.Simplify(geometries).ToList());
		}

		public static IEnumerable<LineString> Simplify(ICollection<LineString> geometries)
		{
			return geometries.Select(x => ProcessorExtension.DouglasPeucker(x, 16));
		}

		private static LineString DouglasPeucker(LineString line, int tolerance = 16)
		{
			List<Point> points = line.Coordinates.Select(x => new Point(x.X, x.Y)).ToList();
			List<Point> output = new();

			ProcessorExtension.RamerDouglasPeucker(points, tolerance, output);

			return new LineString(output.Select(x => new Coordinate(x.X, x.Y)).ToArray());
		}

		private static double PerpendicularDistance(Point pt, Point lineStart, Point lineEnd)
		{
			double dx = lineEnd.X - lineStart.X;
			double dy = lineEnd.Y - lineStart.Y;

			// Normalize
			double mag = Math.Sqrt((dx * dx) + (dy * dy));

			if (mag > 0.0)
			{
				dx /= mag;
				dy /= mag;
			}

			double pvx = pt.X - lineStart.X;
			double pvy = pt.Y - lineStart.Y;

			// Get dot product (project pv onto normalized direction)
			double pvdot = (dx * pvx) + (dy * pvy);

			// Scale line direction vector and subtract it from pv
			double ax = pvx - (pvdot * dx);
			double ay = pvy - (pvdot * dy);

			return Math.Sqrt((ax * ax) + (ay * ay));
		}

		private static void RamerDouglasPeucker(List<Point> pointList, double epsilon, List<Point> output)
		{
			if (pointList.Count < 2)
			{
				throw new ArgumentOutOfRangeException("Not enough points to simplify");
			}

			// Find the point with the maximum distance from line between the start and end
			double dmax = 0.0;
			int index = 0;
			int end = pointList.Count - 1;

			for (int i = 1; i < end; ++i)
			{
				double d = ProcessorExtension.PerpendicularDistance(pointList[i], pointList[0], pointList[end]);

				if (d > dmax)
				{
					index = i;
					dmax = d;
				}
			}

			// If max distance is greater than epsilon, recursively simplify
			if (dmax > epsilon)
			{
				List<Point> recResults1 = new List<Point>();
				List<Point> recResults2 = new List<Point>();
				List<Point> firstLine = pointList.Take(index + 1).ToList();
				List<Point> lastLine = pointList.Skip(index).ToList();
				ProcessorExtension.RamerDouglasPeucker(firstLine, epsilon, recResults1);
				ProcessorExtension.RamerDouglasPeucker(lastLine, epsilon, recResults2);

				// build the result list
				output.AddRange(recResults1.Take(recResults1.Count - 1));
				output.AddRange(recResults2);

				if (output.Count < 2)
				{
					throw new Exception("Problem assembling output");
				}
			}
			else
			{
				// Just return start and end points
				output.Clear();
				output.Add(pointList[0]);
				output.Add(pointList[pointList.Count - 1]);
			}
		}
	}
}