// <copyright file="LineStringExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors
{
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public static class LineStringExtension
	{
		public static IEnumerable<LineString> Explode(this LineString lineString)
		{
			for (int i = 0; i < lineString.NumPoints - 1; i++)
			{
				yield return new LineString(new[] { lineString[i], lineString[i + 1] });
			}
		}
	}
}