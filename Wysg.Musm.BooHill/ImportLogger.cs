using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Wysg.Musm.BooHill;

/// <summary>
/// Writes structured import log entries to daily log files (YYYYMMDD.log)
/// stored under LocalFolder/logs/.
/// </summary>
public static class ImportLogger
{
    private static readonly string LogFolder =
        Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");

    private static string LogFilePath(DateTime date) =>
        Path.Combine(LogFolder, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

    // ── Parse ────────────────────────────────────────────────

    /// <summary>Log the overall parse summary.</summary>
    public static void LogParse(int totalHouses, int totalItems, int duplicates)
    {
        Append($"PARSE | 파싱 주택: {totalHouses}, 매물: {totalItems}, 중복: {duplicates}");
    }

    /// <summary>Log each parsed new-candidate house and every item it contains.</summary>
    public static void LogParseHouse(BulkParsedHouse house)
    {
        var dir = string.IsNullOrEmpty(house.Direction) ? "" : $" {house.Direction}";
        Append($"  PARSE_HOUSE | {house.ClusterName} {house.BuildingNumber}동 {house.UnitNumber} ({house.Area}평{dir}) 매물 {house.Items.Count}개");
        foreach (var item in house.Items)
        {
            Append($"    PARSE_ITEM | {item.TransactionType} {item.PriceDisplay} | 부동산: {item.Office ?? "-"} | 날짜: {item.LastUpdatedDate ?? "-"} | 비고: {item.Remark ?? "-"}");
        }
    }

    /// <summary>Log each parsed duplicate house.</summary>
    public static void LogParseDuplicate(BulkParsedHouse dup)
    {
        var dir = string.IsNullOrEmpty(dup.Direction) ? "" : $" {dup.Direction}";
        Append($"  PARSE_DUP | {dup.ClusterName} {dup.BuildingNumber}동 {dup.UnitNumber} ({dup.Area}평{dir}) → 기존 house_id={dup.MatchedHouseId} 매물 {dup.Items.Count}개");
        foreach (var item in dup.Items)
        {
            Append($"    PARSE_DUP_ITEM | {item.TransactionType} {item.PriceDisplay} | 부동산: {item.Office ?? "-"} | 날짜: {item.LastUpdatedDate ?? "-"} | 비고: {item.Remark ?? "-"}");
        }
    }

    // ── Import duplicates ────────────────────────────────────

    /// <summary>Log overall duplicate-import summary.</summary>
    public static void LogImportDuplicates(int addedItems, int duplicateHouses)
    {
        Append($"IMPORT_DUP | 기존 주택 {duplicateHouses}개에 매물 {addedItems}개 추가");
    }

    /// <summary>Log each duplicate house whose items were imported, with item details.</summary>
    public static void LogImportDupHouse(BulkParsedHouse dup, int addedItems)
    {
        Append($"  IMPORT_DUP_HOUSE | {dup.ClusterName} {dup.BuildingNumber}동 {dup.UnitNumber} ({dup.Area}평) → house_id={dup.MatchedHouseId} 매물 {addedItems}개 추가");
        foreach (var item in dup.Items)
        {
            Append($"    IMPORT_DUP_ITEM | {item.TransactionType} {item.PriceDisplay} | 부동산: {item.Office ?? "-"} | 비고: {item.Remark ?? "-"}");
        }
    }

    // ── Merge ────────────────────────────────────────────────

    /// <summary>Log a merge operation with full item details.</summary>
    public static void LogMerge(string houseDisplay, long targetHouseId, int addedItems, IEnumerable<BulkParsedItem> items)
    {
        Append($"MERGE | \"{houseDisplay}\" → house_id={targetHouseId}, 매물 {addedItems}개 추가");
        foreach (var item in items)
        {
            Append($"  MERGE_ITEM | {item.TransactionType} {item.PriceDisplay} | 부동산: {item.Office ?? "-"} | 날짜: {item.LastUpdatedDate ?? "-"} | 비고: {item.Remark ?? "-"}");
        }
    }

    // ── Insert new ───────────────────────────────────────────

    /// <summary>Log overall new-house-insert summary.</summary>
    public static void LogInsertNew(int housesInserted, int itemsInserted)
    {
        Append($"INSERT_NEW | 새 주택 {housesInserted}개, 매물 {itemsInserted}개 추가");
    }

    /// <summary>Log each newly inserted house with its items.</summary>
    public static void LogInsertNewHouse(BulkParsedHouse house, long newHouseId, int itemsAdded)
    {
        var dir = string.IsNullOrEmpty(house.Direction) ? "" : $" {house.Direction}";
        Append($"  INSERT_NEW_HOUSE | house_id={newHouseId} {house.ClusterName} {house.BuildingNumber}동 {house.UnitNumber} ({house.Area}평{dir}) 매물 {itemsAdded}/{house.Items.Count}개");
        foreach (var item in house.Items)
        {
            Append($"    INSERT_NEW_ITEM | {item.TransactionType} {item.PriceDisplay} | 부동산: {item.Office ?? "-"} | 날짜: {item.LastUpdatedDate ?? "-"} | 비고: {item.Remark ?? "-"}");
        }
    }

    // ── Read / Parse ─────────────────────────────────────────

    /// <summary>Read all lines from a daily log file. Returns empty list if the file does not exist.</summary>
    public static async Task<List<string>> ReadLogAsync(DateTime date)
    {
        var path = LogFilePath(date);
        if (!File.Exists(path))
        {
            return new List<string>();
        }

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        return lines.ToList();
    }

    /// <summary>Returns dates that have log files, sorted descending (most recent first).</summary>
    public static List<DateTime> GetAvailableDates()
    {
        if (!Directory.Exists(LogFolder))
        {
            return new List<DateTime>();
        }

        return Directory.GetFiles(LogFolder, "*.log")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => DateTime.TryParseExact(n, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(n => DateTime.ParseExact(n, "yyyyMMdd", CultureInfo.InvariantCulture))
            .OrderByDescending(d => d)
            .ToList();
    }

    /// <summary>Parse a single log line into its components.</summary>
    public static LogEntry? ParseLine(string line)
    {
        // Format: [yyyy-MM-dd HH:mm:ss] ACTION | details
        // Indented sub-lines: [yyyy-MM-dd HH:mm:ss]   SUB_ACTION | details
        if (string.IsNullOrWhiteSpace(line) || line.Length < 22 || line[0] != '[')
        {
            return null;
        }

        var closeBracket = line.IndexOf(']');
        if (closeBracket < 0)
        {
            return null;
        }

        var timestamp = line.Substring(1, closeBracket - 1).Trim();
        var rest = line.Substring(closeBracket + 1);

        // Detect indentation depth (number of leading spaces in rest)
        var trimmed = rest.TrimStart();
        var indent = rest.Length - trimmed.Length;

        var pipeIndex = trimmed.IndexOf('|');
        if (pipeIndex < 0)
        {
            return new LogEntry { Timestamp = timestamp, Action = trimmed.Trim(), Detail = string.Empty, Indent = indent };
        }

        return new LogEntry
        {
            Timestamp = timestamp,
            Action = trimmed.Substring(0, pipeIndex).Trim(),
            Detail = trimmed.Substring(pipeIndex + 1).Trim(),
            Indent = indent
        };
    }

    private static void Append(string message)
    {
        Directory.CreateDirectory(LogFolder);
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"[{ts}] {message}{Environment.NewLine}";
        File.AppendAllText(LogFilePath(now), line);
    }
}

/// <summary>Represents one parsed log entry.</summary>
public sealed class LogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    /// <summary>Indentation depth in characters – 0 = top-level, 2 = house, 4 = item.</summary>
    public int Indent { get; set; }
}
