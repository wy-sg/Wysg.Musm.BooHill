using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Wysg.Musm.BooHill
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private BooHillRepository? _repository;
        private FilterOptions _currentFilters = new();

        private Grid? _rootGrid;
        private TextBlock? _statusText;
        private ListView? _houseList;
        private ComboBox? _clusterCombo;
        private Button? _buildingFilterButton;
        private ListView? _buildingFilterList;
        private Button? _unitFilterButton;
        private ListView? _unitFilterList;
        private Button? _areaFilterButton;
        private ListView? _areaFilterList;
        private Button? _tagFilterButton;
        private ListView? _tagFilterList;
        private CheckBox? _favoriteOnlyCheck;
        private CheckBox? _showSoldCheck;
        private TextBox? _minValueBox;
        private TextBox? _maxValueBox;
        private TextBox? _minRankBox;
        private TextBox? _maxRankBox;

        public ObservableCollection<HouseView> Houses { get; } = new();
        public ObservableCollection<ClusterRecord> Clusters { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WireUpControls();
                _repository = await BooHillRepository.CreateAsync();
                await LoadClustersAsync();
                await LoadFilterOptionsAsync();
                await LoadHousesAsync(_currentFilters);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to initialize", ex.Message);
            }
        }

        private async Task LoadClustersAsync()
        {
            if (_repository == null)
            {
                return;
            }

            Clusters.Clear();
            var clusters = await _repository.GetClustersAsync();
            foreach (var cluster in clusters)
            {
                Clusters.Add(cluster);
            }
        }

        private async Task LoadHousesAsync(FilterOptions filters)
        {
            if (_repository == null)
            {
                return;
            }

            if (_statusText != null)
            {
                _statusText.Text = "Loading...";
            }

            if (_houseList != null)
            {
                _houseList.IsEnabled = false;
            }

            try
            {
                var houses = await _repository.GetHousesAsync(filters);
                Houses.Clear();
                foreach (var house in houses)
                {
                    Houses.Add(house);
                }

                // Preload items so status/office group counts are available even when rows are collapsed.
                await PopulateHouseItemsAsync(Houses);

                if (_statusText != null)
                {
                    _statusText.Text = Houses.Count == 0 ? "No listings found." : $"Showing {Houses.Count} listings.";
                }
            }
            catch (Exception ex)
            {
                if (_statusText != null)
                {
                    _statusText.Text = "Failed to load listings.";
                }
                await ShowErrorAsync("Load failed", ex.Message);
            }
            finally
            {
                if (_houseList != null)
                {
                    _houseList.IsEnabled = true;
                }
            }
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "Close",
                XamlRoot = (Content as FrameworkElement)?.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            _currentFilters = ReadFiltersFromUi();
            await LoadHousesAsync(_currentFilters);
        }

        private async void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            if (_clusterCombo != null)
            {
                _clusterCombo.SelectedItem = null;
            }

            ClearMultiSelect(_buildingFilterList, _buildingFilterButton);
            ClearMultiSelect(_unitFilterList, _unitFilterButton);
            ClearMultiSelect(_areaFilterList, _areaFilterButton);
            ClearMultiSelect(_tagFilterList, _tagFilterButton);
            if (_favoriteOnlyCheck != null) _favoriteOnlyCheck.IsChecked = false;
            if (_showSoldCheck != null) _showSoldCheck.IsChecked = false;
            if (_minValueBox != null) _minValueBox.Text = string.Empty;
            if (_maxValueBox != null) _maxValueBox.Text = string.Empty;
            if (_minRankBox != null) _minRankBox.Text = string.Empty;
            if (_maxRankBox != null) _maxRankBox.Text = string.Empty;

            _currentFilters = new FilterOptions();
            UpdateSortButtonLabels();
            await LoadClustersAsync();
            await LoadFilterOptionsAsync();
            await LoadHousesAsync(_currentFilters);
        }

        private void OpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            var window = new AdminWindow();
            window.Activate();
        }

        private void OpenBulkImport_Click(object sender, RoutedEventArgs e)
        {
            var window = new BulkImportWindow();
            window.Activate();
        }

        private void OpenInfo_Click(object sender, RoutedEventArgs e)
        {
            var window = new InfoWindow();
            window.Activate();
        }

        private FilterOptions ReadFiltersFromUi()
        {
            int? selectedClusterId = null;
            if (_clusterCombo?.SelectedValue is int selectedValue)
            {
                selectedClusterId = selectedValue;
            }
            else if (_clusterCombo?.SelectedItem is ClusterRecord record)
            {
                selectedClusterId = record.ClusterId;
            }

            return new FilterOptions
            {
                ClusterId = selectedClusterId,
                BuildingNumbers = GetSelectedStrings(_buildingFilterList),
                UnitNumbers = GetSelectedStrings(_unitFilterList),
                Areas = GetSelectedStrings(_areaFilterList),
                Tags = GetSelectedStrings(_tagFilterList),
                FavoriteOnly = _favoriteOnlyCheck?.IsChecked == true,
                ShowSold = _showSoldCheck?.IsChecked == true,
                MinValue = TryParseDouble(NormalizeInput(_minValueBox?.Text)),
                MaxValue = TryParseDouble(NormalizeInput(_maxValueBox?.Text)),
                MinRank = TryParseDouble(NormalizeInput(_minRankBox?.Text)),
                MaxRank = TryParseDouble(NormalizeInput(_maxRankBox?.Text)),
                SortColumns = new List<SortColumn>(_currentFilters.SortColumns)
            };
        }

        private static List<string>? GetSelectedStrings(ListView? listView)
        {
            if (listView == null || listView.SelectedItems.Count == 0)
            {
                return null;
            }

            return listView.SelectedItems.OfType<string>().ToList();
        }

        private static string NormalizeInput(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static double? TryParseDouble(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return double.TryParse(text, out var value) ? value : null;
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null)
            {
                return;
            }

            if (sender is Button button && button.Tag is long houseId)
            {
                try
                {
                    await _repository.ToggleFavoriteAsync(houseId);
                    await LoadHousesAsync(_currentFilters);
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync("Failed to toggle favorite", ex.Message);
                }
            }
        }

        private async void Sold_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null)
            {
                return;
            }

            if (sender is Button button && button.Tag is long houseId)
            {
                try
                {
                    await _repository.ToggleSoldAsync(houseId);
                    await LoadHousesAsync(_currentFilters);
                }
                catch (Exception ex)
                {
                    await ShowErrorAsync("Failed to toggle sold", ex.Message);
                }
            }
        }

        private async void SortColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string tagStr)
            {
                return;
            }

            if (!Enum.TryParse<SortField>(tagStr, out var field))
            {
                return;
            }

            var columns = _currentFilters.SortColumns;
            var existing = columns.FindIndex(c => c.Field == field);
            if (existing >= 0)
            {
                var col = columns[existing];
                if (col.Direction == SortDirection.Ascending)
                {
                    col.Direction = SortDirection.Descending;
                }
                else
                {
                    columns.RemoveAt(existing);
                }
            }
            else
            {
                columns.Add(new SortColumn { Field = field, Direction = SortDirection.Ascending });
            }

            UpdateSortButtonLabels();
            _currentFilters = ReadFiltersFromUi();
            await LoadHousesAsync(_currentFilters);
        }

        private async void ClearSort_Click(object sender, RoutedEventArgs e)
        {
            _currentFilters.SortColumns.Clear();
            UpdateSortButtonLabels();
            _currentFilters = ReadFiltersFromUi();
            await LoadHousesAsync(_currentFilters);
        }

        private static readonly Dictionary<SortField, string> SortFieldLabels = new()
        {
            { SortField.Building, "동" },
            { SortField.Unit, "호수" },
            { SortField.Area, "평" },
            { SortField.Favorite, "관심" },
            { SortField.Office, "부동산" },
            { SortField.PriceRange, "가격 범위" },
            { SortField.Status, "상태" },
            { SortField.Value, "감평액" },
            { SortField.Rank, "순위" },
            { SortField.Sold, "거래 완료" }
        };

        private void UpdateSortButtonLabels()
        {
            foreach (var (field, label) in SortFieldLabels)
            {
                var btn = FindSortButton(field);
                if (btn == null)
                {
                    continue;
                }

                var idx = _currentFilters.SortColumns.FindIndex(c => c.Field == field);
                if (idx < 0)
                {
                    btn.Content = label;
                }
                else
                {
                    var col = _currentFilters.SortColumns[idx];
                    var arrow = col.Direction == SortDirection.Ascending ? "↑" : "↓";
                    btn.Content = $"{idx + 1} {label} {arrow}";
                }
            }
        }

        private Button? FindSortButton(SortField field)
        {
            return field switch
            {
                SortField.Building => FindControl<Button>("SortBuildingBtn"),
                SortField.Unit => FindControl<Button>("SortUnitBtn"),
                SortField.Area => FindControl<Button>("SortAreaBtn"),
                SortField.Favorite => FindControl<Button>("SortFavoriteBtn"),
                SortField.Office => FindControl<Button>("SortOfficeBtn"),
                SortField.PriceRange => FindControl<Button>("SortPriceBtn"),
                SortField.Status => FindControl<Button>("SortStatusBtn"),
                SortField.Value => FindControl<Button>("SortValueBtn"),
                SortField.Rank => FindControl<Button>("SortRankBtn"),
                SortField.Sold => FindControl<Button>("SortSoldBtn"),
                _ => null
            };
        }

        private void WireUpControls()
        {
            _rootGrid = FindControl<Grid>("RootGrid");
            _statusText = FindControl<TextBlock>("StatusText");
            _houseList = FindControl<ListView>("HouseList");
            _clusterCombo = FindControl<ComboBox>("ClusterCombo");
            _buildingFilterButton = FindControl<Button>("BuildingFilterButton");
            _buildingFilterList = FindControl<ListView>("BuildingFilterList");
            _unitFilterButton = FindControl<Button>("UnitFilterButton");
            _unitFilterList = FindControl<ListView>("UnitFilterList");
            _areaFilterButton = FindControl<Button>("AreaFilterButton");
            _areaFilterList = FindControl<ListView>("AreaFilterList");
            _tagFilterButton = FindControl<Button>("TagFilterButton");
            _tagFilterList = FindControl<ListView>("TagFilterList");
            _favoriteOnlyCheck = FindControl<CheckBox>("FavoriteOnlyCheck");
            _showSoldCheck = FindControl<CheckBox>("ShowSoldCheck");
            _minValueBox = FindControl<TextBox>("MinValueBox");
            _maxValueBox = FindControl<TextBox>("MaxValueBox");
            _minRankBox = FindControl<TextBox>("MinRankBox");
            _maxRankBox = FindControl<TextBox>("MaxRankBox");

            if (_rootGrid != null)
            {
                _rootGrid.DataContext = this;
            }
        }

        private async Task LoadFilterOptionsAsync()
        {
            if (_repository == null)
            {
                return;
            }

            var buildings = await _repository.GetDistinctBuildingNumbersAsync();
            var units = await _repository.GetDistinctUnitNumbersAsync();
            var areas = await _repository.GetDistinctAreasAsync();
            var tags = await _repository.GetDistinctTagsAsync();

            if (_buildingFilterList != null) _buildingFilterList.ItemsSource = buildings;
            if (_unitFilterList != null) _unitFilterList.ItemsSource = units;
            if (_areaFilterList != null) _areaFilterList.ItemsSource = areas;
            if (_tagFilterList != null) _tagFilterList.ItemsSource = tags;
        }

        private void BuildingFlyout_Closed(object sender, object e)
        {
            UpdateMultiSelectButtonText(_buildingFilterList, _buildingFilterButton);
        }

        private void UnitFlyout_Closed(object sender, object e)
        {
            UpdateMultiSelectButtonText(_unitFilterList, _unitFilterButton);
        }

        private void AreaFlyout_Closed(object sender, object e)
        {
            UpdateMultiSelectButtonText(_areaFilterList, _areaFilterButton);
        }

        private void TagFlyout_Closed(object sender, object e)
        {
            UpdateMultiSelectButtonText(_tagFilterList, _tagFilterButton);
        }

        private static void UpdateMultiSelectButtonText(ListView? listView, Button? button)
        {
            if (listView == null || button == null)
            {
                return;
            }

            var count = listView.SelectedItems.Count;
            button.Content = count == 0 ? "전체" : $"{count}개 선택";
        }

        private static void ClearMultiSelect(ListView? listView, Button? button)
        {
            if (listView != null)
            {
                listView.SelectedItems.Clear();
            }

            if (button != null)
            {
                button.Content = "전체";
            }
        }

        private T? FindControl<T>(string name) where T : class
        {
            return (Content as FrameworkElement)?.FindName(name) as T;
        }

        private async void HouseExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (_repository == null)
            {
                return;
            }

            if (sender.DataContext is not HouseView house)
            {
                return;
            }

            if (house.Items.Count > 0)
            {
                return;
            }

            var items = await _repository.GetItemsForHouseAsync(house.HouseId);
            foreach (var item in items)
            {
                house.Items.Add(item);
            }
        }

        private async Task PopulateHouseItemsAsync(IEnumerable<HouseView> houses)
        {
            if (_repository == null)
            {
                return;
            }

            foreach (var house in houses)
            {
                if (house.Items.Count > 0)
                {
                    continue;
                }

                var items = await _repository.GetItemsForHouseAsync(house.HouseId);
                foreach (var item in items)
                {
                    house.Items.Add(item);
                }
            }
        }

        private void HouseExpander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args)
        {
            // Keep items loaded so status/office-group counts stay visible when collapsed.
        }
    }
}
