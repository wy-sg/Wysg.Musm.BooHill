# Multi-Column Sort Row

**Date:** 2026-02-15

## Summary

Added a dedicated "정렬" (sort) row below the filter section that supports multi-column
sorting with user-defined priority and direction per column.

## Problem

Sorting was limited to two columns ("동" and "가격범위") via header buttons, supporting
only single-column sort at a time.

## Solution

Replaced the single `SortField`/`SortDirection` model with a prioritized
`List<SortColumn>`, each carrying a `SortField` and `SortDirection`. The UI presents
ten sort buttons in a dedicated row; clicking a button adds it as the next sort
priority (ascending). Clicking an active column toggles it to descending, and clicking
again removes it. Button labels show the priority number and direction arrow
(e.g. "1 동 ↑", "2 가격 범위 ↓").

### Sortable columns

| Label      | DB expression                         |
|------------|---------------------------------------|
| 동         | `building_number COLLATE NOCASE`      |
| 호수       | `unit_number COLLATE NOCASE`          |
| 평         | `area COLLATE NOCASE`                 |
| 관심       | `is_favorite`                         |
| 부동산     | `item_total`                          |
| 가격 범위  | `min_price` / `max_price`             |
| 상태       | `is_new_today`                        |
| 감평액     | `value`                               |
| 순위       | `rank`                                |
| 거래 완료  | `is_sold`                             |

## Changes

### `Models/FilterOptions.cs`
- Removed `SortField` and `SortDirection` properties from `FilterOptions`
- Added `SortColumns` (`List<SortColumn>`) property
- Added `SortColumn` class with `Field` and `Direction`
- Expanded `SortField` enum: added `Unit`, `Area`, `Favorite`, `Office`, `Status`,
  `Value`, `Rank`, `Sold`

### `BooHillRepository.cs`
- `GetHousesAsync`: ORDER BY now iterates `SortColumns` to build a multi-column clause;
  falls back to `house_id DESC` when no sort columns are specified

### `MainWindow.xaml`
- Added "정렬" label and a `StackPanel` with 10 sort buttons + "정렬 초기화" clear button
- Replaced old sort `Button` elements in the table header with plain `TextBlock` headers

### `MainWindow.xaml.cs`
- Removed `SortBuilding_Click`, `SortPrice_Click`, `ToggleSort`
- Added `SortColumn_Click`: click-to-add (ASC) → click-to-toggle (DESC) → click-to-remove
- Added `ClearSort_Click`: resets all sort columns
- Added `UpdateSortButtonLabels`: refreshes button text with priority number and arrow
- Added `FindSortButton`, `SortFieldLabels` helpers
- `ReadFiltersFromUi` now copies `SortColumns` list
- `ClearFilters_Click` also resets sort button labels

### `AdminWindow.xaml.cs`
- Migrated from `SortField`/`SortDirection` to `SortColumns` list
- Added `System.Collections.Generic` using directive
