using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Wysg.Musm.BooHill;

public sealed class AdminWindow : Window
{
    private BooHillRepository? _repository;
    private FilterOptions _filters = new();
    private HouseView? _selectedHouse;

    private Grid? _rootGrid;
    private TextBlock? _statusText;
    private ComboBox? _clusterCombo;
    private CheckBox? _showSoldCheck;
    private ListView? _houseList;
    private ListView? _itemList;
    private TextBlock? _itemsHeader;
    private Button? _addItemButton;
    private Button? _massImportItemsButton;
    private Button? _fixAreaButton;

    public ObservableCollection<HouseView> Houses { get; } = new();
    public ObservableCollection<ItemRecord> Items { get; } = new();
    public ObservableCollection<ClusterRecord> Clusters { get; } = new();

    public AdminWindow()
    {
        _filters.SortColumns.Add(new SortColumn { Field = SortField.Default, Direction = SortDirection.Descending });
        BuildLayout();
    }

    private void BuildLayout()
    {
        Title = "Admin";

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var headerPanel = new StackPanel
        {
            Spacing = 12,
            Padding = new Thickness(16),
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"]
        };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        titleRow.Children.Add(new TextBlock { Text = "Admin - Houses & Items", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var massImportHousesButton = new Button { Content = "Mass Import Houses" };
        massImportHousesButton.Click += MassImportHouses_Click;
        titleRow.Children.Add(massImportHousesButton);
        _fixAreaButton = new Button { Content = "Fix Area 66+ (47→24)" };
        _fixAreaButton.Click += FixArea_Click;
        titleRow.Children.Add(_fixAreaButton);
        var refreshButton = new Button { Content = "Refresh" };
        refreshButton.Click += Refresh_Click;
        titleRow.Children.Add(refreshButton);
        _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        titleRow.Children.Add(_statusText);
        headerPanel.Children.Add(titleRow);

        var filterGrid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        var clusterPanel = new StackPanel { Spacing = 4 };
        clusterPanel.Children.Add(new TextBlock { Text = "Cluster" });
        _clusterCombo = new ComboBox
        {
            PlaceholderText = "All",
            ItemsSource = Clusters,
            DisplayMemberPath = "Display",
            SelectedValuePath = "ClusterId"
        };
        clusterPanel.Children.Add(_clusterCombo);
        filterGrid.Children.Add(clusterPanel);

        var showSoldPanel = new StackPanel { Spacing = 4 };
        Grid.SetColumn(showSoldPanel, 1);
        showSoldPanel.Children.Add(new TextBlock { Text = "Sold" });
        _showSoldCheck = new CheckBox { Content = "Show sold" };
        showSoldPanel.Children.Add(_showSoldCheck);
        filterGrid.Children.Add(showSoldPanel);

        var applyPanel = new StackPanel { Spacing = 8, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetColumn(applyPanel, 2);
        var applyButton = new Button { Content = "Apply" };
        applyButton.Click += ApplyFilters_Click;
        applyPanel.Children.Add(applyButton);
        var clearButton = new Button { Content = "Clear" };
        clearButton.Click += ClearFilters_Click;
        applyPanel.Children.Add(clearButton);
        filterGrid.Children.Add(applyPanel);

        headerPanel.Children.Add(filterGrid);
        Grid.SetRow(headerPanel, 0);
        root.Children.Add(headerPanel);

        var contentGrid = new Grid { ColumnSpacing = 12, Padding = new Thickness(16) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

        var houseSection = new Grid
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var houseHeader = new StackPanel { Spacing = 6, Padding = new Thickness(12), Background = (Brush)Application.Current.Resources["LayerFillColorAltBrush"] };
        var houseHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        houseHeaderRow.Children.Add(new TextBlock { Text = "Houses", FontWeight = FontWeights.SemiBold });
        var addHouseButton = new Button { Content = "Add" };
        addHouseButton.Click += AddHouse_Click;
        houseHeaderRow.Children.Add(addHouseButton);
        var sortBuildingButton = new Button { Content = "Sort Building" };
        sortBuildingButton.Click += SortBuilding_Click;
        houseHeaderRow.Children.Add(sortBuildingButton);
        var sortPriceButton = new Button { Content = "Sort Price" };
        sortPriceButton.Click += SortPrice_Click;
        houseHeaderRow.Children.Add(sortPriceButton);
        houseHeader.Children.Add(houseHeaderRow);

        var houseColumns = new Grid { ColumnSpacing = 8 };
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        houseColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        houseColumns.Children.Add(new TextBlock { Text = "ID", FontWeight = FontWeights.SemiBold });
        var houseBuilding = new TextBlock { Text = "Building", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseBuilding, 1);
        houseColumns.Children.Add(houseBuilding);
        var houseUnit = new TextBlock { Text = "Unit", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseUnit, 2);
        houseColumns.Children.Add(houseUnit);
        var houseArea = new TextBlock { Text = "Area", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseArea, 3);
        houseColumns.Children.Add(houseArea);
        var housePrice = new TextBlock { Text = "Price Range", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(housePrice, 4);
        houseColumns.Children.Add(housePrice);
        var houseValue = new TextBlock { Text = "Value", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseValue, 5);
        houseColumns.Children.Add(houseValue);
        var houseRank = new TextBlock { Text = "Rank", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseRank, 6);
        houseColumns.Children.Add(houseRank);
        var houseActions = new TextBlock { Text = "Actions", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(houseActions, 7);
        houseColumns.Children.Add(houseActions);
        houseHeader.Children.Add(houseColumns);
        houseSection.Children.Add(houseHeader);

        _houseList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = Houses
        };
        _houseList.SelectionChanged += AdminHouseList_OnSelectionChanged;

        var houseTemplateXaml = @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Grid Padding='8' ColumnSpacing='8'>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width='70'/>
            <ColumnDefinition Width='110'/>
            <ColumnDefinition Width='110'/>
            <ColumnDefinition Width='90'/>
            <ColumnDefinition Width='140'/>
            <ColumnDefinition Width='110'/>
            <ColumnDefinition Width='110'/>
            <ColumnDefinition Width='*'/>
        </Grid.ColumnDefinitions>
        <TextBlock Text='{Binding HouseId}' />
        <TextBlock Grid.Column='1' Text='{Binding BuildingNumber}' />
        <TextBlock Grid.Column='2' Text='{Binding UnitNumber}' />
        <TextBlock Grid.Column='3' Text='{Binding Area}' />
        <TextBlock Grid.Column='4' Text='{Binding PriceRange}' />
        <TextBlock Grid.Column='5' Text='{Binding ValueDisplay}' />
        <TextBlock Grid.Column='6' Text='{Binding RankDisplay}' />
        <StackPanel Grid.Column='7' Orientation='Horizontal' Spacing='6'>
            <Button Content='{Binding FavoriteGlyph}' Tag='{Binding HouseId}' Click='AdminFavorite_Click' />
            <Button Content='Edit' Tag='{Binding HouseId}' Click='EditHouse_Click' />
            <Button Content='Delete' Tag='{Binding HouseId}' Click='DeleteHouse_Click' />
            <Button Content='Items' Tag='{Binding HouseId}' Click='OpenItems_Click' />
        </StackPanel>
    </Grid>
</DataTemplate>";
        _houseList.ItemTemplate = (DataTemplate)XamlReader.Load(houseTemplateXaml);
        var houseScroll = new ScrollViewer { Content = _houseList };
        Grid.SetRow(houseScroll, 1);
        houseSection.Children.Add(houseScroll);
        Grid.SetColumn(houseSection, 0);
        contentGrid.Children.Add(houseSection);

        var itemSection = new Grid
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var itemHeader = new StackPanel { Spacing = 6, Padding = new Thickness(12), Background = (Brush)Application.Current.Resources["LayerFillColorAltBrush"] };
        var itemHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _itemsHeader = new TextBlock { Text = "Select a house", FontWeight = FontWeights.SemiBold };
        itemHeaderRow.Children.Add(_itemsHeader);
        _addItemButton = new Button { Content = "Add Item", IsEnabled = false };
        _addItemButton.Click += AddItem_Click;
        itemHeaderRow.Children.Add(_addItemButton);
        _massImportItemsButton = new Button { Content = "Mass Import Items", IsEnabled = false };
        _massImportItemsButton.Click += MassImportItems_Click;
        itemHeaderRow.Children.Add(_massImportItemsButton);
        itemHeader.Children.Add(itemHeaderRow);

        var itemColumns = new Grid { ColumnSpacing = 8 };
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        itemColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        itemColumns.Children.Add(new TextBlock { Text = "ID", FontWeight = FontWeights.SemiBold });
        var itemPrice = new TextBlock { Text = "Price", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemPrice, 1);
        itemColumns.Children.Add(itemPrice);
        var itemOffice = new TextBlock { Text = "Office", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemOffice, 2);
        itemColumns.Children.Add(itemOffice);
        var itemUpdated = new TextBlock { Text = "Updated", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemUpdated, 3);
        itemColumns.Children.Add(itemUpdated);
        var itemAdded = new TextBlock { Text = "Added", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemAdded, 4);
        itemColumns.Children.Add(itemAdded);
        var itemRemark = new TextBlock { Text = "Remark", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemRemark, 5);
        itemColumns.Children.Add(itemRemark);
        var itemActions = new TextBlock { Text = "Actions", FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(itemActions, 6);
        itemColumns.Children.Add(itemActions);
        itemHeader.Children.Add(itemColumns);
        itemSection.Children.Add(itemHeader);

        _itemList = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            ItemsSource = Items
        };

        var itemTemplateXaml = @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Grid Padding='8' ColumnSpacing='8'>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width='70'/>
            <ColumnDefinition Width='100'/>
            <ColumnDefinition Width='110'/>
            <ColumnDefinition Width='100'/>
            <ColumnDefinition Width='100'/>
            <ColumnDefinition Width='*'/>
            <ColumnDefinition Width='160'/>
        </Grid.ColumnDefinitions>
        <TextBlock Text='{Binding ItemId}' />
        <TextBlock Grid.Column='1' Text='{Binding PriceDisplay}' />
        <TextBlock Grid.Column='2' Text='{Binding Office}' />
        <TextBlock Grid.Column='3' Text='{Binding LastUpdatedDate}' />
        <TextBlock Grid.Column='4' Text='{Binding AddedDate}' />
        <TextBlock Grid.Column='5' Text='{Binding Remark}' TextWrapping='Wrap' />
        <StackPanel Grid.Column='6' Orientation='Horizontal' Spacing='6'>
            <Button Content='Edit' Tag='{Binding ItemId}' Click='EditItem_Click' />
            <Button Content='Delete' Tag='{Binding ItemId}' Click='DeleteItem_Click' />
        </StackPanel>
    </Grid>
</DataTemplate>";
        _itemList.ItemTemplate = (DataTemplate)XamlReader.Load(itemTemplateXaml);
        var itemScroll = new ScrollViewer { Content = _itemList };
        Grid.SetRow(itemScroll, 1);
        itemSection.Children.Add(itemScroll);
        Grid.SetColumn(itemSection, 1);
        contentGrid.Children.Add(itemSection);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        _rootGrid = root;
        Content = root;
        root.Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_rootGrid != null)
            {
                _rootGrid.DataContext = this;
            }

            _repository = await BooHillRepository.CreateAsync();
            await LoadClustersAsync();
            await LoadHousesAsync();
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

    private async Task LoadHousesAsync()
    {
        if (_repository == null)
        {
            return;
        }

        if (_statusText != null)
        {
            _statusText.Text = "Loading houses...";
        }
        var filters = BuildFiltersFromUi();
        _filters = filters;

        var houses = await _repository.GetHousesAsync(filters);
        Houses.Clear();
        foreach (var house in houses)
        {
            Houses.Add(house);
        }

        if (_statusText != null)
        {
            _statusText.Text = Houses.Count == 0 ? "No houses found." : $"Houses: {Houses.Count}";
        }

        if (_selectedHouse != null)
        {
            var match = Houses.FirstOrDefault(h => h.HouseId == _selectedHouse.HouseId);
            if (match != null)
            {
                if (_houseList != null)
                {
                    _houseList.SelectedItem = match;
                }
            }
            else if (Houses.Count > 0)
            {
                if (_houseList != null)
                {
                    _houseList.SelectedIndex = 0;
                }
            }
        }
        else if (Houses.Count > 0)
        {
            if (_houseList != null)
            {
                _houseList.SelectedIndex = 0;
            }
        }

        await LoadItemsForSelectionAsync();
    }

    private async Task LoadItemsForSelectionAsync()
    {
        if (_repository == null || _selectedHouse == null)
        {
            Items.Clear();
            if (_itemsHeader != null)
            {
                _itemsHeader.Text = "Select a house";
            }
            UpdateItemButtons();
            return;
        }

        if (_itemsHeader != null)
        {
            _itemsHeader.Text = $"for House {_selectedHouse.HouseId} ({_selectedHouse.BuildingNumber} {_selectedHouse.UnitNumber})";
        }
        var items = await _repository.GetItemsForHouseAsync(_selectedHouse.HouseId);
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
        UpdateItemButtons();
    }

    private FilterOptions BuildFiltersFromUi()
    {
        int? selectedCluster = null;
        if (_clusterCombo?.SelectedValue is int cluster)
        {
            selectedCluster = cluster;
        }
        else if (_clusterCombo?.SelectedItem is ClusterRecord record)
        {
            selectedCluster = record.ClusterId;
        }

        return new FilterOptions
        {
            ClusterId = selectedCluster,
            ShowSold = _showSoldCheck?.IsChecked == true,
            SortColumns = new List<SortColumn>(_filters.SortColumns),
        };
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
        await LoadHousesAsync();
    }

    private async void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        if (_clusterCombo != null)
        {
            _clusterCombo.SelectedItem = null;
        }

        if (_showSoldCheck != null)
        {
            _showSoldCheck.IsChecked = false;
        }
        _filters = new FilterOptions();
        await LoadHousesAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadHousesAsync();
    }

    private async void FixArea_Click(object sender, RoutedEventArgs e)
    {
        if (_repository == null)
        {
            return;
        }

        try
        {
            if (_statusText != null)
            {
                _statusText.Text = "Updating area for houses 66+...";
            }

            var updated = await _repository.UpdateHouseAreaAsync(66, "47", "24");

            if (_statusText != null)
            {
                _statusText.Text = $"Area updated for {updated} houses.";
            }

            if (_fixAreaButton != null)
            {
                _fixAreaButton.IsEnabled = false;
            }

            await LoadHousesAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Failed to update areas", ex.Message);
        }
    }

    private async void SortBuilding_Click(object sender, RoutedEventArgs e)
    {
        ToggleSort(SortField.Building);
        await LoadHousesAsync();
    }

    private async void SortPrice_Click(object sender, RoutedEventArgs e)
    {
        ToggleSort(SortField.PriceRange);
        await LoadHousesAsync();
    }

    private void ToggleSort(SortField field)
    {
        var columns = _filters.SortColumns;
        var existing = columns.FindIndex(c => c.Field == field);
        if (existing >= 0)
        {
            var col = columns[existing];
            col.Direction = col.Direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            columns.Clear();
            columns.Add(new SortColumn { Field = field, Direction = SortDirection.Ascending });
        }
    }

    private async void AddHouse_Click(object sender, RoutedEventArgs e)
    {
        var edit = new HouseEdit
        {
            ClusterId = Clusters.FirstOrDefault()?.ClusterId ?? 1,
            Area = "48"
        };

        var saved = await ShowHouseDialogAsync(edit);
        if (saved)
        {
            await SaveHouseAsync(edit);
        }
    }

    private async void EditHouse_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long houseId)
        {
            return;
        }

        var house = Houses.FirstOrDefault(h => h.HouseId == houseId);
        if (house == null)
        {
            return;
        }

        var edit = new HouseEdit
        {
            HouseId = house.HouseId,
            ClusterId = house.ClusterId,
            BuildingNumber = house.BuildingNumber,
            UnitNumber = house.UnitNumber,
            Area = house.Area,
            Direction = house.Direction,
            IsSold = house.IsSold,
            IsFavorite = house.IsFavorite,
            Value = house.Value,
            ValueEstimate = house.ValueEstimate,
            Rank = house.Rank,
            RankEstimate = house.RankEstimate
        };

        var saved = await ShowHouseDialogAsync(edit);
        if (saved)
        {
            await SaveHouseAsync(edit);
        }
    }

    private async Task SaveHouseAsync(HouseEdit edit)
    {
        if (_repository == null)
        {
            return;
        }

        await _repository.UpsertHouseAsync(edit);
        await LoadHousesAsync();
    }

    private async void DeleteHouse_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long houseId || _repository == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete house?",
            Content = "This will remove the house and its items.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await _repository.DeleteHouseAsync(houseId);
        await LoadHousesAsync();
    }

    private async void AdminFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long houseId || _repository == null)
        {
            return;
        }

        await _repository.ToggleFavoriteAsync(houseId);
        await LoadHousesAsync();
    }

    private async void OpenItems_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long houseId)
        {
            return;
        }

        var house = Houses.FirstOrDefault(h => h.HouseId == houseId);
        if (house != null)
        {
            if (_houseList != null)
            {
                _houseList.SelectedItem = house;
            }
            await LoadItemsForSelectionAsync();
        }
    }

    private async void AdminHouseList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedHouse = _houseList?.SelectedItem as HouseView;
        UpdateItemButtons();
        await LoadItemsForSelectionAsync();
    }

    private void UpdateItemButtons()
    {
        var enabled = _selectedHouse != null;
        if (_addItemButton != null)
        {
            _addItemButton.IsEnabled = enabled;
        }

        if (_massImportItemsButton != null)
        {
            _massImportItemsButton.IsEnabled = enabled;
        }
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedHouse == null)
        {
            return;
        }

        var edit = new ItemRecord
        {
            HouseId = _selectedHouse.HouseId,
            LastUpdatedDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var saved = await ShowItemDialogAsync(edit, false);
        if (saved)
        {
            await SaveItemAsync(edit);
        }
    }

    private async void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long itemId || _selectedHouse == null)
        {
            return;
        }

        var existing = Items.FirstOrDefault(i => i.ItemId == itemId);
        if (existing == null)
        {
            return;
        }

        var edit = new ItemRecord
        {
            ItemId = existing.ItemId,
            HouseId = existing.HouseId,
            Price = existing.Price,
            Office = existing.Office,
            LastUpdatedDate = existing.LastUpdatedDate,
            AddedDate = existing.AddedDate,
            Remark = existing.Remark
        };

        var saved = await ShowItemDialogAsync(edit, true);
        if (saved)
        {
            await SaveItemAsync(edit);
        }
    }

    private async Task SaveItemAsync(ItemRecord edit)
    {
        if (_repository == null)
        {
            return;
        }

        await _repository.UpsertItemAsync(edit);
        await LoadItemsForSelectionAsync();
    }

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not long itemId || _repository == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete item?",
            Content = "This will remove the item entry.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await _repository.DeleteItemAsync(itemId);
        await LoadItemsForSelectionAsync();
    }

    private async void MassImportHouses_Click(object sender, RoutedEventArgs e)
    {
        if (_repository == null)
        {
            return;
        }

        var raw = await ShowRawTextDialogAsync("Mass House + Item Import");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var result = await _repository.ImportHouseBatchAsync(raw);
        if (_statusText != null)
        {
            _statusText.Text = $"Imported Houses: {result.HousesInserted}, Items: {result.ItemsInserted}";
        }
        await LoadHousesAsync();
    }

    private async void MassImportItems_Click(object sender, RoutedEventArgs e)
    {
        if (_repository == null || _selectedHouse == null)
        {
            return;
        }

        var raw = await ShowRawTextDialogAsync("Mass Item Import");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var inserted = await _repository.ImportItemsAsync(_selectedHouse.HouseId, raw);
        if (_statusText != null)
        {
            _statusText.Text = $"Items imported: {inserted}";
        }
        await LoadItemsForSelectionAsync();
    }

    private async Task<bool> ShowHouseDialogAsync(HouseEdit edit)
    {
        var clusterBox = new ComboBox
        {
            ItemsSource = Clusters,
            DisplayMemberPath = "Display",
            SelectedValuePath = "ClusterId",
            SelectedValue = edit.ClusterId,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var buildingBox = new TextBox { Text = edit.BuildingNumber, PlaceholderText = "Building number" };
        var unitBox = new TextBox { Text = edit.UnitNumber, PlaceholderText = "Unit number" };
        var areaBox = new TextBox { Text = edit.Area, PlaceholderText = "Area" };
        var soldCheck = new CheckBox { Content = "Sold", IsChecked = edit.IsSold };
        var favoriteCheck = new CheckBox { Content = "Favorite", IsChecked = edit.IsFavorite };
        var valueBox = new TextBox { Text = edit.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, PlaceholderText = "Value" };
        var valueEstBox = new TextBox { Text = edit.ValueEstimate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, PlaceholderText = "Value Estimate" };
        var rankBox = new TextBox { Text = edit.Rank?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, PlaceholderText = "Rank" };
        var rankEstBox = new TextBox { Text = edit.RankEstimate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, PlaceholderText = "Rank Estimate" };
        var directionBox = new TextBox { Text = edit.Direction, PlaceholderText = "Direction" };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Cluster" });
        panel.Children.Add(clusterBox);
        panel.Children.Add(new TextBlock { Text = "Building" });
        panel.Children.Add(buildingBox);
        panel.Children.Add(new TextBlock { Text = "Unit" });
        panel.Children.Add(unitBox);
        panel.Children.Add(new TextBlock { Text = "Area" });
        panel.Children.Add(areaBox);
        panel.Children.Add(soldCheck);
        panel.Children.Add(favoriteCheck);
        panel.Children.Add(new TextBlock { Text = "Value" });
        panel.Children.Add(valueBox);
        panel.Children.Add(new TextBlock { Text = "Value Estimate" });
        panel.Children.Add(valueEstBox);
        panel.Children.Add(new TextBlock { Text = "Rank" });
        panel.Children.Add(rankBox);
        panel.Children.Add(new TextBlock { Text = "Rank Estimate" });
        panel.Children.Add(rankEstBox);
        panel.Children.Add(new TextBlock { Text = "Direction" });
        panel.Children.Add(directionBox);

        var dialog = new ContentDialog
        {
            Title = edit.HouseId == 0 ? "Add House" : "Edit House",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        edit.ClusterId = clusterBox.SelectedValue is int cid ? cid : edit.ClusterId;
        edit.BuildingNumber = buildingBox.Text.Trim();
        edit.UnitNumber = unitBox.Text.Trim();
        edit.Area = areaBox.Text.Trim();
        edit.IsSold = soldCheck.IsChecked == true;
        edit.IsFavorite = favoriteCheck.IsChecked == true;
        edit.Value = TryParseDouble(valueBox.Text);
        edit.ValueEstimate = TryParseDouble(valueEstBox.Text);
        edit.Rank = TryParseDouble(rankBox.Text);
        edit.RankEstimate = TryParseDouble(rankEstBox.Text);
        edit.Direction = directionBox.Text.Trim();

        return true;
    }

    private async Task<bool> ShowItemDialogAsync(ItemRecord edit, bool isEdit)
    {
        var priceBox = new TextBox { Text = edit.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, PlaceholderText = "Price" };
        var officeBox = new TextBox { Text = edit.Office ?? string.Empty, PlaceholderText = "Office" };
        var updatedBox = new TextBox { Text = edit.LastUpdatedDate ?? string.Empty, PlaceholderText = "Last Updated (YYYY-MM-DD)" };
        var remarkBox = new TextBox { Text = edit.Remark ?? string.Empty, PlaceholderText = "Remark" };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Price" });
        panel.Children.Add(priceBox);
        panel.Children.Add(new TextBlock { Text = "Office" });
        panel.Children.Add(officeBox);
        panel.Children.Add(new TextBlock { Text = "Last Updated" });
        panel.Children.Add(updatedBox);
        panel.Children.Add(new TextBlock { Text = "Remark" });
        panel.Children.Add(remarkBox);

        var dialog = new ContentDialog
        {
            Title = isEdit ? "Edit Item" : "Add Item",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        edit.Price = TryParseDouble(priceBox.Text);
        edit.Office = officeBox.Text.Trim();
        edit.LastUpdatedDate = updatedBox.Text.Trim();
        edit.Remark = remarkBox.Text.Trim();

        return true;
    }

    private async Task<string?> ShowRawTextDialogAsync(string title)
    {
        var textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 200,
            PlaceholderText = "Paste raw listing text here"
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return textBox.Text;
    }

    private static double? TryParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
