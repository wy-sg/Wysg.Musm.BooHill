using System;
using System.Collections.Generic;

namespace Wysg.Musm.BooHill;

public sealed class FilterOptions
{
    public int? ClusterId { get; set; }
    public List<string>? BuildingNumbers { get; set; }
    public List<string>? UnitNumbers { get; set; }
    public List<string>? Areas { get; set; }
    public List<string>? Directions { get; set; }
    public bool ShowSold { get; set; }
    public bool FavoriteOnly { get; set; }
    public List<string>? Tags { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? MinRank { get; set; }
    public double? MaxRank { get; set; }
    public string? RemarkText { get; set; }
    public List<SortColumn> SortColumns { get; set; } = new();
}

public sealed class SortColumn
{
    public SortField Field { get; set; }
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

public enum SortField
{
    Default,
    Building,
    Unit,
    Area,
    Direction,
    Favorite,
    Office,
    PriceRange,
    Status,
    Value,
    Rank,
    Sold
}

public enum SortDirection
{
    Ascending,
    Descending
}
