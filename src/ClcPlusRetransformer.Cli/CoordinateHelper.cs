// <copyright file="CoordinateHelper.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;

	public static class CoordinateHelper
	{
		public static void Deconstruct<T>(this T[] items, out T x1, out T y1, out T x2, out T y2)
		{
			if (items.Length != 4)
			{
				throw new ArgumentException();
			}

			x1 = items[0];
			y1 = items[1];
			x2 = items[2];
			y2 = items[3];
		}
	}
}