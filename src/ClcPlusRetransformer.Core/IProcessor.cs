// <copyright file="IProcessor.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core
{
	using System;
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public interface IProcessor<TGeometryType> where TGeometryType : Geometry
	{
		ChainedProcessor<TGeometryType, TGeometryType> Chain(string processorName,
			Func<ICollection<TGeometryType>, ICollection<TGeometryType>> processorFunc);

		ChainedProcessor<TGeometryType, TNewGeometryType> Chain<TNewGeometryType>(string processorName,
			Func<ICollection<TGeometryType>, ICollection<TNewGeometryType>> processorFunc) where TNewGeometryType : Geometry;

		public ICollection<TGeometryType> Execute();
	}
}