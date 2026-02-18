# Tags Column & Multi-Select Filters

**Date:** 2026-02-14

## Summary

Added a "태그" (tags) column to the house table and converted the 동, 호수, 평수 filters
from plain text boxes to multi-selectable dropdown lists. Also added 관심 (favorite)
and 태그 (tags) filters. All filters now fit in a single row.

## Changes

### `apps/Wysg.Musm.BooHill/Models/FilterOptions.cs`
- `BuildingNumber` → `BuildingNumbers` (`List<string>?`, multi-value IN filter)
- `UnitNumber` → `UnitNumbers` (`List<string>?`, multi-value IN filter)
- `Area` → `Areas` (`List<string>?`, multi-value IN filter)
- Added `FavoriteOnly` (`bool`)
- Added `Tags` (`List<string>?`)

### `apps/Wysg.Musm.BooHill/Models/HouseModels.cs`
- Added `Tags` property to `HouseEdit` and `HouseView`

### `apps/Wysg.Musm.BooHill/BooHillRepository.cs`
- `GetHousesAsync`: SQL SELECT now includes `COALESCE(h.tags, '') AS tags` (index 17);
  filter WHERE clauses use IN for buildings/units/areas; added FavoriteOnly and Tags filters
- `GetHousesWithItemsAsync`: same SQL SELECT update + reader mapping for tags
- `UpsertHouseAsync`: INSERT/UPDATE now includes `tags` column
- Added `EnsureTagsColumnAsync()`: migrates DB schema (ALTER TABLE ADD COLUMN tags TEXT)
- Added `GetDistinctBuildingNumbersAsync()`, `GetDistinctUnitNumbersAsync()`,
  `GetDistinctAreasAsync()`, `GetDistinctTagsAsync()` for populating filter dropdowns

### `apps/Wysg.Musm.BooHill/MainWindow.xaml`
- Filter section: one-row layout with multi-select dropdowns (Button + Flyout + ListView
  with SelectionMode="Multiple") for 동/호수/평수/태그; CheckBoxes for 관심 and 거래;
  inline min~max for 감평액/순위
- House table header: added 태그 column (120px, Grid.Column="10")
- House data template: added 태그 TextBlock bound to `{Binding Tags}`
- Column count increased from 11 to 12 (shifted 거래완료 to Column="11")

### `apps/Wysg.Musm.BooHill/MainWindow.xaml.cs`
- Replaced `_buildingBox`/`_unitBox`/`_areaBox` (TextBox) fields with
  `_buildingFilterButton`/`_buildingFilterList`, `_unitFilterButton`/`_unitFilterList`,
  `_areaFilterButton`/`_areaFilterList` (Button + ListView) pairs
- Added `_tagFilterButton`/`_tagFilterList`, `_favoriteOnlyCheck`
- `WireUpControls()`: wires all new named elements
- Added `LoadFilterOptionsAsync()`: populates dropdowns from DB distinct values
- Added flyout `Closed` handlers: `BuildingFlyout_Closed`, `UnitFlyout_Closed`,
  `AreaFlyout_Closed`, `TagFlyout_Closed`
- Added `UpdateMultiSelectButtonText()`, `ClearMultiSelect()`, `GetSelectedStrings()` helpers
- `ReadFiltersFromUi()`: reads selected items from ListViews instead of TextBox text
- `ClearFilters_Click()`: clears ListView selections + resets button labels
