# No-DB Fallback: Create Blank Database

**Date:** 2026-02-15

## Summary

When no legacy SQLite database file is found during startup, the app now creates a
blank database with the required schema instead of throwing a `FileNotFoundException`.

## Problem

If the packaged legacy database (`docs/boohill_legacy/data/realestate.sqlite`) was
missing and no writable copy existed in `LocalFolder`, `EnsureWritableDatabaseAsync`
threw `FileNotFoundException`, making the app unusable.

## Solution

Added a fallback path in `EnsureWritableDatabaseAsync`: when the legacy source file
is not present, `CreateBlankDatabaseAsync` is called to create a new SQLite database
with the `cluster`, `house`, and `item` tables already defined.

## Changes

### `BooHillRepository.cs`
- `EnsureWritableDatabaseAsync`: replaced the `throw` when the legacy file is missing
  with a call to the new `CreateBlankDatabaseAsync` helper.
- Added `CreateBlankDatabaseAsync(string dbPath)`: creates a new SQLite database at the
  given path and executes `CREATE TABLE IF NOT EXISTS` statements for `cluster`, `house`
  (including the `tags` column), and `item`.
