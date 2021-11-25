// <copyright file="ProcessorExtension.CountTooPrecise.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> CountTooPrecise(this IProcessor<LineString> container, PrecisionModel precisionModel,
			ILogger<Processor> logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("CountTooPrecise",
				(geometries) => ProcessorExtension.CountTooPrecise(geometries, precisionModel, logger).ToList());
		}

		public static IEnumerable<LineString> CountTooPrecise(ICollection<LineString> geometries, PrecisionModel precisionModel,
			ILogger<Processor> logger)
		{
			if (logger == null)
			{
				return geometries;
			}

			int num = 0;
			int decimalPlaces = (int)Math.Round(Math.Log10(precisionModel.Scale));

			Parallel.ForEach(geometries, line =>
			{
				foreach (Coordinate c in line.Coordinates)
				{
					string sX = c.X.ToString(CultureInfo.InvariantCulture);
					string sY = c.Y.ToString(CultureInfo.InvariantCulture);

					int xDecimalPlaces = sX.Split('.').Length > 1 ? sX.Split('.')[1].Length : 0;
					int yDecimalPlaces = sY.Split('.').Length > 1 ? sY.Split('.')[1].Length : 0;

					if (xDecimalPlaces > decimalPlaces || yDecimalPlaces > decimalPlaces)
					{
						Interlocked.Increment(ref num);
						break;
					}
				}
			});

			logger.LogInformation("Number of too precise geometries: {Number}", num);

			return geometries;
		}
	}
}