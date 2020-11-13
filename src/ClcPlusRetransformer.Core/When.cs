// <copyright file="When.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System.Threading.Tasks;

	public class When
	{
		public static async Task<(T1, T2)> All<T1, T2>(Task<T1> task1, Task<T2> task2)
		{
			return (await task1, await task2);
		}

		public static async Task<(T1, T2, T3)> All<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3)
		{
			return (await task1, await task2, await task3);
		}
	}
}