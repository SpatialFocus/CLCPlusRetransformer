// <copyright file="ProcessorExtension.Difference.cs" company="Spatial Focus GmbH">
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
	using NetTopologySuite.Dissolve;
	using NetTopologySuite.Geometries;
	using NetTopologySuite.Index;
	using NetTopologySuite.Index.Strtree;

	public static partial class ProcessorExtension
	{
		public static IProcessor<LineString> Difference(this IProcessor<LineString> container, ICollection<LineString> others,
			ILogger<Processor> logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("Difference", (geometries) => ProcessorExtension.Difference(geometries, others, onProgress: progress =>
				{
					logger?.LogDebug("{ProcessorName} [{DataName}] progress: {Progress:P}", "Difference", container.DataName, progress);
				})
				.ToList());
		}

		public static IEnumerable<LineString> Difference(ICollection<LineString> geometries, ICollection<LineString> others,
			int batchSize = 8, Action<double> onProgress = null)
		{
			ISpatialIndex<LineString> spatialIndexOthers = new STRtree<LineString>();

			foreach (LineString lineString in others)
			{
				foreach (LineString componentLineString in lineString.Explode())
				{
					spatialIndexOthers.Insert(componentLineString.EnvelopeInternal, lineString);
				}
			}

			ConcurrentBag<Geometry> results = new ConcurrentBag<Geometry>();

			int counter = 0;

			Parallel.ForEach(ProcessorExtension.Batch(geometries, spatialIndexOthers, batchSize), (geometriesBatched, state, index) =>
			{
				foreach (LineString lineString in geometriesBatched.Ignored)
				{
					results.Add(lineString);
				}

				if (!geometriesBatched.Sources.Any())
				{
					return;
				}

				LineDissolver lineDissolver = new LineDissolver();
				lineDissolver.Add(geometriesBatched.Targets);
				Geometry targetLineStrings = lineDissolver.GetResult();

				results.Add(new MultiLineString(geometriesBatched.Sources.ToArray()).Difference(targetLineStrings));

				int ignoredCount = geometriesBatched.Ignored.Count();
				int processedCount = geometriesBatched.Sources.Count();

				int progress = Interlocked.Add(ref counter, processedCount + ignoredCount);

				int previousProgress = (progress - processedCount - ignoredCount) / (geometries.Count / 20);
				int currentProgress = progress / (geometries.Count / 20);

				if (previousProgress < currentProgress)
				{
					onProgress?.Invoke(currentProgress / 20.0);
				}
			});

			return results.SelectMany(x => x.FlattenAndIgnore<LineString>());
		}

		public static IProcessor<LineString> DifferenceSimple(this IProcessor<LineString> container, ICollection<LineString> others,
			ILogger<Processor> logger = null)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}

			return container.Chain("DifferenceSimple",
				(geometries) => new MultiLineString(geometries.ToArray()).Difference(new MultiLineString(others.ToArray()))
					.FlattenAndIgnore<LineString>()
					.ToList());
		}

		private static IEnumerable<(IEnumerable<LineString> Sources, HashSet<LineString> Targets, IEnumerable<LineString> Ignored)> Batch(
			IEnumerable<LineString> lineStrings, ISpatialIndex<LineString> spatialIndex, int batchSize)
		{
			List<LineString> sources = new List<LineString>();
			HashSet<LineString> candidates = new HashSet<LineString>();
			List<LineString> ignored = new List<LineString>();

			int count = 0;

			foreach (LineString lineString in lineStrings)
			{
				ICollection<LineString> candidateTargets = spatialIndex.Query(lineString.EnvelopeInternal);

				if (candidateTargets.Any())
				{
					sources.Add(lineString);
					count++;

					foreach (LineString candidateTarget in candidateTargets)
					{
						candidates.Add(candidateTarget);
					}
				}
				else
				{
					ignored.Add(lineString);
				}

				if (count == batchSize)
				{
					yield return (sources, candidates, ignored);

					sources = new List<LineString>();
					candidates = new HashSet<LineString>();
					ignored = new List<LineString>();

					count = 0;
				}
			}

			yield return (sources, candidates, ignored);
		}
	}
}