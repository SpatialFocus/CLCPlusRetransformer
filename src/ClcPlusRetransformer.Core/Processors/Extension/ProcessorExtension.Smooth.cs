// <copyright file="ProcessorExtension.Smooth.cs" company="Spatial Focus GmbH">
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
		public static IProcessor<LineString> Smooth(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Smooth", (geometries) => ProcessorExtension.Smooth(geometries).ToList());
		}

		public static IEnumerable<LineString> Smooth(ICollection<LineString> geometries)
		{
			return geometries.AsParallel().Select(ProcessorExtension.Chaikin);
		}

		private static LineString Chaikin(LineString line)
		{
			List<Coordinate> output = new List<Coordinate>();

			Coordinate[] input = line.Coordinates;

			if (input.Length <= 2)
			{
				return line;
			}

			bool addFirstAndLastPoint = !input.First().Equals2D(input.Last());

			if (addFirstAndLastPoint)
			{
				output.Add(new Coordinate(input[0]));
			}

			for (int i = 0; i < input.Length - 1; i++)
			{
				Coordinate p0 = input[i];
				Coordinate p1 = input[i + 1];

				////Coordinate s = new Coordinate((0.5 * p0.X) + (0.5 * p1.X), (0.5 * p0.Y) + (0.5 * p1.Y));
				////output.Add(s);

				Coordinate q = new Coordinate((0.75 * p0.X) + (0.25 * p1.X), (0.75 * p0.Y) + (0.25 * p1.Y));
				Coordinate r = new Coordinate((0.25 * p0.X) + (0.75 * p1.X), (0.25 * p0.Y) + (0.75 * p1.Y));
				output.Add(q);
				output.Add(r);
			}

			output.Add(addFirstAndLastPoint ? new Coordinate(input[^1]) : output.First().Copy());

			return new LineString(output.ToArray());
		}
	}
}