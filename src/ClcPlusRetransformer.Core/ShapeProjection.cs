// <copyright file="ShapeProjection.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System.IO;

	public static class ShapeProjection
	{
		public static string ReadProjectionInfo(string fileName)
		{
			string projectionFile = Path.ChangeExtension(fileName, ".prj");
			return File.ReadAllText(projectionFile);
		}
	}
}