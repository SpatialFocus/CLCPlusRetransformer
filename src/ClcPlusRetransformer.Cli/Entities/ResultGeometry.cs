// <copyright file="ResultGeometry.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public class ResultGeometry
	{
		public bool Completed { get; set; }

		public int Id { get; set; }

		public bool Locked { get; set; }

		public Guid OriginId { get; set; }

		public Polygon Polygon { get; set; }

		public List<Guid> RelatedGeometries { get; set; } = new();

		public virtual Source Source { get; set; }

		public int SourceId { get; set; }
	}
}