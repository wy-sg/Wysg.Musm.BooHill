using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Graphics;

namespace Wysg.Musm.BooHill;

public sealed partial class DailyLogWindow : Window
{
    private CalendarDatePicker? _datePicker;
    private Grid? _summaryGrid;
    private TextBlock? _parseCountText;
    private TextBlock? _parseDetailText;
    private TextBlock? _dupCountText;
    private TextBlock? _dupDetailText;
    private TextBlock? _mergeCountText;
    private TextBlock? _mergeDetailText;
    private TextBlock? _newCountText;
    private TextBlock? _newDetailText;
    private TextBlock? _totalLinesText;
    private ListView? _logEntryList;
    private TextBlock? _emptyText;

    public ObservableCollection<LogEntryViewModel> Entries { get; } = new();

    public DailyLogWindow()
    {
        InitializeComponent();
        SetFixedSize(1100, 780);
        WireUpControls();

        // Pre-select today
        if (_datePicker != null)
        {
            _datePicker.Date = DateTimeOffset.Now;
        }
    }

    private void WireUpControls()
    {
        var root = Content as FrameworkElement;
        _datePicker = root?.FindName("DatePicker") as CalendarDatePicker;
        _summaryGrid = root?.FindName("SummaryGrid") as Grid;
        _parseCountText = root?.FindName("ParseCountText") as TextBlock;
        _parseDetailText = root?.FindName("ParseDetailText") as TextBlock;
        _dupCountText = root?.FindName("DupCountText") as TextBlock;
        _dupDetailText = root?.FindName("DupDetailText") as TextBlock;
        _mergeCountText = root?.FindName("MergeCountText") as TextBlock;
        _mergeDetailText = root?.FindName("MergeDetailText") as TextBlock;
        _newCountText = root?.FindName("NewCountText") as TextBlock;
        _newDetailText = root?.FindName("NewDetailText") as TextBlock;
        _totalLinesText = root?.FindName("TotalLinesText") as TextBlock;
        _logEntryList = root?.FindName("LogEntryList") as ListView;
        _emptyText = root?.FindName("EmptyText") as TextBlock;

        if (_logEntryList != null)
        {
            _logEntryList.ItemsSource = Entries;
        }
    }

    private async void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        Entries.Clear();

        if (args.NewDate is not DateTimeOffset selected)
        {
            if (_summaryGrid != null) _summaryGrid.Visibility = Visibility.Collapsed;
            if (_emptyText != null) { _emptyText.Text = "날짜를 선택하세요"; _emptyText.Visibility = Visibility.Visible; }
            if (_totalLinesText != null) _totalLinesText.Text = string.Empty;
            return;
        }

        var lines = await ImportLogger.ReadLogAsync(selected.DateTime).ConfigureAwait(false);

        DispatcherQueue?.TryEnqueue(() => PopulateEntries(lines));
    }

    private void PopulateEntries(List<string> lines)
    {
        Entries.Clear();

        if (lines.Count == 0)
        {
            if (_summaryGrid != null) _summaryGrid.Visibility = Visibility.Collapsed;
            if (_emptyText != null) { _emptyText.Text = "해당 날짜에 로그가 없습니다"; _emptyText.Visibility = Visibility.Visible; }
            if (_totalLinesText != null) _totalLinesText.Text = string.Empty;
            return;
        }

        if (_emptyText != null) _emptyText.Visibility = Visibility.Collapsed;
        if (_summaryGrid != null) _summaryGrid.Visibility = Visibility.Visible;

        var parseCount = 0;
        var parseHouses = 0;
        var parseItems = 0;
        var dupCount = 0;
        var dupHouses = 0;
        var dupItems = 0;
        var mergeCount = 0;
        var mergeItems = 0;
        var newCount = 0;
        var newHouses = 0;
        var newItems = 0;

        foreach (var line in lines)
        {
            var entry = ImportLogger.ParseLine(line);
            if (entry == null)
            {
                continue;
            }

            var vm = new LogEntryViewModel(entry);
            Entries.Add(vm);

            switch (entry.Action)
            {
                case "PARSE": parseCount++; break;
                case "PARSE_HOUSE": parseHouses++; break;
                case "PARSE_ITEM": parseItems++; break;
                case "PARSE_DUP": parseHouses++; break;
                case "PARSE_DUP_ITEM": parseItems++; break;
                case "IMPORT_DUP": dupCount++; break;
                case "IMPORT_DUP_HOUSE": dupHouses++; break;
                case "IMPORT_DUP_ITEM": dupItems++; break;
                case "MERGE": mergeCount++; break;
                case "MERGE_ITEM": mergeItems++; break;
                case "INSERT_NEW": newCount++; break;
                case "INSERT_NEW_HOUSE": newHouses++; break;
                case "INSERT_NEW_ITEM": newItems++; break;
            }
        }

        if (_parseCountText != null) _parseCountText.Text = $"{parseCount}회";
        if (_parseDetailText != null) _parseDetailText.Text = $"주택 {parseHouses} · 매물 {parseItems}";
        if (_dupCountText != null) _dupCountText.Text = $"{dupCount}건";
        if (_dupDetailText != null) _dupDetailText.Text = $"주택 {dupHouses} · 매물 {dupItems}";
        if (_mergeCountText != null) _mergeCountText.Text = $"{mergeCount}건";
        if (_mergeDetailText != null) _mergeDetailText.Text = $"매물 {mergeItems}";
        if (_newCountText != null) _newCountText.Text = $"{newCount}건";
        if (_newDetailText != null) _newDetailText.Text = $"주택 {newHouses} · 매물 {newItems}";
        if (_totalLinesText != null) _totalLinesText.Text = $"총 {Entries.Count}줄";
    }

    private void SetFixedSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(width, height));
    }
}

/// <summary>View model for a single log entry displayed in the list.</summary>
public sealed class LogEntryViewModel
{
    public LogEntryViewModel(LogEntry entry)
    {
        Timestamp = entry.Timestamp;
        Detail = entry.Detail;
        Action = entry.Action;

        // Indentation level: 0 = summary, 1 = house, 2 = item
        var level = entry.Indent >= 4 ? 2 : entry.Indent >= 2 ? 1 : 0;

        LeftPadding = level switch
        {
            2 => "48,4,8,4",
            1 => "24,4,8,4",
            _ => "8,6,8,6"
        };

        RowOpacity = level switch
        {
            2 => 0.7,
            1 => 0.85,
            _ => 1.0
        };

        DetailFontSize = level >= 2 ? 12 : 14;
        LabelWeight = level == 0 ? "SemiBold" : "Normal";

        (ActionLabel, ActionColor) = entry.Action switch
        {
            "PARSE" => ("🔍 파싱", "#E3F2FD"),
            "PARSE_HOUSE" => ("  🏘 주택", "#BBDEFB"),
            "PARSE_ITEM" => ("    📄 매물", "#E3F2FD"),
            "PARSE_DUP" => ("  🔁 중복 주택", "#FFE0B2"),
            "PARSE_DUP_ITEM" => ("    📄 매물", "#FFF3E0"),
            "IMPORT_DUP" => ("📥 중복 가져오기", "#FFF3E0"),
            "IMPORT_DUP_HOUSE" => ("  🏘 주택", "#FFE0B2"),
            "IMPORT_DUP_ITEM" => ("    📄 매물", "#FFF3E0"),
            "MERGE" => ("🔗 합치기", "#E8F5E9"),
            "MERGE_ITEM" => ("  📄 매물", "#C8E6C9"),
            "INSERT_NEW" => ("🏠 새 주택", "#F3E5F5"),
            "INSERT_NEW_HOUSE" => ("  🏘 주택", "#E1BEE7"),
            "INSERT_NEW_ITEM" => ("    📄 매물", "#F3E5F5"),
            _ => (entry.Action, "Transparent")
        };
    }

    public string Timestamp { get; }
    public string Action { get; }
    public string ActionLabel { get; }
    public string ActionColor { get; }
    public string Detail { get; }
    public string LeftPadding { get; }
    public double RowOpacity { get; }
    public int DetailFontSize { get; }
    public string LabelWeight { get; }
}
