// <copyright file="GeometryExtension.Difference.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index.Quadtree;

	public static partial class GeometryExtension
	{
		public static IProcessor<LineString> Difference(this IProcessor<LineString> container, ICollection<LineString> others)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Difference", (geometries) =>
			{
				Envelope envelope1 = new MultiLineString(others.ToArray()).EnvelopeInternal;
				Envelope envelope2 = new MultiLineString(others.ToArray()).EnvelopeInternal;

				Envelope newEnvelope = new Envelope(envelope1.MinX < envelope2.MinX ? envelope1.MinX : envelope2.MinX,
					envelope1.MaxX < envelope2.MaxX ? envelope1.MaxX : envelope2.MaxX,
					envelope1.MinY < envelope2.MinY ? envelope1.MinY : envelope2.MinY,
					envelope1.MaxY < envelope2.MaxY ? envelope1.MaxY : envelope2.MaxY);

				Quadtree<LineString> geometryIndex = new Quadtree<LineString>();

				foreach (LineString lineString in geometries)
				{
					geometryIndex.Insert(lineString.EnvelopeInternal, lineString);
				}

				Quadtree<LineString> otherIndex = new Quadtree<LineString>();

				foreach (LineString lineString in others)
				{
					otherIndex.Insert(lineString.EnvelopeInternal, lineString);
				}

				ICollection<Envelope> envelopes = new List<Envelope>();

				int numberOfSplits = 4;

				for (int i = 0; i < numberOfSplits; i++)
				{
					for (int j = 0; j < numberOfSplits; j++)
					{
						envelopes.Add(new Envelope(newEnvelope.MinX + (newEnvelope.Width * (1.0 / numberOfSplits) * i),
							newEnvelope.MinX + (newEnvelope.Width * (1.0 / numberOfSplits) * (i + 1)),
							newEnvelope.MinY + (newEnvelope.Height * (1.0 / numberOfSplits) * j),
							newEnvelope.MinY + (newEnvelope.Height * (1.0 / numberOfSplits) * (j + 1))));
					}
				}

				ConcurrentBag<Geometry> results = new ConcurrentBag<Geometry>();

				Parallel.ForEach(envelopes, (envelope, state, index) =>
				{
					MultiLineString x = new MultiLineString(geometryIndex.Query(envelope).ToArray());
					MultiLineString y = new MultiLineString(otherIndex.Query(envelope).ToArray());

					// Clip to envelope
					x = new MultiLineString(x.Intersection(new GeometryFactory().ToGeometry(envelope))
						.FlattenAndIgnore<LineString>()
						.ToArray());
					y = new MultiLineString(y.Intersection(new GeometryFactory().ToGeometry(envelope))
						.FlattenAndIgnore<LineString>()
						.ToArray());

					results.Add(x.Difference(y));
				});

				return results.SelectMany(result => result.FlattenAndThrow<LineString>()).ToList();
			});
		}
	}
}