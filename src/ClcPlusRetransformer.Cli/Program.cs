﻿// <copyright file="Program.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using ClcPlusRetransformer.Cli.Entities;
	using ClcPlusRetransformer.Core.Processors;
	using Microsoft.EntityFrameworkCore;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;
	using Serilog;

	public partial class Program
	{
		public static async Task Main()
		{
			IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

			IConfigurationRoot config = builder.Build();

			ServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(loggingBuilder =>
				loggingBuilder.AddSerilog(new LoggerConfiguration().ReadFrom.Configuration(config.GetSection("Logging")).CreateLogger()));

			serviceCollection.AddTransient(typeof(Processor<>));
			serviceCollection.AddTransient(typeof(ChainedProcessor<,>));
			serviceCollection.AddTransient<ProcessorFactory>();

			serviceCollection.AddDbContext<SpatialContext>(options =>
			{
				options.UseSqlite(config["SqliteConnectionString"], o => o.UseNetTopologySuite().CommandTimeout(60 * 5))
					.UseLazyLoadingProxies();
			});

			ServiceProvider provider = serviceCollection.BuildServiceProvider();

			ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

			logger.LogInformation("Workflow started");
			Stopwatch stopwatch = Stopwatch.StartNew();

			PrecisionModel precisionModel = new PrecisionModel(10000);

			if (int.Parse(config["PartitionCount"]) > 1)
			{
				await Program.ImportShapefilesToSqliteAsync(provider, config, precisionModel, logger);
				await Program.ProcessTilesAsync(provider, config, precisionModel, logger);
				await Program.MergeTilesAsync(provider, config, logger);
				await Program.MergeToResultAsync(provider, config, logger);
			}
			else
			{
				await Program.ProcessShapesAsync(provider, config, precisionModel, logger);
			}

			stopwatch.Stop();
			logger.LogInformation("Workflow finished in {Time}ms", stopwatch.ElapsedMilliseconds);

			if (bool.Parse(config["WaitForUserInputAfterCompletion"]))
			{
				Console.WriteLine("=== PRESS A KEY TO PROCEED ===");
				Console.ReadKey();
			}
		}
	}
}