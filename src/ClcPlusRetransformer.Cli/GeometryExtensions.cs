// <copyright file="GeometryExtensions.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ClcPlusRetransformer.Cli.Entities;
	using Microsoft.EntityFrameworkCore;

	public static class GeometryExtensions
	{
		public static ICollection<Guid> ExtendedRelatedGeometryIds(this ResultGeometry resultGeometry, SpatialContext context)
		{
			List<Guid> relatedGeometryIds = resultGeometry.RelatedGeometries.Except(new[] { resultGeometry.OriginId }).ToList();

			return relatedGeometryIds
				.Concat(relatedGeometryIds.SelectMany(x =>
						context.Set<ResultGeometry>()
							.FromSqlRaw($"SELECT * FROM ResultGeometry WHERE RelatedGeometries LIKE '%{x.ToString().ToUpper()}%'"))
					.Select(x => x.OriginId))
				.Except(new[] { resultGeometry.OriginId })
				.Distinct()
				.ToList();
		}
	}
}