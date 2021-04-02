// <copyright file="TileGeometryBuffered.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using NetTopologySuite.Geometries;

	public class TileGeometryBuffered
	{
		public int Id { get; set; }

		public Polygon Polygon { get; set; }

		public virtual Tile Tile { get; set; }

		public int TileId { get; set; }
	}
}