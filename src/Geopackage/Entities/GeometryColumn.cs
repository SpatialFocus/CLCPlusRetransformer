// <copyright file="GeometryColumn.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage.Entities
{
	public class GeometryColumn
	{
		public string ColumnName { get; set; } = string.Empty;

		public string GeometryTypeName { get; set; } = string.Empty;

		public bool M { get; set; }

		public ReferenceSystem ReferenceSystem { get; set; } = null!;

		public int SrsId { get; set; }

		public string TableName { get; set; } = string.Empty;

		public bool Z { get; set; }
	}
}