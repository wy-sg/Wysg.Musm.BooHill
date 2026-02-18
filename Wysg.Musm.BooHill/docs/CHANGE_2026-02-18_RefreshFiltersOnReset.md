# Refresh Filter Items on Reset

**Date:** 2026-02-18

## Summary

Pressing the "초기화" (Reset) button now refreshes the filter combobox items so that
newly imported data (e.g. from bulk import) is immediately visible in the filter
dropdowns.

## Problem

After adding rows via bulk import, opening a filter combobox in the main window showed
stale items. The filter item lists (building numbers, unit numbers, areas, tags, and
clusters) were only loaded once at startup and were never refreshed when the user
pressed the reset button.

## Solution

Added calls to `LoadClustersAsync()` and `LoadFilterOptionsAsync()` inside
`ClearFilters_Click` so that every reset re-queries the database for distinct filter
values before reloading the house list.

## Changes

### `MainWindow.xaml.cs`
- `ClearFilters_Click`: added `await LoadClustersAsync()` and
  `await LoadFilterOptionsAsync()` before `LoadHousesAsync` so all filter dropdowns
  are repopulated from the database on each reset.
