// <copyright file="DynamicModelCacheKeyFactory.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace Geopackage
{
	using Microsoft.EntityFrameworkCore;
	using Microsoft.EntityFrameworkCore.Infrastructure;

	// Source: https://docs.microsoft.com/en-us/ef/core/modeling/dynamic-model
	public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
	{
		public object Create(DbContext context) =>
			context is GeopackageWriteContext writeContext ? (context.GetType(), writeContext.LayerName) : (object)context.GetType();
	}
}