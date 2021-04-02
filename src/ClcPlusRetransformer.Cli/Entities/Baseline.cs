// <copyright file="Baseline.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using NetTopologySuite.Geometries;

	public class Baseline
	{
		public LineString Geometry { get; set; }

		public int Id { get; set; }

		public virtual Source Source { get; set; }

		public int SourceId { get; set; }
	}
}