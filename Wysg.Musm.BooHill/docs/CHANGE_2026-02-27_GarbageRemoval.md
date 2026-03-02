# CHANGE 2026-02-27: One-time garbage removal on app start

## Summary
On app startup, detect houses that have no associated items (orphan records likely caused by previous import errors) and offer to remove them via a confirmation dialog.

## Changes

### `BooHillRepository.cs`
- Added `GetHousesWithoutItemsCountAsync()` — returns the count of houses with zero items.
- Added `DeleteHousesWithoutItemsAsync()` — deletes all houses that have no items.

### `MainWindow.xaml.cs`
- Added `RemoveHousesWithoutItemsAsync()` — called during `MainWindow_OnLoaded` after repository creation. Shows a `ContentDialog` with the orphan house count and asks the user whether to delete them. Only deletes on "Yes".

### `Package.appxmanifest`
- Version bumped from `1.0.1.0` → `1.0.2.0`.
