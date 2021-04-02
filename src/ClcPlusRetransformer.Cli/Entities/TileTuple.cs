// <copyright file="TileTuple.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;

	public class TileTuple : Tuple<Tile, Tile>
	{
		public TileTuple(Tile item1, Tile item2) : base(item1, item2)
		{
		}

		public Tile Tile1 => Item1;

		public Tile Tile2 => Item2;
	}
}