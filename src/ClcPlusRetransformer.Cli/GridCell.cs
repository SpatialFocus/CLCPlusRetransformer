// <copyright file="GridCell.cs" company="Spatial Focus GmbH">
// Copyright (c) Spatial Focus GmbH. All rights reserved.
// </copyright>

namespace ClcPlusRetransformer.Cli
{
	using System;

	public class GridCell
	{
		public GridCell(string code)
		{
			GridCellSize size;

			int indexOfE = code.IndexOf("E");
			int indexOfN = code.IndexOf("N");

			string prefix = code[..indexOfE];

			if (prefix.EndsWith("km"))
			{
				size = (GridCellSize)Enum.Parse(typeof(GridCellSize), $"Km{prefix[..^2]}");
			}
			else if (prefix.EndsWith("m"))
			{
				size = (GridCellSize)Enum.Parse(typeof(GridCellSize), $"M{prefix[..^2]}");
			}
			else
			{
				throw new ArgumentException($"{nameof(code)}");
			}

			CellSizeInMeter = size switch
			{
				GridCellSize.Km100 => (int)size,
				GridCellSize.Km50 => (int)size,
				GridCellSize.Km25 => (int)size,
				GridCellSize.Km10 => (int)size,
				GridCellSize.Km => (int)size,
				GridCellSize.M250 => (int)size,
				GridCellSize.M25 => (int)size,

				_ => throw new ArgumentException($"{nameof(size)}"),
			};

			NumberOfZeroes = CalculateNumberOfZeroes();

			EastOfOrigin = int.Parse(code[(indexOfE + 1)..indexOfN]) * (int)Math.Pow(10, NumberOfZeroes);
			NorthOfOrigin = int.Parse(code[(indexOfN + 1)..]) * (int)Math.Pow(10, NumberOfZeroes);
		}

		public GridCell(GridCellSize size, int eastOfOrigin, int northOfOrigin)
		{
			CellSizeInMeter = size switch
			{
				GridCellSize.Km100 => (int)size,
				GridCellSize.Km50 => (int)size,
				GridCellSize.Km25 => (int)size,
				GridCellSize.Km10 => (int)size,
				GridCellSize.Km => (int)size,
				GridCellSize.M250 => (int)size,
				GridCellSize.M25 => (int)size,

				_ => throw new ArgumentException($"{nameof(size)}"),
			};

			EastOfOrigin = eastOfOrigin;
			NorthOfOrigin = northOfOrigin;

			NumberOfZeroes = CalculateNumberOfZeroes();

			if (eastOfOrigin % Math.Pow(10, NumberOfZeroes) != 0)
			{
				throw new ArgumentException($"{nameof(eastOfOrigin)}");
			}

			if (northOfOrigin % Math.Pow(10, NumberOfZeroes) != 0)
			{
				throw new ArgumentException($"{nameof(northOfOrigin)}");
			}
		}

		public int CellSizeInMeter { get; }

		public string Code => $"{CodePrefix}E{EastOfOrigin / Math.Pow(10, NumberOfZeroes)}N{NorthOfOrigin / Math.Pow(10, NumberOfZeroes)}";

		public int EastOfOrigin { get; }

		public int NorthOfOrigin { get; }

		private string CodePrefix => CellSizeInMeter >= 1000 ? $"{CellSizeInMeter / 1000}km" : $"{CellSizeInMeter}m";

		private int NumberOfZeroes { get; }

		private int CalculateNumberOfZeroes()
		{
			int number = CellSizeInMeter;
			int count = 0;

			while (number > 0 && number % 10 == 0)
			{
				number = number / 10;
				count++;
			}

			return count;
		}
	}
}