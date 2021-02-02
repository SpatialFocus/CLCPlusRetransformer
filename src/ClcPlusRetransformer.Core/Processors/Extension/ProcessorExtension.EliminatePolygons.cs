﻿// <copyright file="ProcessorExtension.EliminatePolygons.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index;
	using NetTopologySuite.Index.Quadtree;

	public static partial class ProcessorExtension
	{
		public static IProcessor<Polygon> EliminatePolygons(this IProcessor<Polygon> container, ILogger logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain<Polygon>("EliminatePolygons",
				geometries => ProcessorExtension.EliminatePolygons(geometries, onProgress: (current, total) =>
				{
					logger?.LogDebug("{ProcessorName} [{DataName}] progress: {Current} / {Total}", "Union", container.DataName, current, total);
				}).ToList());
		}

		public static IEnumerable<Polygon> EliminatePolygons(ICollection<Polygon> polygons, double threshold = 5000, Action<int, int> onProgress = null)
		{
			if (polygons == null)
			{
				throw new ArgumentNullException(nameof(polygons));
			}

			Quadtree<Polygon> spatialIndex = new Quadtree<Polygon>();
			List<Polygon> polygonsToEliminateList = new List<Polygon>();

			foreach (Polygon polygon in polygons)
			{
				if (polygon.Area <= threshold)
				{
					polygonsToEliminateList.Add(polygon);
				}
				else
				{
					spatialIndex.Insert(polygon.EnvelopeInternal, polygon);
				}
			}

			IDictionary<Polygon, ConcurrentBag<Polygon>> eliminatePairs =
				ProcessorExtension.GetEliminatePairs(polygonsToEliminateList, spatialIndex, onProgress);

			while (eliminatePairs.Keys.Any())
			{
				foreach ((Polygon motherPolygon, ConcurrentBag<Polygon> polygonsToEliminate) in eliminatePairs)
				{
					foreach (Polygon polygonToEliminate in polygonsToEliminate)
					{
						polygonsToEliminateList.Remove(polygonToEliminate);
					}

					Polygon mergedPolygon = (Polygon)motherPolygon.Union(new MultiPolygon(polygonsToEliminate.ToArray()));
					spatialIndex.Remove(motherPolygon.EnvelopeInternal, motherPolygon);
					spatialIndex.Insert(mergedPolygon.EnvelopeInternal, mergedPolygon);
				}

				eliminatePairs = ProcessorExtension.GetEliminatePairs(polygonsToEliminateList, spatialIndex, onProgress);
			}

			return spatialIndex.QueryAll()
				.Select(x => x.Copy())
				.Cast<Polygon>()
				.Union(polygonsToEliminateList.Select(x => x.Copy()).Cast<Polygon>());
		}

		private static IDictionary<Polygon, ConcurrentBag<Polygon>> GetEliminatePairs(ICollection<Polygon> geometriesToEliminate,
			ISpatialIndex<Polygon> index, Action<int, int> onProgress = null)
		{
			// Force ConcurrentDictionary, otherwise TryAdd will refer to the IDictionary extension method
			// See https://github.com/dotnet/runtime/issues/30451
			ConcurrentDictionary<Polygon, ConcurrentBag<Polygon>> results = new ConcurrentDictionary<Polygon, ConcurrentBag<Polygon>>();

			int count = 0;
			int total = geometriesToEliminate.Count;

			Parallel.ForEach(geometriesToEliminate, geometryToEliminate =>
			{
				int current = Interlocked.Increment(ref count);

				if (current % 1000 == 0)
				{
					onProgress?.Invoke(current, total);
				}

				List<Polygon> candidates = index.Query(geometryToEliminate.EnvelopeInternal)
					.Where(result => result.Touches(geometryToEliminate))
					.ToList();

				if (!candidates.Any())
				{
					return;
				}

				var candidateWithLargestCommonBorder = candidates
					.Select(candidate => new
					{
						Candidate = candidate,
						Intersection = candidate.Intersection(geometryToEliminate).FlattenAndIgnore<LineString>().ToList(),
					})
					.Where(candidate => candidate.Intersection.Any())
					.OrderByDescending(candidate => candidate.Intersection.Sum(lineString => lineString.Length))
					.ToList();

				if (!candidateWithLargestCommonBorder.Any())
				{
					return;
				}

				Polygon polygon = candidateWithLargestCommonBorder.First().Candidate;

				results.TryAdd(polygon, new ConcurrentBag<Polygon>());
				results[polygon].Add(geometryToEliminate);
			});

			if (total % 100 != 0)
			{
				onProgress?.Invoke(total, total);
			}

			return results;
		}
	}
}