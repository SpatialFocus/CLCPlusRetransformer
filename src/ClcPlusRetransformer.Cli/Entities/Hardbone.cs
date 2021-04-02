// <copyright file="Hardbone.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using NetTopologySuite.Geometries;

	public class Hardbone
	{
		public Polygon Geometry { get; set; }

		public int Id { get; set; }

		public virtual Source Source { get; set; }

		public int SourceId { get; set; }
	}
}