// <copyright file="ProcessorExtension.Union.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors.Extension
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index;
	using NetTopologySuite.Index.Strtree;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Union(this IProcessor<LineString> container, ILogger<Processor> logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Union", (geometries) => ProcessorExtension.Union(geometries, logger, onProgress: progress =>
				{
					logger?.LogDebug("{ProcessorName} [{DataName}] progress: {Progress:P}", "Union", container.DataName, progress);
				})
				.ToList());
		}

		public static IEnumerable<LineString> Union(ICollection<LineString> geometries, ILogger<Processor> logger, int numberOfSplits = 8,
			Action<double> onProgress = null)
		{
			Envelope envelopeGlobal = new Envelope();

			foreach (LineString lineString in geometries)
			{
				envelopeGlobal.ExpandToInclude(lineString.EnvelopeInternal);
			}

			ICollection<Geometry> envelopesLocal =
				envelopeGlobal.Split(numberOfSplits).Select(x => new GeometryFactory().ToGeometry(x)).ToList();

			ISpatialIndex<Geometry> spatialIndexEnvelopes = new STRtree<Geometry>();

			foreach (Geometry envelope in envelopesLocal)
			{
				spatialIndexEnvelopes.Insert(envelope.EnvelopeInternal, envelope);
			}

			IDictionary<Geometry, ConcurrentBag<LineString>> sourceLineStrings =
				envelopesLocal.ToDictionary(x => x, x => new ConcurrentBag<LineString>());

			Parallel.ForEach(geometries, lineString =>
			{
				foreach (Geometry envelope in spatialIndexEnvelopes.Query(lineString.EnvelopeInternal))
				{
					sourceLineStrings[envelope].Add(lineString);
				}
			});

			ConcurrentBag<LineString> results = new ConcurrentBag<LineString>();
			ConcurrentBag<LineString> geometriesRemaining = new ConcurrentBag<LineString>();

			int counter = 0;

			Parallel.ForEach(envelopesLocal, envelope =>
			{
				foreach (LineString lineString in new MultiLineString(sourceLineStrings[envelope].ToArray()).Union()
							.FlattenAndThrow<LineString>())
				{
					if (envelope.Contains(lineString))
					{
						results.Add(lineString);
					}
					else
					{
						geometriesRemaining.Add(lineString);
					}
				}

				int progress = Interlocked.Increment(ref counter);

				int previousProgress = (progress - 1) / (envelopesLocal.Count / 9);
				int currentProgress = progress / (envelopesLocal.Count / 9);

				if (previousProgress < currentProgress)
				{
					onProgress?.Invoke(currentProgress / 10.0);
				}
			});

			ISpatialIndex<LineString> spatialIndexResults = new STRtree<LineString>();

			foreach (LineString geometry in results.ToList())
			{
				spatialIndexResults.Insert(geometry.EnvelopeInternal, geometry);
			}

			HashSet<LineString> geometriesRemainingWithAdditional = new HashSet<LineString>();

			foreach (LineString remainingGeometry in geometriesRemaining.ToList())
			{
				geometriesRemainingWithAdditional.Add(remainingGeometry);

				foreach (LineString additionalRemainingGeometry in spatialIndexResults.Query(remainingGeometry.EnvelopeInternal))
				{
					geometriesRemainingWithAdditional.Add(additionalRemainingGeometry);
				}
			}

			HashSet<LineString> resultsExceptRemaining = new HashSet<LineString>(results);
			resultsExceptRemaining.ExceptWith(geometriesRemainingWithAdditional);

			try
			{
				Geometry resultsRemaining = new GeometryCollection(geometriesRemainingWithAdditional.Cast<Geometry>().ToArray()).Union();

				onProgress?.Invoke(1);

				return resultsExceptRemaining.Union(resultsRemaining.FlattenAndIgnore<LineString>());
			}
			catch (TopologyException exception)
			{
				logger?.LogError("Exception of type {ExceptionType} at {Coordinate}: {Message}", nameof(TopologyException),
					exception.Coordinate, exception.Message);
				throw;
			}
		}

		public static IProcessor<LineString> UnionSimple(this IProcessor<LineString> container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Union",
				(geometries) => new MultiLineString(geometries.ToArray()).Union().FlattenAndIgnore<LineString>().ToList());
		}
	}
}