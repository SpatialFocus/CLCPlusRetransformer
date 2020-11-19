// <copyright file="GeometryExtension.EliminatePolygons.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NetTopologySuite.Geometries;

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

			IDictionary<Polygon, ICollection<Polygon>> findGeometriesToEliminate = GeometryExtension.GetGeometriesToEliminate(polygons);

			while (findGeometriesToEliminate.Keys.Any())
			{
				List<Polygon> untouchedPolygons = polygons.Where(x => !findGeometriesToEliminate.Keys.Contains(x)).ToList();

				List<Polygon> mergedPolygons = findGeometriesToEliminate.Select(x => x.Key.Union(new MultiPolygon(x.Value.ToArray())))
					.Cast<Polygon>()
					.ToList();

				polygons = untouchedPolygons.Union(mergedPolygons).ToList();

				findGeometriesToEliminate = GeometryExtension.GetGeometriesToEliminate(polygons);
			}

			return polygons;
		}

		private static IDictionary<Polygon, ICollection<Polygon>> GetGeometriesToEliminate(ICollection<Polygon> geometries)
		{
			IDictionary<Polygon, ICollection<Polygon>> results = new Dictionary<Polygon, ICollection<Polygon>>();

			List<Polygon> geometriesToEliminate = geometries.Where(x => x.Area < 5000).ToList();

			foreach (Polygon geometryToEliminate in geometriesToEliminate)
			{
				List<Polygon> candidates = geometries
					.Where(result => !geometriesToEliminate.Contains(result) && result.Touches(geometryToEliminate))
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