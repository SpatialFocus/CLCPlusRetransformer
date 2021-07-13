// <copyright file="EnumerableExtensions.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	public static class EnumerableExtensions
	{
		public static Task ForEachAsync<T>(this IEnumerable<T> source, int degreeOfParallelism, Func<T, CancellationToken, Task> body,
			CancellationToken cancellationToken = default)
		{
			return Task.WhenAll(Partitioner.Create(source)
				.GetPartitions(degreeOfParallelism)
				.Select(partition => Task.Run(async () =>
				{
					using (partition)
					{
						while (partition.MoveNext())
						{
							await body(partition.Current, cancellationToken);
						}
					}
				}, cancellationToken)));
		}

		public static async Task<IEnumerable<TResult>> ForEachAsync<T, TResult>(this IEnumerable<T> source, int degreeOfParallelism,
			Func<T, CancellationToken, Task<TResult>> body, CancellationToken cancellationToken = default)
		{
			ConcurrentBag<TResult> concurrentBag = new();

			await Task.WhenAll(Partitioner.Create(source)
				.GetPartitions(degreeOfParallelism)
				.Select(partition => Task.Run(async () =>
				{
					using (partition)
					{
						while (partition.MoveNext())
						{
							concurrentBag.Add(await body(partition.Current, cancellationToken));
						}
					}
				}, cancellationToken)));

			return concurrentBag;
		}
	}
}