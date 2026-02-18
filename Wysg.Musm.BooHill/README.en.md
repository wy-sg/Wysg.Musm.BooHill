# Boo Hill (부유한 언덕)

Real Estate Property Management Application (General Project)

## Overview

This application manages residential property information using .NET 8 and WinUI 3, with an SQLite database to store property and listing data.

## Key Features

### 📋 Property List Management
- **Multi-Filter Support**: Filter by building number, unit number, area, tags, favorites, and sale status
- **Sorting**: Sort by building number, price range, and more
- **Appraisal Values & Rankings**: Display appraisal values and rankings for each property
- **Price Range**: Automatic calculation of minimum/maximum listing prices

### 🏠 Property Details
- Basic property information (building, unit, area)
- Appraisal values (actual/estimated)
- Rankings (actual/estimated)
- Toggle favorite and sold status
- Tag management

### 💰 Listing Management
- Add/edit/delete listing information per property
- Store price, real estate office, dates, and remarks
- Automatic highlighting of today's new listings

### 📥 Bulk Import
- Batch input of multiple properties and listings via text format
- Duplicate detection and automatic merging
- Automatic creation of new properties

### 🎯 Cluster Management
- Group properties by clusters (complexes)
- Filter by cluster

## System Requirements

- **Operating System**: Windows 10 (Version 1809, Build 17763) or later
- **Framework**: .NET 8.0
- **Architecture**: x86, x64, ARM64

## Installation and Running

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or later (with Windows App SDK workload)

### Build Instructions

```powershell
# Clone the repository
git clone https://github.com/wy-sg/Wysg.Musm.BooHill.git
cd Wysg.Musm.BooHill

# Restore and build
dotnet restore
dotnet build

# Run
dotnet run --project Wysg.Musm.BooHill
```

### Running in Visual Studio
1. Open the `Wysg.Musm.BooHill.sln` solution file
2. Select Debug or Release build configuration
3. Choose platform (x64 recommended)
4. Press F5 to run in debug mode

## Database

The application uses a local SQLite database:
- **Location**: `%LocalAppData%\Packages\<PackageId>\LocalState\realestate.sqlite`
- **Initialization**: 
  - Automatically copies legacy database if available
  - Creates blank database if none exists

### Database Schema

#### `cluster` Table
- Property cluster (complex) information

#### `house` Table
- Basic property information (building, unit, area)
- Appraisal values and rankings
- Favorite/sold flags
- Tags

#### `item` Table
- Listing information per property
- Price, real estate office, update dates

## Project Structure

```
Wysg.Musm.BooHill/
├── Models/
│   ├── FilterOptions.cs      # Filter options model
│   └── HouseModels.cs         # Property and listing models
├── BooHillRepository.cs       # Database access layer
├── BooHillParsing.cs          # Text parsing utilities
├── MainWindow.xaml/cs         # Main window
├── AdminWindow.xaml.cs        # Admin window
├── BulkImportWindow.xaml/cs   # Bulk import window
├── Converters.cs              # UI value converters
└── docs/                      # Change logs and documentation
```

## Usage

### Search and Filter Properties
1. Select desired conditions in the top filter section
   - Building/Unit/Area: Multiple selection available
   - Tags: Search by interest-based tags
   - Favorites: Show only favorites
   - Sale Status: Show only unsold properties
2. Enter appraisal value or ranking range
3. List updates automatically

### Edit Property Information
1. Select a property from the list
2. Right-click or double-click to open detailed edit window
3. Modify information and save

### Add Listing
1. Select a property
2. Click "Add Listing" button
3. Enter price, real estate office, and remarks

### Bulk Import
1. Select "Bulk Import" menu
2. Enter text in the specified format
3. Click "Import" button

## Technology Stack

- **.NET 8.0**: Latest .NET platform
- **WinUI 3**: Modern Windows UI framework
- **Microsoft.Data.Sqlite**: SQLite database access
- **MVVM Pattern**: Data binding and UI separation

## Recent Changes

For detailed change history, refer to the change logs in the `docs/` folder:
- [2026-02-15: No-DB Fallback](docs/CHANGE_2026-02-15_NoDbFallback.md)
- [2026-02-14: Tags & Multi-Select Filters](docs/CHANGE_2026-02-14_TagsAndMultiSelectFilters.md)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

MIT License

Copyright (c) 2026 Wysg.Musm.BooHill Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Support and Contact

Submit issues and improvement suggestions via [GitHub Issues](https://github.com/wy-sg/Wysg.Musm.BooHill/issues).
