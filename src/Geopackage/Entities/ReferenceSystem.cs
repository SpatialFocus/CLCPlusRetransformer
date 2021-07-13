// <copyright file="ReferenceSystem.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage.Entities
{
	public class ReferenceSystem
	{
		public string Definition { get; set; } = string.Empty;

		public string? Description { get; set; }

		public string Organization { get; set; } = string.Empty;

		public int OrganizationCoordsysId { get; set; }

		public int SrsId { get; set; }

		public string SrsName { get; set; } = string.Empty;
	}
}