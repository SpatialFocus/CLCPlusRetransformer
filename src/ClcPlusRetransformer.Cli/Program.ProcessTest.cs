// <copyright file="Program.ProcessTilesAsync.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Cli.Entities;
	using ClcPlusRetransformer.Core;
	using ClcPlusRetransformer.Core.Processors;
	using ClcPlusRetransformer.Core.Processors.Extension;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public partial class Program
	{
		public static async Task ProcessTestAsync(IServiceProvider provider, IConfigurationRoot configuration,
			ILogger<Program> logger, CancellationToken cancellationToken = default)
		{
			logger.LogInformation("Running test...");

			await Program.TestAsync(provider.CreateScope().ServiceProvider);
		}

		private static async Task TestAsync(IServiceProvider provider)
		{
			IProcessor<LineString> original = provider.LoadFromFile<LineString>(@"C:\temp\geo\3_edges_B.shp", new PrecisionModel(10));

			var dissolved = original.Union(provider.GetRequiredService<ILogger<Processor>>()).Dissolve();

			dissolved.Execute().Save(@"C:\temp\geo\dissolved_B.shp", new PrecisionModel(10000));
		}
	}
}