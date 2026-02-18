using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Windows.Storage;
using System.Linq;

namespace Wysg.Musm.BooHill;

public sealed class BooHillRepository
{
    private readonly string _dbPath;
    private bool _areaPatched;

    private const string TargetAreaValue = "47";

    private BooHillRepository(string dbPath)
    {
        _dbPath = dbPath;
    }

    public static async Task<BooHillRepository> CreateAsync()
    {
        var dbPath = await EnsureWritableDatabaseAsync();
        var repo = new BooHillRepository(dbPath);
        await repo.EnsureAreaPatchedAsync();
        await repo.EnsureTagsColumnAsync();
        await repo.EnsureClustersFromHousesAsync();
        return repo;
    }

    private static async Task<string> EnsureWritableDatabaseAsync()
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var targetPath = Path.Combine(localFolder.Path, "realestate.sqlite");
        if (File.Exists(targetPath))
        {
            return targetPath;
        }

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "docs", "boohill_legacy", "data", "realestate.sqlite");
        if (File.Exists(sourcePath))
        {
            Directory.CreateDirectory(localFolder.Path);
            using var source = File.OpenRead(sourcePath);
            using var target = File.Open(targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await source.CopyToAsync(target).ConfigureAwait(false);
            return targetPath;
        }

        // Fallback: create a blank database with the required schema.
        Directory.CreateDirectory(localFolder.Path);
        await CreateBlankDatabaseAsync(targetPath).ConfigureAwait(false);
        return targetPath;
    }

    private static async Task CreateBlankDatabaseAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS cluster (
                cluster_id INTEGER PRIMARY KEY,
                name       TEXT
            );

            CREATE TABLE IF NOT EXISTS house (
                house_id        INTEGER PRIMARY KEY AUTOINCREMENT,
                cluster_id      INTEGER,
                building_number TEXT,
                unit_number     TEXT,
                area            TEXT,
                is_sold         INTEGER DEFAULT 0,
                is_favorite     INTEGER DEFAULT 0,
                value           REAL,
                value_estm      REAL,
                rank            REAL,
                rank_estm       REAL,
                tags            TEXT
            );

            CREATE TABLE IF NOT EXISTS item (
                item_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                house_id          INTEGER,
                price             REAL,
                office            TEXT,
                last_updated_date TEXT,
                added_date        TEXT,
                remark            TEXT
            );
            """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClusterRecord>> GetClustersAsync()
    {
        var clusters = new List<ClusterRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT cluster_id, name FROM cluster ORDER BY cluster_id";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            clusters.Add(new ClusterRecord
            {
                ClusterId = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
            });
        }

        return clusters;
    }

    public async Task<IReadOnlyList<HouseView>> GetHousesAsync(FilterOptions filters)
    {
        var rows = new List<HouseView>();
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        var whereClauses = new List<string>();
        var command = connection.CreateCommand();

        if (filters.ClusterId.HasValue)
        {
            whereClauses.Add("h.cluster_id = $clusterId");
            command.Parameters.AddWithValue("$clusterId", filters.ClusterId.Value);
        }

        if (filters.BuildingNumbers?.Count > 0)
        {
            var paramNames = new List<string>();
            for (var i = 0; i < filters.BuildingNumbers.Count; i++)
            {
                var name = "$bn" + i.ToString(CultureInfo.InvariantCulture);
                paramNames.Add(name);
                command.Parameters.AddWithValue(name, filters.BuildingNumbers[i]);
            }
            whereClauses.Add($"h.building_number IN ({string.Join(",", paramNames)})");
        }

        if (filters.UnitNumbers?.Count > 0)
        {
            var paramNames = new List<string>();
            for (var i = 0; i < filters.UnitNumbers.Count; i++)
            {
                var name = "$un" + i.ToString(CultureInfo.InvariantCulture);
                paramNames.Add(name);
                command.Parameters.AddWithValue(name, filters.UnitNumbers[i]);
            }
            whereClauses.Add($"h.unit_number IN ({string.Join(",", paramNames)})");
        }

        if (filters.Areas?.Count > 0)
        {
            var paramNames = new List<string>();
            for (var i = 0; i < filters.Areas.Count; i++)
            {
                var name = "$ar" + i.ToString(CultureInfo.InvariantCulture);
                paramNames.Add(name);
                command.Parameters.AddWithValue(name, filters.Areas[i]);
            }
            whereClauses.Add($"h.area IN ({string.Join(",", paramNames)})");
        }

        if (filters.MinValue.HasValue)
        {
            whereClauses.Add("h.value >= $minValue");
            command.Parameters.AddWithValue("$minValue", filters.MinValue.Value);
        }

        if (filters.MaxValue.HasValue)
        {
            whereClauses.Add("h.value <= $maxValue");
            command.Parameters.AddWithValue("$maxValue", filters.MaxValue.Value);
        }

        if (filters.MinRank.HasValue)
        {
            whereClauses.Add("h.rank >= $minRank");
            command.Parameters.AddWithValue("$minRank", filters.MinRank.Value);
        }

        if (filters.MaxRank.HasValue)
        {
            whereClauses.Add("h.rank <= $maxRank");
            command.Parameters.AddWithValue("$maxRank", filters.MaxRank.Value);
        }

        if (!filters.ShowSold)
        {
            whereClauses.Add("h.is_sold = 0");
        }

        if (filters.FavoriteOnly)
        {
            whereClauses.Add("h.is_favorite = 1");
        }

        if (filters.Tags?.Count > 0)
        {
            var tagClauses = new List<string>();
            for (var i = 0; i < filters.Tags.Count; i++)
            {
                var name = "$tag" + i.ToString(CultureInfo.InvariantCulture);
                tagClauses.Add($"(',' || COALESCE(h.tags, '') || ',' LIKE '%,' || {name} || ',%')");
                command.Parameters.AddWithValue(name, filters.Tags[i]);
            }
            whereClauses.Add($"({string.Join(" OR ", tagClauses)})");
        }

        if (!string.IsNullOrEmpty(filters.RemarkText))
        {
            whereClauses.Add("EXISTS (SELECT 1 FROM item WHERE item.house_id = h.house_id AND item.remark LIKE '%' || $remarkText || '%')");
            command.Parameters.AddWithValue("$remarkText", filters.RemarkText);
        }

        var orderParts = new List<string>();
        foreach (var col in filters.SortColumns)
        {
            var dir = col.Direction == SortDirection.Descending ? "DESC" : "ASC";
            var expr = col.Field switch
            {
                SortField.Building => $"h.building_number COLLATE NOCASE {dir}",
                SortField.Unit => $"h.unit_number COLLATE NOCASE {dir}",
                SortField.Area => $"h.area COLLATE NOCASE {dir}",
                SortField.Favorite => $"h.is_favorite {dir}",
                SortField.Office => $"item_total {dir}",
                SortField.PriceRange => $"(min_price IS NULL) ASC, min_price {dir}, max_price {dir}",
                SortField.Status => $"is_new_today {dir}",
                SortField.Value => $"(h.value IS NULL) ASC, h.value {dir}",
                SortField.Rank => $"(h.rank IS NULL) ASC, h.rank {dir}",
                SortField.Sold => $"h.is_sold {dir}",
                _ => $"h.house_id {dir}"
            };
            orderParts.Add(expr);
        }

        if (orderParts.Count == 0)
        {
            orderParts.Add("h.house_id DESC");
        }

        var orderClause = string.Join(", ", orderParts);

        var sql = @"SELECT h.house_id, h.building_number, h.unit_number, h.area, h.is_favorite, h.is_sold,
    h.value, h.value_estm, h.rank, h.rank_estm, h.cluster_id,
    (SELECT MIN(price) FROM item WHERE item.house_id = h.house_id) AS min_price,
    (SELECT MAX(price) FROM item WHERE item.house_id = h.house_id) AS max_price,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id AND item.added_date = $today) AS office_count,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id) AS item_total,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id AND item.added_date = $today AND item.last_updated_date = $today) AS item_today_match,
    CASE WHEN EXISTS(SELECT 1 FROM item WHERE item.house_id = h.house_id AND item.added_date = $today) THEN 1 ELSE 0 END AS is_new_today,
    COALESCE(h.tags, '') AS tags
  FROM house h";

        if (whereClauses.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", whereClauses);
        }

        sql += " ORDER BY " + orderClause;

        command.CommandText = sql;
        command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(new HouseView
            {
                HouseId = reader.GetInt64(0),
                BuildingNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                UnitNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Area = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                IsFavorite = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                IsSold = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                Value = ReadNullableDouble(reader, 6),
                ValueEstimate = ReadNullableDouble(reader, 7),
                Rank = ReadNullableDouble(reader, 8),
                RankEstimate = ReadNullableDouble(reader, 9),
                ClusterId = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                MinPrice = ReadNullableDouble(reader, 11),
                MaxPrice = ReadNullableDouble(reader, 12),
                // Use total items for the Office column display so counts are visible even when no items were added today.
                OfficeCount = reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                ItemTotal = reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                ItemTodayMatch = reader.IsDBNull(15) ? 0 : reader.GetInt64(15),
                IsNewToday = !reader.IsDBNull(16) && reader.GetInt32(16) == 1,
                Tags = reader.IsDBNull(17) ? string.Empty : reader.GetString(17)
            });
        }

        return rows;
    }

    public async Task EnsureAreaPatchedAsync()
    {
        if (_areaPatched)
        {
            return;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE house SET area = $area WHERE area IS NULL OR area = ''";
        command.Parameters.AddWithValue("$area", TargetAreaValue);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        _areaPatched = true;
    }

    public async Task<int> UpdateHouseAreaAsync(long minHouseIdInclusive, string fromArea, string toArea)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE house SET area = $toArea WHERE house_id >= $minId AND area = $fromArea";
        command.Parameters.AddWithValue("$toArea", toArea);
        command.Parameters.AddWithValue("$minId", minHouseIdInclusive);
        command.Parameters.AddWithValue("$fromArea", fromArea);
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task ToggleFavoriteAsync(long houseId)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE house SET is_favorite = CASE WHEN is_favorite = 1 THEN 0 ELSE 1 END WHERE house_id = $id";
        command.Parameters.AddWithValue("$id", houseId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task ToggleSoldAsync(long houseId)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE house SET is_sold = CASE WHEN is_sold = 1 THEN 0 ELSE 1 END WHERE house_id = $id";
        command.Parameters.AddWithValue("$id", houseId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<long> UpsertHouseAsync(HouseEdit house)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        if (house.HouseId == 0)
        {
            command.CommandText = @"INSERT INTO house (cluster_id, building_number, unit_number, area, is_sold, is_favorite, value, value_estm, rank, rank_estm, tags)
VALUES ($cluster_id, $building_number, $unit_number, $area, $is_sold, $is_favorite, $value, $value_estm, $rank, $rank_estm, $tags);
SELECT last_insert_rowid();";
        }
        else
        {
            command.CommandText = @"UPDATE house SET
    cluster_id = $cluster_id,
    building_number = $building_number,
    unit_number = $unit_number,
    area = $area,
    is_sold = $is_sold,
    is_favorite = $is_favorite,
    value = $value,
    value_estm = $value_estm,
    rank = $rank,
    rank_estm = $rank_estm,
    tags = $tags
WHERE house_id = $house_id;
SELECT $house_id;";
            command.Parameters.AddWithValue("$house_id", house.HouseId);
        }

        command.Parameters.AddWithValue("$cluster_id", house.ClusterId);
        command.Parameters.AddWithValue("$building_number", (object?)house.BuildingNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$unit_number", string.IsNullOrWhiteSpace(house.UnitNumber) ? DBNull.Value : house.UnitNumber);
        command.Parameters.AddWithValue("$area", (object?)house.Area ?? DBNull.Value);
        command.Parameters.AddWithValue("$is_sold", house.IsSold ? 1 : 0);
        command.Parameters.AddWithValue("$is_favorite", house.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$value", house.Value.HasValue ? house.Value.Value : DBNull.Value);
        command.Parameters.AddWithValue("$value_estm", house.ValueEstimate.HasValue ? house.ValueEstimate.Value : DBNull.Value);
        command.Parameters.AddWithValue("$rank", house.Rank.HasValue ? house.Rank.Value : DBNull.Value);
        command.Parameters.AddWithValue("$rank_estm", house.RankEstimate.HasValue ? house.RankEstimate.Value : DBNull.Value);
        command.Parameters.AddWithValue("$tags", string.IsNullOrWhiteSpace(house.Tags) ? DBNull.Value : house.Tags);

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task DeleteHouseAsync(long houseId)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

        await using (var deleteItems = connection.CreateCommand())
        {
            deleteItems.CommandText = "DELETE FROM item WHERE house_id = $house_id";
            deleteItems.Parameters.AddWithValue("$house_id", houseId);
            deleteItems.Transaction = transaction;
            await deleteItems.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM house WHERE house_id = $house_id";
            command.Parameters.AddWithValue("$house_id", houseId);
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ItemRecord>> GetItemsForHouseAsync(long houseId)
    {
        var items = new List<ItemRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT item_id, house_id, price, office, last_updated_date, added_date, remark FROM item WHERE house_id = $house_id ORDER BY added_date DESC, last_updated_date DESC";
        command.Parameters.AddWithValue("$house_id", houseId);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            items.Add(new ItemRecord
            {
                ItemId = reader.GetInt64(0),
                HouseId = reader.GetInt64(1),
                Price = ReadNullableDouble(reader, 2),
                Office = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                LastUpdatedDate = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AddedDate = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Remark = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<HouseView>> GetHousesWithItemsAsync(IEnumerable<string> buildingNumbers, string? area = null)
    {
        var buildings = buildingNumbers
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (buildings.Count == 0)
        {
            return Array.Empty<HouseView>();
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        var where = new List<string>();
        var parameters = new List<(string Name, object Value)>();

        var buildingParams = new List<string>();
        for (var i = 0; i < buildings.Count; i++)
        {
            var name = "$b" + i.ToString(CultureInfo.InvariantCulture);
            buildingParams.Add(name);
            parameters.Add((name, buildings[i]));
        }

        where.Add($"h.building_number IN ({string.Join(",", buildingParams)})");

        if (!string.IsNullOrWhiteSpace(area))
        {
            where.Add("h.area = $areaFilter");
            parameters.Add(("$areaFilter", area));
        }

        var sql = @"SELECT h.house_id, h.building_number, h.unit_number, h.area, h.is_favorite, h.is_sold,
    h.value, h.value_estm, h.rank, h.rank_estm, h.cluster_id,
    (SELECT MIN(price) FROM item WHERE item.house_id = h.house_id) AS min_price,
    (SELECT MAX(price) FROM item WHERE item.house_id = h.house_id) AS max_price,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id AND item.added_date = $today) AS office_count,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id) AS item_total,
    (SELECT COUNT(*) FROM item WHERE item.house_id = h.house_id AND item.added_date = $today AND item.last_updated_date = $today) AS item_today_match,
    CASE WHEN EXISTS(SELECT 1 FROM item WHERE item.house_id = h.house_id AND item.added_date = $today) THEN 1 ELSE 0 END AS is_new_today,
    COALESCE(h.tags, '') AS tags
  FROM house h";

        if (where.Count > 0)
        {
            sql += " WHERE " + string.Join(" AND ", where);
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var houses = new List<HouseView>();
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                houses.Add(new HouseView
                {
                    HouseId = reader.GetInt64(0),
                    BuildingNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    UnitNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Area = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    IsFavorite = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                    IsSold = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                    Value = ReadNullableDouble(reader, 6),
                    ValueEstimate = ReadNullableDouble(reader, 7),
                    Rank = ReadNullableDouble(reader, 8),
                    RankEstimate = ReadNullableDouble(reader, 9),
                    ClusterId = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                    MinPrice = ReadNullableDouble(reader, 11),
                    MaxPrice = ReadNullableDouble(reader, 12),
                    OfficeCount = reader.IsDBNull(13) ? 0 : reader.GetInt64(13),
                    ItemTotal = reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                    ItemTodayMatch = reader.IsDBNull(15) ? 0 : reader.GetInt64(15),
                    IsNewToday = !reader.IsDBNull(16) && reader.GetInt32(16) == 1,
                    Tags = reader.IsDBNull(17) ? string.Empty : reader.GetString(17)
                });
            }
        }

        // Fetch items per house
        foreach (var house in houses)
        {
            var items = await GetItemsForHouseAsync(house.HouseId).ConfigureAwait(false);
            foreach (var item in items)
            {
                house.Items.Add(item);
            }
        }

        return houses;
    }

    public async Task<long> UpsertItemAsync(ItemRecord item)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        if (item.ItemId == 0)
        {
            command.CommandText = @"INSERT OR IGNORE INTO item (house_id, price, office, last_updated_date, added_date, remark)
VALUES ($house_id, $price, $office, $last_updated_date, $added_date, $remark);
SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$added_date", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        else
        {
            command.CommandText = @"UPDATE item SET
    price = $price,
    office = $office,
    last_updated_date = $last_updated_date,
    remark = $remark
WHERE item_id = $item_id AND house_id = $house_id;
SELECT $item_id;";
            command.Parameters.AddWithValue("$item_id", item.ItemId);
            command.Parameters.AddWithValue("$added_date", item.AddedDate ?? string.Empty);
        }

        command.Parameters.AddWithValue("$house_id", item.HouseId);
        command.Parameters.AddWithValue("$price", item.Price.HasValue ? item.Price.Value : DBNull.Value);
        command.Parameters.AddWithValue("$office", string.IsNullOrWhiteSpace(item.Office) ? DBNull.Value : item.Office);
        command.Parameters.AddWithValue("$last_updated_date", string.IsNullOrWhiteSpace(item.LastUpdatedDate) ? DBNull.Value : item.LastUpdatedDate);
        command.Parameters.AddWithValue("$remark", string.IsNullOrWhiteSpace(item.Remark) ? DBNull.Value : item.Remark);

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task DeleteItemAsync(long itemId)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM item WHERE item_id = $item_id";
        command.Parameters.AddWithValue("$item_id", itemId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<MassImportResult> ImportHouseBatchAsync(string rawText)
    {
        var entries = BooHillParsing.ParseHouseBatch(rawText);
        if (entries.Count == 0)
        {
            return new MassImportResult();
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

        var houseInsert = connection.CreateCommand();
        houseInsert.CommandText = @"INSERT INTO house (cluster_id, building_number, unit_number, area, is_sold, is_favorite, value, value_estm, rank, rank_estm)
VALUES ($cluster_id, $building_number, $unit_number, $area, 0, 0, NULL, NULL, NULL, NULL);";
        houseInsert.Parameters.Add("$cluster_id", SqliteType.Integer);
        houseInsert.Parameters.Add("$building_number", SqliteType.Text);
        houseInsert.Parameters.Add("$unit_number", SqliteType.Text);
        houseInsert.Parameters.Add("$area", SqliteType.Text);
        houseInsert.Transaction = transaction;

        var itemInsert = connection.CreateCommand();
        itemInsert.CommandText = @"INSERT OR IGNORE INTO item (house_id, price, office, last_updated_date, added_date, remark)
VALUES ($house_id, $price, $office, $last_updated_date, $added_date, $remark);";
        itemInsert.Parameters.Add("$house_id", SqliteType.Integer);
        itemInsert.Parameters.Add("$price", SqliteType.Real);
        itemInsert.Parameters.Add("$office", SqliteType.Text);
        itemInsert.Parameters.Add("$last_updated_date", SqliteType.Text);
        itemInsert.Parameters.Add("$added_date", SqliteType.Text);
        itemInsert.Parameters.Add("$remark", SqliteType.Text);
        itemInsert.Transaction = transaction;

        var houseMatch = connection.CreateCommand();
        houseMatch.CommandText = @"SELECT house_id FROM house WHERE area IS $area AND building_number IS $building_number AND (unit_number IS $unit_number OR (unit_number IS NULL AND $unit_number IS NULL))";
        houseMatch.Parameters.Add("$area", SqliteType.Text);
        houseMatch.Parameters.Add("$building_number", SqliteType.Text);
        houseMatch.Parameters.Add("$unit_number", SqliteType.Text);
        houseMatch.Transaction = transaction;

        var itemMatch = connection.CreateCommand();
        itemMatch.CommandText = @"SELECT 1 FROM item WHERE house_id = $house_id AND price IS $price AND office IS $office AND remark IS $remark LIMIT 1";
        itemMatch.Parameters.Add("$house_id", SqliteType.Integer);
        itemMatch.Parameters.Add("$price", SqliteType.Real);
        itemMatch.Parameters.Add("$office", SqliteType.Text);
        itemMatch.Parameters.Add("$remark", SqliteType.Text);
        itemMatch.Transaction = transaction;

        var result = new MassImportResult();
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        foreach (var entry in entries)
        {
            houseMatch.Parameters["$area"].Value = DbValue(entry.Area);
            houseMatch.Parameters["$building_number"].Value = DbValue(entry.BuildingNumber);
            houseMatch.Parameters["$unit_number"].Value = DbValue(entry.UnitNumber);

            var matches = new List<long>();
            await using (var matchReader = await houseMatch.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await matchReader.ReadAsync().ConfigureAwait(false))
                {
                    matches.Add(matchReader.GetInt64(0));
                }
            }

            long? matchedHouseId = null;
            foreach (var candidateId in matches)
            {
                itemMatch.Parameters["$house_id"].Value = candidateId;
                itemMatch.Parameters["$price"].Value = DbValue(entry.Price);
                itemMatch.Parameters["$office"].Value = DbValue(entry.Office);
                itemMatch.Parameters["$remark"].Value = DbValue(entry.Remark);

                var exists = await itemMatch.ExecuteScalarAsync().ConfigureAwait(false);
                if (exists != null && exists != DBNull.Value)
                {
                    matchedHouseId = candidateId;
                    break;
                }
            }

            if (matchedHouseId == null)
            {
                houseInsert.Parameters["$cluster_id"].Value = entry.ClusterId;
                houseInsert.Parameters["$building_number"].Value = DbValue(entry.BuildingNumber);
                houseInsert.Parameters["$unit_number"].Value = DbValue(entry.UnitNumber);
                houseInsert.Parameters["$area"].Value = DbValue(entry.Area);
                await houseInsert.ExecuteNonQueryAsync().ConfigureAwait(false);

                await using (var lastId = connection.CreateCommand())
                {
                    lastId.CommandText = "SELECT last_insert_rowid();";
                    lastId.Transaction = transaction;
                    var scalar = await lastId.ExecuteScalarAsync().ConfigureAwait(false);
                    matchedHouseId = Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
                }
                result.HousesInserted += 1;
            }

            itemInsert.Parameters["$house_id"].Value = matchedHouseId.Value;
            itemInsert.Parameters["$price"].Value = DbValue(entry.Price);
            itemInsert.Parameters["$office"].Value = DbValue(entry.Office);
            itemInsert.Parameters["$last_updated_date"].Value = DbValue(entry.LastUpdatedDate);
            itemInsert.Parameters["$added_date"].Value = today;
            itemInsert.Parameters["$remark"].Value = DbValue(entry.Remark);

            result.ItemsInserted += await itemInsert.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        return result;
    }

    public async Task<int> AddItemsAsync(long houseId, IEnumerable<BulkParsedItem> items, string today)
    {
        var existing = await GetItemsForHouseAsync(houseId).ConfigureAwait(false);
        var existingKeys = new HashSet<string>(existing.Select(i => ItemKey(i)), StringComparer.OrdinalIgnoreCase);

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

        var stmt = connection.CreateCommand();
        stmt.CommandText = @"INSERT OR IGNORE INTO item (house_id, price, office, last_updated_date, added_date, remark)
VALUES ($house_id, $price, $office, $last_updated_date, $added_date, $remark);";
        stmt.Parameters.AddWithValue("$house_id", houseId);
        stmt.Parameters.Add("$price", SqliteType.Real);
        stmt.Parameters.Add("$office", SqliteType.Text);
        stmt.Parameters.Add("$last_updated_date", SqliteType.Text);
        stmt.Parameters.Add("$added_date", SqliteType.Text);
        stmt.Parameters.Add("$remark", SqliteType.Text);
        stmt.Transaction = transaction;

        var added = 0;
        foreach (var item in items)
        {
            var key = ItemKey(item, today);
            if (existingKeys.Contains(key))
            {
                continue;
            }

            stmt.Parameters["$price"].Value = DbValue(item.Price);
            stmt.Parameters["$office"].Value = DbValue(item.Office);
            stmt.Parameters["$last_updated_date"].Value = DbValue(item.LastUpdatedDate);
            stmt.Parameters["$added_date"].Value = today;
            stmt.Parameters["$remark"].Value = DbValue(item.Remark);
            added += await stmt.ExecuteNonQueryAsync().ConfigureAwait(false);
            existingKeys.Add(key);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        return added;
    }

    public async Task<long> InsertHouseWithItemsAsync(BulkParsedHouse house, string today, int clusterId = 1)
    {
        var houseEdit = new HouseEdit
        {
            HouseId = 0,
            ClusterId = clusterId,
            BuildingNumber = house.BuildingNumber,
            UnitNumber = house.UnitNumber,
            Area = house.Area,
            IsFavorite = false,
            IsSold = false,
            Value = null,
            ValueEstimate = null,
            Rank = null,
            RankEstimate = null
        };

        var newHouseId = await UpsertHouseAsync(houseEdit).ConfigureAwait(false);
        await AddItemsAsync(newHouseId, house.Items, today).ConfigureAwait(false);
        return newHouseId;
    }

    private static string ItemKey(BulkParsedItem item, string addedDate)
    {
        var price = item.Price?.ToString("G17", CultureInfo.InvariantCulture) ?? "<null>";
        var office = item.Office?.Trim() ?? "<null>";
        var lastUpdated = item.LastUpdatedDate?.Trim() ?? "<null>";
        var added = string.IsNullOrWhiteSpace(addedDate) ? "<null>" : addedDate.Trim();
        var remark = item.Remark?.Trim() ?? "<null>";
        return $"{price}|{office}|{lastUpdated}|{added}|{remark}";
    }

    private static string ItemKey(ItemRecord item)
    {
        var price = item.Price?.ToString("G17", CultureInfo.InvariantCulture) ?? "<null>";
        var office = item.Office?.Trim() ?? "<null>";
        var lastUpdated = item.LastUpdatedDate?.Trim() ?? "<null>";
        var added = item.AddedDate?.Trim() ?? "<null>";
        var remark = item.Remark?.Trim() ?? "<null>";
        return $"{price}|{office}|{lastUpdated}|{added}|{remark}";
    }

    public async Task<int> ImportItemsAsync(long houseId, string rawText)
    {
        var items = BooHillParsing.ParseItemsText(rawText);
        if (items.Count == 0)
        {
            return 0;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

        var stmt = connection.CreateCommand();
        stmt.CommandText = @"INSERT OR IGNORE INTO item (house_id, price, office, last_updated_date, added_date, remark)
VALUES ($house_id, $price, $office, $last_updated_date, $added_date, $remark);";
        stmt.Parameters.AddWithValue("$house_id", houseId);
        stmt.Parameters.Add("$price", SqliteType.Real);
        stmt.Parameters.Add("$office", SqliteType.Text);
        stmt.Parameters.Add("$last_updated_date", SqliteType.Text);
        stmt.Parameters.Add("$added_date", SqliteType.Text);
        stmt.Parameters.Add("$remark", SqliteType.Text);
        stmt.Transaction = transaction;

        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var inserted = 0;
        foreach (var item in items)
        {
            stmt.Parameters["$price"].Value = DbValue(item.Price);
            stmt.Parameters["$office"].Value = DbValue(item.Office);
            stmt.Parameters["$last_updated_date"].Value = DbValue(item.LastUpdatedDate);
            stmt.Parameters["$added_date"].Value = today;
            stmt.Parameters["$remark"].Value = DbValue(item.Remark);
            inserted += await stmt.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        return inserted;
    }

    public async Task<List<string>> GetDistinctBuildingNumbersAsync(int? clusterId = null)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = clusterId.HasValue
            ? "SELECT DISTINCT building_number FROM house WHERE cluster_id = $cid AND building_number IS NOT NULL ORDER BY building_number COLLATE NOCASE"
            : "SELECT DISTINCT building_number FROM house WHERE building_number IS NOT NULL ORDER BY building_number COLLATE NOCASE";
        if (clusterId.HasValue)
        {
            command.Parameters.AddWithValue("$cid", clusterId.Value);
        }

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                results.Add(reader.GetString(0));
            }
        }

        return results;
    }

    public async Task<List<string>> GetDistinctUnitNumbersAsync(int? clusterId = null)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = clusterId.HasValue
            ? "SELECT DISTINCT unit_number FROM house WHERE cluster_id = $cid AND unit_number IS NOT NULL ORDER BY unit_number COLLATE NOCASE"
            : "SELECT DISTINCT unit_number FROM house WHERE unit_number IS NOT NULL ORDER BY unit_number COLLATE NOCASE";
        if (clusterId.HasValue)
        {
            command.Parameters.AddWithValue("$cid", clusterId.Value);
        }

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                results.Add(reader.GetString(0));
            }
        }

        return results;
    }

    public async Task<List<string>> GetDistinctAreasAsync(int? clusterId = null)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = clusterId.HasValue
            ? "SELECT DISTINCT area FROM house WHERE cluster_id = $cid AND area IS NOT NULL ORDER BY area COLLATE NOCASE"
            : "SELECT DISTINCT area FROM house WHERE area IS NOT NULL ORDER BY area COLLATE NOCASE";
        if (clusterId.HasValue)
        {
            command.Parameters.AddWithValue("$cid", clusterId.Value);
        }

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                results.Add(reader.GetString(0));
            }
        }

        return results;
    }

    public async Task<List<string>> GetDistinctTagsAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT tags FROM house WHERE tags IS NOT NULL AND tags != ''";

        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var raw = reader.GetString(0);
            foreach (var tag in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                tagSet.Add(tag);
            }
        }

        var results = tagSet.ToList();
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private async Task EnsureTagsColumnAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(house)";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var name = reader.GetString(1);
            if (string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE house ADD COLUMN tags TEXT";
        await alter.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task EnsureClustersFromHousesAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT OR IGNORE INTO cluster (cluster_id)
SELECT DISTINCT cluster_id FROM house WHERE cluster_id IS NOT NULL
AND cluster_id NOT IN (SELECT cluster_id FROM cluster)";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static object DbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    private static double? ReadNullableDouble(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is double d)
        {
            return d;
        }

        if (value is float f)
        {
            return f;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
