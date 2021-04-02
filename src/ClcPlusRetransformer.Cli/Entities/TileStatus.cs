// <copyright file="TileStatus.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;

	[Flags]
	public enum TileStatus
	{
		Created = 0,

		Processed = 1,

		MergedNorth = 2,

		MergedEast = 4,

		MergedSouth = 8,

		MergedWest = 16,

		Merged = TileStatus.MergedNorth | TileStatus.MergedEast | TileStatus.MergedSouth | TileStatus.MergedWest,

		Exported
	}
}