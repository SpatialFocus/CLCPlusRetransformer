// <copyright file="ListValueComparer.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli.Entities
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.EntityFrameworkCore.ChangeTracking;

	public class ListValueComparer<T>
	{
		public static ValueComparer<List<T>> Default()
		{
			return new ValueComparer<List<T>>((c1, c2) => c1.SequenceEqual(c2),
				c => c.Aggregate(0, (index, value) => HashCode.Combine(index, value != null ? value.GetHashCode() : 0)), c => c.ToList());
		}
	}
}