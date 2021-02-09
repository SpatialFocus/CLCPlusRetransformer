// <copyright file="TileResult.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public class TileResult
	{
		public Polygon Envelope { get; set; }

		public ICollection<Polygon> Polygons { get; set; } = new List<Polygon>();

		public ICollection<Polygon> UnclippedPolygons { get; set; } = new List<Polygon>();
	}
}