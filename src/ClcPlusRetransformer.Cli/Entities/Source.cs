// <copyright file="Source.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System.Collections.Generic;

	public class Source
	{
		public virtual ICollection<Backbone> Backbones { get; set; }

		public virtual ICollection<Baseline> Baselines { get; set; }

		public virtual ICollection<Hardbone> Hardbones { get; set; }

		public int Id { get; set; }

		public string Name { get; set; }
	}
}