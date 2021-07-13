// <copyright file="Content.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage.Entities
{
	using System;

	public class Content
	{
		public string DataType { get; set; } = string.Empty;

		public string? Description { get; set; }

		public string? Identifier { get; set; }

		public DateTime LastChange { get; set; }

		public double? MaxX { get; set; }

		public double? MaxY { get; set; }

		public double? MinX { get; set; }

		public double? MinY { get; set; }

		public ReferenceSystem? ReferenceSystem { get; set; }

		public int? SrsId { get; set; }

		public string TableName { get; set; } = string.Empty;
	}
}