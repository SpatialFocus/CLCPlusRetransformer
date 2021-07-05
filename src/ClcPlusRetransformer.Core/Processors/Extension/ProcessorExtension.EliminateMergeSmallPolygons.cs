// <copyright file="ProcessorExtension.EliminateMergeSmallPolygons.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index.Quadtree;

	public static partial class ProcessorExtension
	{
		public static IProcessor<Polygon> EliminateMergeSmallPolygons(this IProcessor<Polygon> container, ILogger logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain<Polygon>("EliminatePolygons", geometries => ProcessorExtension.EliminateMergeSmallPolygons(geometries,
					onProgress: (current, total) =>
					{
						logger?.LogDebug("{ProcessorName} [{DataName}] progress: {Current} / {Total}", "EliminatePolygons",
							container.DataName,
							current, total);
					})
				.ToList());
		}

		public static IEnumerable<Polygon> EliminateMergeSmallPolygons(ICollection<Polygon> polygons, double threshold = 5000,
			Action<int, int> onProgress = null)
		{
			if (polygons == null)
			{
				throw new ArgumentNullException(nameof(polygons));
			}

			List<Polygon> otherPolygons = new List<Polygon>();
			List<Polygon> polygonsToMerge = new List<Polygon>();
			Quadtree<Polygon> polygonsToMergeIndex = new Quadtree<Polygon>();

			foreach (Polygon polygon in polygons)
			{
				if (polygon.Area <= threshold)
				{
					polygonsToMerge.Add(polygon);
					polygonsToMergeIndex.Insert(polygon.EnvelopeInternal, polygon);
				}
				else
				{
					otherPolygons.Add(polygon);
				}
			}

			while (polygonsToMerge.Count > 0)
			{
				Polygon polygon = polygonsToMerge.First();
				polygonsToMerge.Remove(polygon);

				List<Polygon> candidates = polygonsToMergeIndex.Query(polygon.EnvelopeInternal)
					.Where(result => result != polygon && result.Touches(polygon) && polygon.Intersection(result).FlattenAndIgnore<LineString>().Any())
					.ToList();

				if (!candidates.Any())
				{
					continue;
				}

				polygonsToMergeIndex.Remove(polygon.EnvelopeInternal, polygon);

				foreach (Polygon candidate in candidates)
				{
					polygonsToMerge.Remove(candidate);
					polygonsToMergeIndex.Remove(candidate.EnvelopeInternal, candidate);
				}

				polygon = (Polygon)polygon.Union(new MultiPolygon(candidates.ToArray()));

				polygonsToMerge.Add(polygon);
				polygonsToMergeIndex.Insert(polygon.EnvelopeInternal, polygon);
			}

			return polygonsToMergeIndex.QueryAll()
				.Select(x => x.Copy())
				.Cast<Polygon>()
				.Union(otherPolygons.Select(x => x.Copy()).Cast<Polygon>());
		}
	}
}