// <copyright file="GeometryExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using NetTopologySuite.Features;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.IO;

	public static class GeometryExtension
	{
		public static IEnumerable<TGeometryType> FlattenAndIgnore<TGeometryType>(this Geometry geometry)
		{
			if (geometry == null || geometry.IsEmpty)
			{
				yield break;
			}

			switch (geometry)
			{
				case TGeometryType geometry1:
					yield return geometry1;

					break;

				case GeometryCollection geometryCollection:
				{
					foreach (Geometry geometryCollectionGeometry in geometryCollection.Geometries)
					{
						foreach (TGeometryType geometry2 in geometryCollectionGeometry.FlattenAndIgnore<TGeometryType>())
						{
							yield return geometry2;
						}
					}

					break;
				}

				default:
					yield break;
			}
		}

		public static IEnumerable<TGeometryType> FlattenAndThrow<TGeometryType>(this Geometry geometry)
		{
			if (geometry == null || geometry.IsEmpty)
			{
				yield break;
			}

			switch (geometry)
			{
				case TGeometryType geometry1:
					yield return geometry1;

					break;

				case GeometryCollection geometryCollection:
				{
					foreach (Geometry geometryCollectionGeometry in geometryCollection.Geometries)
					{
						foreach (TGeometryType geometry2 in geometryCollectionGeometry.FlattenAndThrow<TGeometryType>())
						{
							yield return geometry2;
						}
					}

					break;
				}

				default:
					throw new InvalidOperationException($"Unexpected geometry type {geometry.GetType().Name}");
			}
		}

		public static void Save<TGeometryType>(this IEnumerable<TGeometryType> geometries, string fileName, PrecisionModel precisionModel, string projectionInfo = null)
			where TGeometryType : Geometry
		{
			while (File.Exists(fileName))
			{
				string[] fileNameParts = Path.GetFileNameWithoutExtension(fileName).Split("_").ToArray();

				if (int.TryParse(fileNameParts.Last(), out int number))
				{
					fileName = Path.Combine(Path.GetDirectoryName(fileName) ?? "./",
						$"{string.Join("_", fileNameParts[..^1])}_{number + 1}{Path.GetExtension(fileName)}");
				}
				else
				{
					fileName = Path.Combine(Path.GetDirectoryName(fileName) ?? "./",
						$"{string.Join("_", fileNameParts)}_1{Path.GetExtension(fileName)}");
				}
			}

			List<IFeature> features = geometries.Select(x => (IFeature)new Feature(x,
					new AttributesTable(new Dictionary<string, object>() { { "Length", x.Length }, { "Area", x.Area } })))
				.ToList();

			DbaseFileHeader fileHeader = new DbaseFileHeader();
			fileHeader.AddColumn("Length", 'N', 18, 11);
			fileHeader.AddColumn("Area", 'N', 18, 11);
			fileHeader.NumRecords = features.Count;

			ShapefileDataWriter shapefileDataWriter = new ShapefileDataWriter(fileName, new GeometryFactory(precisionModel)) { Header = fileHeader, };

			shapefileDataWriter.Write(features);

			if (projectionInfo != null)
			{
				File.WriteAllText(Path.ChangeExtension(fileName, "prj"), projectionInfo);
			}
		}

		public static void Save<TGeometryType>(this TGeometryType geometry, string fileName, string projectionInfo = null)
			where TGeometryType : Geometry
		{
			while (File.Exists(fileName))
			{
				string[] fileNameParts = Path.GetFileNameWithoutExtension(fileName).Split("_").ToArray();

				if (int.TryParse(fileNameParts.Last(), out int number))
				{
					fileName = Path.Combine(Path.GetDirectoryName(fileName) ?? "./",
						$"{string.Join("_", fileNameParts[..^1])}_{number + 1}{Path.GetExtension(fileName)}");
				}
				else
				{
					fileName = Path.Combine(Path.GetDirectoryName(fileName) ?? "./",
						$"{string.Join("_", fileNameParts)}_1{Path.GetExtension(fileName)}");
				}
			}

			using ShapefileWriter writer = new ShapefileWriter(fileName, typeof(TGeometryType).ToShapeGeometryType());

			writer.Write(geometry);

			if (projectionInfo != null)
			{
				File.WriteAllText(Path.ChangeExtension(fileName, "prj"), projectionInfo);
			}
		}
	}
}