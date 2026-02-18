using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Wysg.Musm.BooHill;

public sealed class HouseEdit
{
    public long HouseId { get; set; }
    public int ClusterId { get; set; }
    public string BuildingNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public bool IsSold { get; set; }
    public bool IsFavorite { get; set; }
    public double? Value { get; set; }
    public double? ValueEstimate { get; set; }
    public double? Rank { get; set; }
    public double? RankEstimate { get; set; }
    public string Tags { get; set; } = string.Empty;
}

public sealed class ItemRecord
{
    public long ItemId { get; set; }
    public long HouseId { get; set; }
    public double? Price { get; set; }
    public string? Office { get; set; }
    public string? LastUpdatedDate { get; set; }
    public string? AddedDate { get; set; }
    public string? Remark { get; set; }

    public string PriceDisplay => Price?.ToString("N0", CultureInfo.InvariantCulture) ?? string.Empty;
}

public sealed class MassImportResult
{
    public int HousesInserted { get; set; }
    public int ItemsInserted { get; set; }
}

public sealed class ClusterRecord
{
    public int ClusterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Display => $"{ClusterId} - {Name}";
}

public sealed class ItemDisplayGroup
{
    public ItemDisplayGroup(string office, IReadOnlyList<ItemRecord> items)
    {
        Office = office;
        Items = items;

        var today = DateTime.Today;

        bool IsSameDay(string? value, DateTime day)
        {
            var parsed = ParseDateLocal(value);
            return parsed != DateTime.MinValue && parsed.Date == day.Date;
        }

        bool IsSameDate(string? left, string? right)
        {
            var l = ParseDateLocal(left);
            var r = ParseDateLocal(right);
            return l != DateTime.MinValue && r != DateTime.MinValue && l.Date == r.Date;
        }

        HasSameDayFresh = Items.Any(i => IsSameDay(i.AddedDate, today) && IsSameDate(i.AddedDate, i.LastUpdatedDate));
        HasTodayAdded = Items.Any(i => IsSameDay(i.AddedDate, today));
        StatusColor = HasSameDayFresh ? "LightGreen" : (!HasTodayAdded ? "LightCoral" : "Transparent");
    }

    public string Office { get; }
    public IReadOnlyList<ItemRecord> Items { get; }

    public bool HasSameDayFresh { get; }
    public bool HasTodayAdded { get; }
    public string StatusColor { get; }
    public bool IsGreen => StatusColor == "LightGreen";
    public bool IsRed => StatusColor == "LightCoral";

    private static DateTime ParseDateLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }
}

public sealed class HouseView
    : INotifyPropertyChanged
{
    public HouseView()
    {
        Items.CollectionChanged += (s, e) => OnItemsChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public long HouseId { get; set; }
    public string BuildingNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public bool IsSold { get; set; }
    public double? Value { get; set; }
    public double? ValueEstimate { get; set; }
    public double? Rank { get; set; }
    public double? RankEstimate { get; set; }
    public int ClusterId { get; set; }
    public double? MinPrice { get; set; }
    public double? MaxPrice { get; set; }
    public long OfficeCount { get; set; }
    public long ItemTotal { get; set; }
    public long ItemTodayMatch { get; set; }
    public bool IsNewToday { get; set; }
    public string Tags { get; set; } = string.Empty;

    public ObservableCollection<ItemRecord> Items { get; } = new();

    public IEnumerable<ItemRecord> ItemsForDisplay => Items
        .GroupBy(i => new
        {
            Price = i.Price,
            Office = Normalize(i.Office),
            Updated = Normalize(i.LastUpdatedDate),
            Remark = Normalize(i.Remark)
        })
        .Select(g =>
        {
            var latest = g.OrderByDescending(x => ParseDate(x.AddedDate)).First();
            return new ItemRecord
            {
                ItemId = latest.ItemId,
                HouseId = latest.HouseId,
                Price = g.Key.Price,
                Office = g.Key.Office,
                LastUpdatedDate = g.Key.Updated,
                AddedDate = latest.AddedDate,
                Remark = g.Key.Remark
            };
        })
        .OrderBy(i => Normalize(i.Office))
        .ThenByDescending(i => ParseDate(i.AddedDate))
        .ThenByDescending(i => ParseDate(i.LastUpdatedDate));

    public IEnumerable<ItemDisplayGroup> ItemsForDisplayGroups => ItemsForDisplay
        .GroupBy(i => Normalize(i.Office))
        .Select(g => new ItemDisplayGroup(g.Key, g.ToList()))
        .OrderBy(g => g.Office);

    public bool AllGroupsGreen => ItemsForDisplayGroups.Any() && ItemsForDisplayGroups.All(g => g.IsGreen);
    public bool AllGroupsRed => ItemsForDisplayGroups.Any() && ItemsForDisplayGroups.All(g => g.IsRed);
    public bool ShowStatusCircle => AllGroupsGreen || AllGroupsRed;
    public string StatusCircleColor => AllGroupsGreen ? "LightGreen" : AllGroupsRed ? "LightCoral" : "Transparent";

    public string StatusText => AllItemsRecent ? "new" : string.Empty;
    public bool HasStatusText => !string.IsNullOrEmpty(StatusText);
    public int OfficeGroupCount => ItemsForDisplayGroups.Count();

    private bool AllItemsRecent => Items.Count > 0 && Items.All(i => IsTodayOrYesterday(i.AddedDate) && IsTodayOrYesterday(i.LastUpdatedDate));

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public string SoldGlyph => IsSold ? "▣" : "□";
    public string PriceRange => FormatRange(MinPrice, MaxPrice);
    public string ValueDisplay => FormatValue(Value, ValueEstimate);
    public string RankDisplay => FormatValue(Rank, RankEstimate);

    public override string ToString()
    {
        var status = StatusText;
        var price = FormatRange(MinPrice, MaxPrice);
        var unit = string.IsNullOrWhiteSpace(UnitNumber) ? "-" : UnitNumber;
        var itemDetails = Items.Take(3)
            .Select(i =>
            {
                var p = i.Price?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";
                var o = string.IsNullOrWhiteSpace(i.Office) ? "-" : i.Office;
                var r = string.IsNullOrWhiteSpace(i.Remark) ? "-" : i.Remark;
                var d = string.IsNullOrWhiteSpace(i.LastUpdatedDate) ? "-" : i.LastUpdatedDate;
                return $"{p}/{o}/{d}/{r}";
            });

        var parts = new[]
        {
            $"{BuildingNumber}동",
            $"unit {unit}",
            $"{Area}평",
            string.IsNullOrWhiteSpace(price) ? null : $"price {price}",
            status,
            Items.Count > 0 ? $"items({Items.Count}): {string.Join("; ", itemDetails)}" : null
        };

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void OnItemsChanged()
    {
        OnPropertyChanged(nameof(ItemsForDisplay));
        OnPropertyChanged(nameof(ItemsForDisplayGroups));
        OnPropertyChanged(nameof(AllGroupsGreen));
        OnPropertyChanged(nameof(AllGroupsRed));
        OnPropertyChanged(nameof(ShowStatusCircle));
        OnPropertyChanged(nameof(StatusCircleColor));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(OfficeGroupCount));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatRange(double? min, double? max)
    {
        var left = min.HasValue ? min.Value.ToString("N0", CultureInfo.InvariantCulture) : "";
        var right = max.HasValue ? max.Value.ToString("N0", CultureInfo.InvariantCulture) : "";
        if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(left))
        {
            return $"- {right}";
        }

        if (string.IsNullOrEmpty(right))
        {
            return $"{left} -";
        }

        return $"{left} - {right}";
    }

    private static string FormatValue(double? value, double? estimate)
    {
        if (value.HasValue)
        {
            return value.Value.ToString("N0", CultureInfo.InvariantCulture);
        }

        if (estimate.HasValue)
        {
            return estimate.Value.ToString("N0", CultureInfo.InvariantCulture) + "?";
        }

        return string.Empty;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static DateTime ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }

    private static bool IsTodayOrYesterday(string? value)
    {
        var dt = ParseDate(value);
        if (dt == DateTime.MinValue)
        {
            return false;
        }

        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        return dt.Date == today.Date || dt.Date == yesterday.Date;
    }
}
