// <copyright file="ProcessorFactory.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors
{
	using System;
	using System.Collections.Generic;
	using Microsoft.Extensions.DependencyInjection;
	using NetTopologySuite.Geometries;

	public class ProcessorFactory
	{
		public ProcessorFactory(IServiceProvider serviceProvider)
		{
			ServiceProvider = serviceProvider;
		}

		protected IServiceProvider ServiceProvider { get; }

		public ChainedProcessor<TPreviousGeometryType, TGeometryType> CreateChainedProcessor<TPreviousGeometryType, TGeometryType>(
			string processorName, string dataName, IProcessor<TPreviousGeometryType> previousProcessor,
			Func<ICollection<TPreviousGeometryType>, ICollection<TGeometryType>> processorFunc)
			where TPreviousGeometryType : Geometry where TGeometryType : Geometry
		{
			return ActivatorUtilities.CreateInstance<ChainedProcessor<TPreviousGeometryType, TGeometryType>>(ServiceProvider, processorName,
				dataName, previousProcessor, processorFunc, this);
		}

		public Processor<TGeometryType> CreateProcessor<TGeometryType>(string processorName, string dataName,
			Func<ICollection<TGeometryType>> processorFunc) where TGeometryType : Geometry
		{
			return ActivatorUtilities.CreateInstance<Processor<TGeometryType>>(ServiceProvider, processorName, dataName, processorFunc,
				this);
		}
	}
}