// <copyright file="TileGeometry.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public class TileGeometry
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public Polygon Polygon { get; set; }

		public List<Guid> RelatedGeometries { get; set; } = new();

		public virtual Tile Tile { get; set; }

		public int TileId { get; set; }
	}
}