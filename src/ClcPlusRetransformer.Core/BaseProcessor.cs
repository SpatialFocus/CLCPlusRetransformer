// <copyright file="BaseProcessor.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Microsoft.Extensions.Logging;
	using NetTopologySuite.Geometries;

	public abstract class BaseProcessor<TGeometryType> : IProcessor<TGeometryType> where TGeometryType : Geometry
	{
		private ICollection<TGeometryType> geometries;

		public BaseProcessor(string processorName, string dataName, ProcessorFactory processorFactory, ILogger<Processor> logger)
		{
			ProcessorName = processorName;
			DataName = dataName;
			ProcessorFactory = processorFactory;
			Logger = logger;
		}

		public string DataName { get; }

		public ProcessorFactory ProcessorFactory { get; }

		public string ProcessorName { get; }

		protected ILogger<Processor> Logger { get; }

		protected SemaphoreSlim SemaphoreSlim { get; } = new SemaphoreSlim(1);

		public ChainedProcessor<TGeometryType, TGeometryType> Chain(string processorName,
			Func<ICollection<TGeometryType>, ICollection<TGeometryType>> processorFunc)
		{
			return ProcessorFactory.CreateChainedProcessor(processorName, DataName, this, processorFunc);
		}

		public ChainedProcessor<TGeometryType, TNewGeometryType> Chain<TNewGeometryType>(string processorName,
			Func<ICollection<TGeometryType>, ICollection<TNewGeometryType>> processorFunc) where TNewGeometryType : Geometry
		{
			return ProcessorFactory.CreateChainedProcessor(processorName, DataName, this, processorFunc);
		}

		public virtual ICollection<TGeometryType> Execute()
		{
			try
			{
				SemaphoreSlim.Wait();

				if (this.geometries != null)
				{
					return this.geometries;
				}

				this.geometries = ExecuteInternal();
			}
			finally
			{
				SemaphoreSlim.Release();
			}

			return this.geometries;
		}

		protected abstract ICollection<TGeometryType> ExecuteInternal();
	}
}