// <copyright file="GeometryExtension.EliminatePolygons.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index;
	using NetTopologySuite.Index.Quadtree;

	public static partial class GeometryExtension
	{
		public static IProcessor<Polygon> EliminatePolygons(this IProcessor<Polygon> processor)
		{
			if (processor == null)
			{
				throw new ArgumentNullException(nameof(processor));
			}

			return processor.Chain<Polygon>("EliminatePolygons",
				geometries => GeometryExtension.EliminatePolygons(geometries).Select(x => x.Copy()).Cast<Polygon>().ToList());
		}

		private static ICollection<Polygon> EliminatePolygons(ICollection<Polygon> polygons)
		{
			if (polygons == null)
			{
				throw new ArgumentNullException(nameof(polygons));
			}

			Quadtree<Polygon> index = new Quadtree<Polygon>();
			List<Polygon> polygonsToEliminateList = new List<Polygon>();

			foreach (Polygon polygon in polygons)
			{
				if (polygon.Area <= 5000)
				{
					polygonsToEliminateList.Add(polygon);
				}
				else
				{
					index.Insert(polygon.EnvelopeInternal, polygon);
				}
			}

			IDictionary<Polygon, ICollection<Polygon>> eliminatePairs = GeometryExtension.GetEliminatePairs(polygonsToEliminateList, index);

			while (eliminatePairs.Keys.Any())
			{
				foreach ((Polygon motherPolygon, ICollection<Polygon> polygonsToEliminate) in eliminatePairs)
				{
					foreach (Polygon polygonToEliminate in polygonsToEliminate)
					{
						polygonsToEliminateList.Remove(polygonToEliminate);
					}

					Polygon mergedPolygon = (Polygon)motherPolygon.Union(new MultiPolygon(polygonsToEliminate.ToArray()));
					index.Remove(motherPolygon.EnvelopeInternal, motherPolygon);
					index.Insert(mergedPolygon.EnvelopeInternal, mergedPolygon);
				}

				eliminatePairs = GeometryExtension.GetEliminatePairs(polygonsToEliminateList, index);
			}

			return index.QueryAll().Select(x => x.Copy()).Cast<Polygon>().Union(polygonsToEliminateList.Select(x => x.Copy()).Cast<Polygon>()).ToList();
		}

		private static IDictionary<Polygon, ICollection<Polygon>> GetEliminatePairs(ICollection<Polygon> geometriesToEliminate, ISpatialIndex<Polygon> index)
		{
			IDictionary<Polygon, ICollection<Polygon>> results = new Dictionary<Polygon, ICollection<Polygon>>();

			foreach (Polygon geometryToEliminate in geometriesToEliminate)
			{
				List<Polygon> candidates = index.Query(geometryToEliminate.EnvelopeInternal)
					.Where(result => result.Touches(geometryToEliminate))
					.ToList();

				if (!candidates.Any())
				{
					continue;
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
					continue;
				}

				Polygon polygon = candidateWithLargestCommonBorder.First().Candidate;

				if (!results.ContainsKey(polygon))
				{
					results[polygon] = new List<Polygon>();
				}

				results[polygon].Add(geometryToEliminate);
			}

			return results;
		}
	}
}