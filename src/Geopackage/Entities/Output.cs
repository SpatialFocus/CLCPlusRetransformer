// <copyright file="Output.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage.Entities
{
	public class Output
	{
		public double Area { get; set; }

		public int Fid { get; set; }

		public byte[] Geom { get; set; } = null!;

		public int Id { get; set; }

		public double Length { get; set; }
	}
}