// <copyright file="EnvelopeExtension.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Core.Processors
{
	using System.Collections.Generic;
	using NetTopologySuite.Geometries;

	public static class EnvelopeExtension
	{
		public static IEnumerable<Envelope> Split(this Envelope envelope, int numberOfSplits)
		{
			for (int i = 0; i < numberOfSplits; i++)
			{
				for (int j = 0; j < numberOfSplits; j++)
				{
					yield return new Envelope(envelope.MinX + (envelope.Width * (1.0 / numberOfSplits) * i),
						envelope.MinX + (envelope.Width * (1.0 / numberOfSplits) * (i + 1)),
						envelope.MinY + (envelope.Height * (1.0 / numberOfSplits) * j),
						envelope.MinY + (envelope.Height * (1.0 / numberOfSplits) * (j + 1)));
				}
			}
		}

		public static IEnumerable<Envelope> Split(this Envelope envelope, int numberOfSplitsX, int numberOfSplitsY)
		{
			for (int i = 0; i < numberOfSplitsX; i++)
			{
				for (int j = 0; j < numberOfSplitsY; j++)
				{
					yield return new Envelope(envelope.MinX + (envelope.Width * (1.0 / numberOfSplitsX) * i),
						envelope.MinX + (envelope.Width * (1.0 / numberOfSplitsX) * (i + 1)),
						envelope.MinY + (envelope.Height * (1.0 / numberOfSplitsY) * j),
						envelope.MinY + (envelope.Height * (1.0 / numberOfSplitsY) * (j + 1)));
				}
			}
		}

		public static IEnumerable<Envelope> SplitBorders(this Envelope envelope, int numberOfSplits)
		{
			for (int i = 0; i < numberOfSplits - 1; i++)
			{
				yield return new Envelope((envelope.MinX + (envelope.Width * (1.0 / numberOfSplits) * (i + 1))) - 1,
					envelope.MinX + (envelope.Width * (1.0 / numberOfSplits) * (i + 1)) + 1, envelope.MinY, envelope.MaxY);

				yield return new Envelope(envelope.MinX, envelope.MaxX,
					(envelope.MinY + (envelope.Height * (1.0 / numberOfSplits) * (i + 1))) - 1,
					envelope.MinY + (envelope.Height * (1.0 / numberOfSplits) * (i + 1)) + 1);
			}
		}
	}
}