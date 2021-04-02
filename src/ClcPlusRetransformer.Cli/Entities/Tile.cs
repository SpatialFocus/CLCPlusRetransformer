// <copyright file="Tile.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System.Collections.Generic;

	public class Tile
	{
		// TODO: Connect tile to source and move cell size there?
		public int CellSizeInMeters { get; set; }

		public int EastOfOrigin { get; set; }

		public virtual ICollection<TileGeometry> Geometries { get; set; }

		public virtual ICollection<TileGeometryBuffered> GeometriesBuffered { get; set; }

		public int Id { get; set; }

		public bool Locked { get; set; }

		public int NorthOfOrigin { get; set; }

		public virtual Source Source { get; set; }

		public int SourceId { get; set; }

		public TileStatus TileStatus { get; set; }
	}
}