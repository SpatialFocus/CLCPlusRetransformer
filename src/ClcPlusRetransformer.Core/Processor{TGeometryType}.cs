// <copyright file="Processor{TGeometryType}.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public class Processor<TGeometryType> : BaseProcessor<TGeometryType> where TGeometryType : Geometry
	{
		public Processor(string processorName, string dataName, ProcessorFactory processorFactory,
			Func<ICollection<TGeometryType>> processorFunction, ILogger<Processor> logger) : base(processorName, dataName, processorFactory,
			logger)
		{
			ProcessorFunction = processorFunction;
		}

		protected Func<ICollection<TGeometryType>> ProcessorFunction { get; }

		protected override ICollection<TGeometryType> ExecuteInternal()
		{
			Logger.LogInformation("{ProcessorName} [{DataName}] started", ProcessorName, DataName);

			Stopwatch stopwatch = Stopwatch.StartNew();
			ICollection<TGeometryType> geometries = ProcessorFunction();
			stopwatch.Stop();

			Logger.LogInformation("{ProcessorName} [{DataName}] finished in {Time}ms", ProcessorName, DataName,
				stopwatch.ElapsedMilliseconds);

			return geometries;
		}
	}
}