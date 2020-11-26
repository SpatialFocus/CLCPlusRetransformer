// <copyright file="ChainedProcessor{TGeometryType}.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public class ChainedProcessor<TPreviousGeometryType, TGeometryType> : BaseProcessor<TGeometryType>
		where TPreviousGeometryType : Geometry where TGeometryType : Geometry
	{
		public ChainedProcessor(string processorName, string dataName, ProcessorFactory processorFactory,
			IProcessor<TPreviousGeometryType> previousProcessor,
			Func<ICollection<TPreviousGeometryType>, ICollection<TGeometryType>> processorFunction, ILogger<Processor> logger) : base(
			processorName, dataName, processorFactory, logger)
		{
			PreviousProcessor = previousProcessor;
			ProcessorFunction = processorFunction;
		}

		protected IProcessor<TPreviousGeometryType> PreviousProcessor { get; }

		protected Func<ICollection<TPreviousGeometryType>, ICollection<TGeometryType>> ProcessorFunction { get; }

		protected override ICollection<TGeometryType> ExecuteInternal()
		{
			ICollection<TPreviousGeometryType> previousGeometries = PreviousProcessor.Execute();

			Logger.LogInformation("{ProcessorName} [{DataName}] started", ProcessorName, DataName);

			Stopwatch stopwatch = Stopwatch.StartNew();
			ICollection<TGeometryType> geometries = ProcessorFunction(previousGeometries);
			stopwatch.Stop();

			Logger.LogInformation("{ProcessorName} [{DataName}] finished in {Time}ms", ProcessorName, DataName,
				stopwatch.ElapsedMilliseconds);

			return geometries;
		}
	}
}