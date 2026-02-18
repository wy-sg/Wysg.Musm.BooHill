using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Wysg.Musm.BooHill;

/// <summary>
/// Window for testing bulk import parsing without actually modifying the database.
/// </summary>
public sealed partial class BulkImportWindow : Window
{
    private TextBox? _inputBox;
    private TextBlock? _summaryText;
    private ListView? _housesList;
    private ListView? _itemsList;
    private TextBox? _logBox;
    private ListView? _duplicatesList;
    private ListView? _relatedList;
    private ListView? _similarList;
    private BulkParsedHouse? _selectedHouse;
    private HouseView? _selectedSimilar;
    private readonly ObservableCollection<HouseView> SimilarHouses = new();
    private List<HouseView> _existingHouses = new();
    private int _duplicatesImported;
    private int _mergedCount;
    private int _newInserted;
    private readonly string _today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public ObservableCollection<BulkParsedHouse> ParsedHouses { get; } = new();
    public ObservableCollection<BulkParsedItem> SelectedItems { get; } = new();
    public ObservableCollection<BulkParsedHouse> DuplicateHouses { get; } = new();
    public ObservableCollection<BulkParsedHouse> RelatedDuplicates { get; } = new();

    public BulkImportWindow()
    {
        InitializeComponent();
        WireUpControls();
    }

    private void WireUpControls()
    {
        _inputBox = FindControl<TextBox>("InputBox");
        _summaryText = FindControl<TextBlock>("SummaryText");
        _housesList = FindControl<ListView>("HousesList");
        _itemsList = FindControl<ListView>("ItemsList");
        _logBox = FindControl<TextBox>("LogBox");
        _duplicatesList = FindControl<ListView>("DuplicatesList");
        _relatedList = FindControl<ListView>("RelatedList");
        _similarList = FindControl<ListView>("SimilarList");

        if (_housesList != null) _housesList.ItemsSource = ParsedHouses;
        if (_itemsList != null) _itemsList.ItemsSource = SelectedItems;
        if (_duplicatesList != null) _duplicatesList.ItemsSource = DuplicateHouses;
        if (_relatedList != null) _relatedList.ItemsSource = RelatedDuplicates;
        if (_similarList != null) _similarList.ItemsSource = SimilarHouses;
    }

    private T? FindControl<T>(string name) where T : class
    {
        return (Content as FrameworkElement)?.FindName(name) as T;
    }

    private async Task ApplyDatabaseDuplicateCheckAsync(BulkImportResult result)
    {
        var repo = await BooHillRepository.CreateAsync().ConfigureAwait(false);

        var buildingNumbers = result.Houses
            .Select(h => h.BuildingNumber)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var areas = result.Houses
            .Select(h => h.Area)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // We use the dominant area filter if there is exactly one; otherwise no area filter.
        string? areaFilter = areas.Count == 1 ? areas[0] : null;

        _existingHouses = (await repo.GetHousesWithItemsAsync(buildingNumbers, areaFilter).ConfigureAwait(false)).ToList();

        foreach (var parsed in result.Houses)
        {
            var matchingExisting = _existingHouses.Where(e => HouseIdentityMatches(parsed, e)).ToList();

            var shared = matchingExisting.FirstOrDefault(e => HasSharedItem(parsed.Items, e.Items));
            if (shared != null)
            {
                parsed.IsDuplicate = true;
                parsed.DuplicateReason = "Matches existing DB house with shared item";
                parsed.MatchedHouseId = shared.HouseId;
                DuplicateHouses.Add(parsed);
                result.Logs.Add($"DB duplicate: {parsed.Display} matches house_id={shared.HouseId}");
            }
            else
            {
                ParsedHouses.Add(parsed);
            }
        }
    }

    private static bool HouseIdentityMatches(BulkParsedHouse parsed, HouseView existing)
    {
        var unitParsed = NormalizeUnit(parsed.UnitNumber);
        var unitExisting = NormalizeUnit(existing.UnitNumber);

        return string.Equals(parsed.BuildingNumber?.Trim(), existing.BuildingNumber?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(parsed.Area?.Trim(), existing.Area?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(unitParsed, unitExisting, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSharedItem(IEnumerable<BulkParsedItem> parsedItems, IEnumerable<ItemRecord> existingItems)
    {
        var existingKeys = new HashSet<string>(existingItems.Select(ItemKey), StringComparer.OrdinalIgnoreCase);
        return parsedItems.Any(pi => existingKeys.Contains(ItemKey(pi)));
    }

    private static string NormalizeUnit(string? unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? string.Empty : unit.Trim();
    }

    private static string ItemKey(BulkParsedItem item)
    {
        var price = item.Price?.ToString("G17", CultureInfo.InvariantCulture) ?? "<null>";
        var office = item.Office?.Trim() ?? "<null>";
        var remark = item.Remark?.Trim() ?? "<null>";
        return $"{price}|{office}|{remark}";
    }

    private static string ItemKey(ItemRecord item)
    {
        var price = item.Price?.ToString("G17", CultureInfo.InvariantCulture) ?? "<null>";
        var office = item.Office?.Trim() ?? "<null>";
        var remark = item.Remark?.Trim() ?? "<null>";
        return $"{price}|{office}|{remark}";
    }

    private async void ImportDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (DuplicateHouses.Count == 0)
        {
            return;
        }

        var repo = await BooHillRepository.CreateAsync().ConfigureAwait(false);
        var added = 0;
        foreach (var dup in DuplicateHouses)
        {
            if (dup.MatchedHouseId == null)
            {
                continue;
            }

            added += await repo.AddItemsAsync(dup.MatchedHouseId.Value, dup.Items, _today).ConfigureAwait(false);
        }

        _duplicatesImported += added;
        DispatcherQueue?.TryEnqueue(UpdateSummaryText);
    }

    private async void MergeSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedHouse == null || _selectedSimilar == null)
        {
            return;
        }

        var repo = await BooHillRepository.CreateAsync().ConfigureAwait(false);
        var added = await repo.AddItemsAsync(_selectedSimilar.HouseId, _selectedHouse.Items, _today).ConfigureAwait(false);
        _mergedCount += added > 0 ? 1 : 0;

        ParsedHouses.Remove(_selectedHouse);
        SimilarHouses.Clear();
        _selectedSimilar = null;
        _selectedHouse = null;
        SelectedItems.Clear();

        DispatcherQueue?.TryEnqueue(UpdateSummaryText);
    }

    private async void FinalizeNew_Click(object sender, RoutedEventArgs e)
    {
        if (ParsedHouses.Count == 0)
        {
            return;
        }

        var repo = await BooHillRepository.CreateAsync().ConfigureAwait(false);
        var insertedHouses = 0;
        var insertedItems = 0;

        foreach (var house in ParsedHouses.ToList())
        {
            var clusterId = await repo.GetOrCreateClusterIdAsync(house.ClusterName).ConfigureAwait(false);
            var newId = await repo.InsertHouseWithItemsAsync(house, _today, clusterId).ConfigureAwait(false);
            insertedHouses++;
            insertedItems += house.Items.Count;
        }

        ParsedHouses.Clear();
        SelectedItems.Clear();
        _newInserted += insertedHouses;
        DispatcherQueue?.TryEnqueue(UpdateSummaryText);
    }

    private void UpdateSummaryText()
    {
        if (_summaryText == null)
        {
            return;
        }

        var addableHouses = ParsedHouses.Count;
        var addableItems = ParsedHouses.Sum(h => h.Items.Count);
        _summaryText.Text = $"Remaining: {addableHouses} houses / {addableItems} items | Imported dup items: {_duplicatesImported}, Merged: {_mergedCount}, New houses: {_newInserted}";
    }

    private async void ParseButton_Click(object sender, RoutedEventArgs e)
    {
        var rawText = _inputBox?.Text ?? string.Empty;
        var result = BulkImportParser.Parse(rawText);

        ParsedHouses.Clear();
        SelectedItems.Clear();
        DuplicateHouses.Clear();
        RelatedDuplicates.Clear();
        SimilarHouses.Clear();
        _existingHouses.Clear();
        _duplicatesImported = 0;
        _mergedCount = 0;
        _newInserted = 0;
        await ApplyDatabaseDuplicateCheckAsync(result);

        if (_summaryText != null)
        {
            var addableHouses = ParsedHouses.Count;
            var addableItems = ParsedHouses.Sum(h => h.Items.Count);
            _summaryText.Text = $"Would add: {addableHouses} houses, {addableItems} items (duplicates: {DuplicateHouses.Count})";
        }

        if (_logBox != null)
        {
            _logBox.Text = string.Join(Environment.NewLine, result.Logs);
        }

        // Select first house if any
        if (ParsedHouses.Count > 0 && _housesList != null)
        {
            _housesList.SelectedIndex = 0;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_inputBox != null)
        {
            _inputBox.Text = string.Empty;
        }

        ParsedHouses.Clear();
        SelectedItems.Clear();
        DuplicateHouses.Clear();
        RelatedDuplicates.Clear();
        SimilarHouses.Clear();
        _existingHouses.Clear();
        _duplicatesImported = 0;
        _mergedCount = 0;
        _newInserted = 0;

        if (_summaryText != null)
        {
            _summaryText.Text = "Paste text below and click 'Parse & Simulate'";
        }

        if (_logBox != null)
        {
            _logBox.Text = string.Empty;
        }
    }

    private void HousesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItems.Clear();

        if (_housesList?.SelectedItem is BulkParsedHouse house)
        {
            _selectedHouse = house;
            foreach (var item in house.Items)
            {
                SelectedItems.Add(item);
            }

            RelatedDuplicates.Clear();
            foreach (var dup in DuplicateHouses.Where(d => d.Key == house.Key))
            {
                RelatedDuplicates.Add(dup);
            }

            SimilarHouses.Clear();
            _selectedSimilar = null;
            foreach (var similar in _existingHouses.Where(e => HouseIdentityMatches(house, e)))
            {
                SimilarHouses.Add(similar);
            }
        }
        else
        {
            _selectedHouse = null;
            RelatedDuplicates.Clear();
            SimilarHouses.Clear();
            _selectedSimilar = null;
        }
    }

    private void SimilarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_similarList?.SelectedItem is HouseView hv)
        {
            _selectedSimilar = hv;
        }
        else
        {
            _selectedSimilar = null;
        }
    }
}
