using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wysg.Musm.BooHill;

/// <summary>
/// Parser for bulk import text from real estate listing sites.
/// </summary>
public static class BulkImportParser
{
    // Pattern to match house header: "삼익비치타운 216동"
    private static readonly Regex HouseHeaderPattern = new(@"^(.+?)\s+(\d+)동$", RegexOptions.Compiled);
    
    // Pattern to match floor info: "1/12층" or "고/12층" or "중/12층" or "저/12층"
    private static readonly Regex FloorPattern = new(@"(\d+|고|중|저)/\d+층", RegexOptions.Compiled);

    // Pattern to match area from info lines only: "재건축47평" or "24평" but NOT "40평대"
    // Requires ㎡ or 전용 context on the same line OR the area token followed by space/paren (not 대/형 etc.)
    private static readonly Regex AreaInfoLinePattern = new(@"(?:재건축)?(\d+)(?:평|㎡)\s*\(", RegexOptions.Compiled);
    // Fallback: used only on the same line that contains FloorPattern
    private static readonly Regex AreaPattern = new(@"(?:재건축)?(\d+)평", RegexOptions.Compiled);

    // Pattern to match direction from floor info line: "중/36층남서향"
    private static readonly Regex DirectionPattern = new(@"\d+층((?:남서|남동|북서|북동|동|서|남|북)향)", RegexOptions.Compiled);
    
    // Pattern to match price: "매매 18억" or "전세 2억" (but NOT "매매 18억 ~ 20억")
    private static readonly Regex PricePattern = new(@"^(매매|전세)\s+(.+)$", RegexOptions.Compiled);
    
    // Pattern to detect multi-item house marker
    private static readonly Regex MultiItemMarker = new(@"중개사\s+\d+곳에서", RegexOptions.Compiled);
    
    // Pattern to match date: "확인매물 2026.01.20" or "집주인확인매물 2026.01.20" or "등록 2026.01.20"
    private static readonly Regex DatePattern = new(@"(?:집주인)?(?:확인매물|등록)\s+(\d{4})\.(\d{2})\.(\d{2})", RegexOptions.Compiled);

    /// <summary>
    /// Parses raw bulk text into a list of parsed houses with their items.
    /// </summary>
    public static BulkImportResult Parse(string rawText)
    {
        var result = new BulkImportResult();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return result;
        }

        // Split on CRLF, LF, or CR because clipboard text may contain only CR characters
        var lines = Regex.Split(rawText, "\r\n|\n|\r");
        var count = lines.Length;

        var logs = result.Logs;
        logs.Add($"Raw length: {rawText.Length}, LF count: {rawText.Count(c => c == '\n')}");
        logs.Add($"Total lines: {count}");
        var preview = lines.Take(5).Select((l, idx) => $"[{idx + 1}] {l}");
        logs.Add("First lines: " + string.Join(" | ", preview));

        for (var i = 0; i < count; i++)
        {
            var line = CleanLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Look for house header pattern: "삼익비치타운 XXX동"
            var houseMatch = HouseHeaderPattern.Match(line);
            if (!houseMatch.Success)
            {
                continue;
            }

            var clusterName = houseMatch.Groups[1].Value.Trim();
            var buildingNumber = houseMatch.Groups[2].Value;
            logs.Add($"House header at line {i + 1}: {clusterName} {buildingNumber}동");

            // Parse the house block
            var house = new BulkParsedHouse
            {
                ClusterName = clusterName,
                BuildingNumber = buildingNumber,
                Area = "47" // Default area
            };

            // Determine if this is a multi-item house or single-item house
            var isMultiItem = false;
            var itemsStartIndex = i + 1;
            
            // Scan ahead to find floor info, check for multi-item marker, and find where items start
            for (var j = i + 1; j < count && j < i + 15; j++)
            {
                var probe = CleanLine(lines[j]);
                if (string.IsNullOrWhiteSpace(probe))
                {
                    continue;
                }

                // Check if we hit another house header
                if (HouseHeaderPattern.IsMatch(probe))
                {
                    break;
                }

                // Parse unit number from floor info and direction
                if (string.IsNullOrEmpty(house.UnitNumber))
                {
                    var floorMatch = FloorPattern.Match(probe);
                    if (floorMatch.Success)
                    {
                        house.UnitNumber = ParseUnitNumber(floorMatch.Groups[1].Value);
                        logs.Add($"  Floor at line {j + 1}: {probe} -> unit {house.UnitNumber}");

                        // Parse area from the same info line that has floor info (most reliable)
                        var areaInfoMatch = AreaInfoLinePattern.Match(probe);
                        if (areaInfoMatch.Success)
                        {
                            house.Area = areaInfoMatch.Groups[1].Value;
                            logs.Add($"  Area at line {j + 1}: {house.Area}평 (info-line)");
                        }
                        else
                        {
                            var areaMatch = AreaPattern.Match(probe);
                            if (areaMatch.Success)
                            {
                                house.Area = areaMatch.Groups[1].Value;
                                logs.Add($"  Area at line {j + 1}: {house.Area}평 (fallback)");
                            }
                        }

                        // Parse direction from the same floor info line
                        var dirMatch = DirectionPattern.Match(probe);
                        if (dirMatch.Success)
                        {
                            house.Direction = dirMatch.Groups[1].Value;
                            logs.Add($"  Direction at line {j + 1}: {house.Direction}");
                        }
                    }
                }

                // Check for multi-item marker
                if (MultiItemMarker.IsMatch(probe))
                {
                    isMultiItem = true;
                    logs.Add($"  Multi-item marker at line {j + 1}");
                }

                // Find "매물목록 접기" which marks where items start for multi-item houses
                if (probe.Contains("매물목록 접기"))
                {
                    itemsStartIndex = j + 1;
                    logs.Add($"  Items start after line {j + 1}");
                    break;
                }
            }

            // Parse items
            var items = ParseItems(lines, itemsStartIndex, count, isMultiItem, logs, out var endIndex);
            i = endIndex;

            if (items.Count > 0)
            {
                house.Items = DeduplicateItems(items, logs);
                house.Key = BuildHouseKey(clusterName, buildingNumber, house.UnitNumber, house.Area);
                result.Houses.Add(house);
                logs.Add($"  Added house with {house.Items.Count} items (end at line {endIndex + 1})");
            }
            else
            {
                logs.Add($"  Skipped house (no items found, end at line {endIndex + 1})");
            }
        }

        return result;
    }

    private static List<BulkParsedItem> ParseItems(string[] lines, int startIndex, int count, bool isMultiItem, List<string> logs, out int endIndex)
    {
        var items = new List<BulkParsedItem>();
        BulkParsedItem? currentItem = null;
        endIndex = startIndex;
        var foundFirstPrice = false;

        for (var j = startIndex; j < count; j++)
        {
            var probe = CleanLine(lines[j]);
            if (string.IsNullOrWhiteSpace(probe))
            {
                continue;
            }

            // Check if we hit another house header
            if (HouseHeaderPattern.IsMatch(probe))
            {
                endIndex = j - 1;
                break;
            }

            // Check for price line (start of new item)
            var priceMatch = PricePattern.Match(probe);
            if (priceMatch.Success)
            {
                var priceText = priceMatch.Groups[2].Value;
                
                // Skip summary price lines that contain range (e.g., "18억 ~ 20억")
                // But only for multi-item houses and only for the first price we encounter
                if (!foundFirstPrice && isMultiItem && priceText.Contains('~'))
                {
                    logs.Add($"    Skip summary price at line {j + 1}: {priceText}");
                    foundFirstPrice = true;
                    continue;
                }
                
                foundFirstPrice = true;

                // Save previous item if exists and has required data
                if (currentItem != null && (currentItem.Price.HasValue || !string.IsNullOrEmpty(currentItem.Office)))
                {
                    items.Add(currentItem);
                    logs.Add($"    Saved item (price={currentItem.Price}, office={currentItem.Office}) at line {j + 1}");
                }

                currentItem = new BulkParsedItem
                {
                    TransactionType = priceMatch.Groups[1].Value,
                    Price = ParsePrice(priceText)
                };
                logs.Add($"    New item at line {j + 1}: {priceMatch.Groups[1].Value} {priceText} -> {currentItem.Price}");
                continue;
            }

            // If we have a current item, try to parse its details
            if (currentItem != null)
            {
                // Parse remark from quoted text
                if (string.IsNullOrEmpty(currentItem.Remark) && probe.Contains('"'))
                {
                    var remarkResult = ExtractQuotedBlock(lines, j);
                    if (!string.IsNullOrEmpty(remarkResult.Remark))
                    {
                        currentItem.Remark = remarkResult.Remark;
                        j = remarkResult.EndIndex;
                        logs.Add($"    Remark captured through line {j + 1}");
                        continue;
                    }
                }

                // Parse date
                var dateMatch = DatePattern.Match(probe);
                if (dateMatch.Success)
                {
                    currentItem.LastUpdatedDate = $"{dateMatch.Groups[1].Value}-{dateMatch.Groups[2].Value}-{dateMatch.Groups[3].Value}";
                    logs.Add($"    Date at line {j + 1}: {currentItem.LastUpdatedDate}");

                    // Next non-empty, non-noise line after date is usually the office
                    for (var k = j + 1; k < count && k <= j + 4; k++)
                    {
                        var officeLine = CleanLine(lines[k]);
                        if (string.IsNullOrWhiteSpace(officeLine))
                        {
                            continue;
                        }

                        // Skip known non-office patterns
                        if (IsNonOfficeLine(officeLine))
                        {
                            continue;
                        }

                        // Check if this is another house header
                        if (HouseHeaderPattern.IsMatch(officeLine))
                        {
                            break;
                        }

                        currentItem.Office = officeLine;
                        logs.Add($"    Office at line {k + 1}: {officeLine}");
                        break;
                    }
                    continue;
                }
            }

            endIndex = j;
        }

        // Add last item if exists
        if (currentItem != null && (currentItem.Price.HasValue || !string.IsNullOrEmpty(currentItem.Office)))
        {
            items.Add(currentItem);
            logs.Add("    Saved final item");
        }

        return items;
    }

    private static string BuildHouseKey(string clusterName, string buildingNumber, string? unitNumber, string area)
    {
        var unit = NormalizeUnit(unitNumber);
        return $"{clusterName.Trim()}|{buildingNumber.Trim()}|{unit ?? "<null>"}|{area.Trim()}";
    }

    private static string? NormalizeUnit(string? unitNumber)
    {
        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            return null;
        }

        return unitNumber.Trim();
    }

    private static List<BulkParsedItem> DeduplicateItems(IEnumerable<BulkParsedItem> items, List<string> logs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<BulkParsedItem>();

        foreach (var item in items)
        {
            var key = ItemKey(item);
            if (seen.Add(key))
            {
                unique.Add(item);
            }
            else
            {
                logs.Add($"    Duplicate item skipped (price={item.Price}, office={item.Office}, remark={item.Remark})");
            }
        }

        return unique;
    }

    private static int MergeItems(List<BulkParsedItem> target, IEnumerable<BulkParsedItem> incoming, List<string> logs)
    {
        var added = 0;
        var existingKeys = new HashSet<string>(target.Select(ItemKey), StringComparer.OrdinalIgnoreCase);

        foreach (var item in incoming)
        {
            var key = ItemKey(item);
            if (existingKeys.Contains(key))
            {
                logs.Add($"    Duplicate item skipped during merge (price={item.Price}, office={item.Office}, remark={item.Remark})");
                continue;
            }

            target.Add(item);
            existingKeys.Add(key);
            added++;
        }

        return added;
    }

    private static string ItemKey(BulkParsedItem item)
    {
        var price = item.Price?.ToString("G17", CultureInfo.InvariantCulture) ?? "<null>";
        var office = item.Office?.Trim() ?? "<null>";
        var remark = item.Remark?.Trim() ?? "<null>";
        return $"{price}|{office}|{remark}";
    }

    private static string ParseUnitNumber(string floorPart)
    {
        return floorPart switch
        {
            "고" => "ZXX",
            "중" => "YXX",
            "저" => "XXX",
            _ when int.TryParse(floorPart, out var floor) => $"{floor}0X",
            _ => "XXX"
        };
    }

    private static double? ParsePrice(string priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        // Handle range prices like "17억 ~ 17억 5,000" - take first price
        var rangeParts = priceText.Split('~');
        var raw = rangeParts[0].Trim();

        // Remove common suffixes like "변동상승내역 보기" or "변동하락내역 보기"
        raw = Regex.Replace(raw, @"변동.+$", string.Empty);

        raw = raw.Replace(",", string.Empty).Replace(" ", string.Empty);
        double total = 0;
        var hasMan = false;

        // Parse billions (억)
        var okMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)억", RegexOptions.CultureInvariant);
        if (okMatch.Success)
        {
            total += double.Parse(okMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 100000000;
        }

        // Parse ten-thousands (만)
        var manMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)만", RegexOptions.CultureInvariant);
        if (manMatch.Success)
        {
            total += double.Parse(manMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 10000;
            hasMan = true;
        }

        // Handle trailing numbers after 억 without 만 (e.g., "17억 5,000" means 17억 5000만)
        if (!hasMan)
        {
            var trailing = Regex.Match(raw, @"억(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
            if (trailing.Success)
            {
                total += double.Parse(trailing.Groups[1].Value, CultureInfo.InvariantCulture) * 10000;
            }
        }

        return total > 0 ? total : null;
    }

    private static (string? Remark, int EndIndex) ExtractQuotedBlock(string[] lines, int start)
    {
        var count = lines.Length;
        var parts = new List<string>();
        var endIndex = start;
        var inQuote = false;

        for (var i = start; i < count; i++)
        {
            var line = CleanLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!inQuote)
            {
                if (line.Contains('"'))
                {
                    inQuote = true;
                    var cleaned = line.Replace("\"", string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        parts.Add(cleaned);
                    }
                    endIndex = i;

                    // Check if quote ends on same line
                    if (CountQuotes(lines[i]) >= 2)
                    {
                        break;
                    }
                    continue;
                }
                break;
            }

            if (line.Contains('"'))
            {
                var cleaned = line.Replace("\"", string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    parts.Add(cleaned);
                }
                endIndex = i;
                break;
            }

            parts.Add(line);
            endIndex = i;
        }

        if (parts.Count == 0)
        {
            return (null, start);
        }

        return (string.Join(' ', parts).Trim(), endIndex);
    }

    private static int CountQuotes(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == '"')
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsNonOfficeLine(string line)
    {
        return line.StartsWith("매물", StringComparison.Ordinal)
            || line.StartsWith("관심매물", StringComparison.Ordinal)
            || line.StartsWith("중개사", StringComparison.Ordinal)
            || line.StartsWith("매물목록", StringComparison.Ordinal)
            || line.StartsWith("이미지", StringComparison.Ordinal)
            || line.Contains("이미지")
            || Regex.IsMatch(line, @"^\d+/\d+층", RegexOptions.CultureInvariant)
            || HouseHeaderPattern.IsMatch(line);
    }

    private static string CleanLine(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("\uFEFF", string.Empty)
            .Replace("\u200B", string.Empty)
            .Replace("\u200C", string.Empty)
            .Replace("\u200D", string.Empty);
    }
}

/// <summary>
/// Result of bulk import parsing.
/// </summary>
public sealed class BulkImportResult
{
    public List<BulkParsedHouse> Houses { get; set; } = new();
    public List<BulkParsedHouse> Duplicates { get; set; } = new();
    public List<string> Logs { get; set; } = new();

    public int TotalHouses => Houses.Count;
    public int TotalItems => Houses.Sum(h => h.Items.Count);
}

/// <summary>
/// A parsed house from bulk import.
/// </summary>
public sealed class BulkParsedHouse
{
    public string ClusterName { get; set; } = string.Empty;
    public string BuildingNumber { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Area { get; set; } = "47";
    public string Direction { get; set; } = string.Empty;
    public List<BulkParsedItem> Items { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public string DuplicateReason { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long? MatchedHouseId { get; set; }

    public string Display
    {
        get
        {
            var dir = string.IsNullOrEmpty(Direction) ? string.Empty : $" {Direction}";
            var baseText = $"{(IsDuplicate ? "[DUP] " : string.Empty)}{ClusterName} {BuildingNumber}동 {UnitNumber} ({Area}평{dir}) - {Items.Count} items";
            if (IsDuplicate && MatchedHouseId.HasValue)
            {
                return $"{baseText} -> same as id={MatchedHouseId.Value}";
            }

            return baseText;
        }
    }

    public override string ToString()
    {
        return Display;
    }
}

/// <summary>
/// A parsed item from bulk import.
/// </summary>
public sealed class BulkParsedItem
{
    public string TransactionType { get; set; } = "매매";
    public double? Price { get; set; }
    public string? Office { get; set; }
    public string? LastUpdatedDate { get; set; }
    public string? Remark { get; set; }

    public string PriceDisplay => Price.HasValue
        ? (Price.Value / 100000000).ToString("0.####", CultureInfo.InvariantCulture) + "억"
        : string.Empty;

    public string Display => $"{TransactionType} {PriceDisplay} | {Office ?? "-"} | {LastUpdatedDate ?? "-"} | {Remark ?? "-"}";
}
