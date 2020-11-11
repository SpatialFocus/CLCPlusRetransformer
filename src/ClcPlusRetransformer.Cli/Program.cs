// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Linq;
	using ClcPlusRetransformer.Core;
	using NetTopologySuite.Geometries;

	internal class Program
	{
		private static void Main(string[] args)
		{
			string aoiFileName = @"data\AOI_10k.shp";

			string baselineFileName = @"data\Hardbone_Baseline.shp";
			string hardboneFileName = @"data\Hardbone_Polygons.shp";
			string backboneFileName = @"data\Backbone_Polygons.shp";

			(string fileName, Type geometryType)[] files =
			{
				(baselineFileName, typeof(LineString)), (hardboneFileName, typeof(Polygon)), (backboneFileName, typeof(Polygon))
			};

			Geometry areaOfInterest = Shape.Load<Polygon>(aoiFileName).Single();

			Shape.Load<LineString>(baselineFileName).Intersect(areaOfInterest).Save("baselines.shp");
			Shape.Load<Polygon>(hardboneFileName).Intersect(areaOfInterest).Save("hardbones.shp");
			Shape.Load<Polygon>(backboneFileName).Intersect(areaOfInterest).Save("backbones.shp");
		}
	}
}