using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Wysg.Musm.BooHill;

internal static class BooHillParsing
{
    public static List<HouseBatchEntry> ParseHouseBatch(string raw)
    {
        var entries = new List<HouseBatchEntry>();
        var lines = Regex.Split(raw ?? string.Empty, "\r?\n");
        var count = lines.Length;

        for (var i = 0; i < count; i++)
        {
            var line = CleanLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var buildingMatch = Regex.Match(line, "^(.+?)\\s*(\\d+)동$", RegexOptions.CultureInvariant);
            if (!buildingMatch.Success)
            {
                continue;
            }

            var buildingNumber = buildingMatch.Groups[2].Value;
            string? unitNumber = null;
            double? price = null;
            string? remark = null;
            string? office = null;
            string? lastUpdated = null;

            for (var j = i + 1; j < count; j++)
            {
                var probe = CleanLine(lines[j]);
                if (string.IsNullOrWhiteSpace(probe))
                {
                    continue;
                }

                if (Regex.IsMatch(probe, "^.+?\\s*\\d+동$", RegexOptions.CultureInvariant))
                {
                    i = j - 1;
                    break;
                }

                if (price == null && Regex.IsMatch(probe, "^매매\\s+", RegexOptions.CultureInvariant))
                {
                    price = ParsePrice(probe);
                    continue;
                }

                if (unitNumber == null)
                {
                    var levelMatch = Regex.Match(probe, @"(고|중|저)\\s*/\\s*\\d+층", RegexOptions.CultureInvariant);
                    if (levelMatch.Success)
                    {
                        unitNumber = UnitNumberFromLevel(levelMatch.Groups[1].Value);
                        continue;
                    }

                    var numericMatch = Regex.Match(probe, @"(\\d+)\\s*/\\s*\\d+층", RegexOptions.CultureInvariant);
                    if (numericMatch.Success)
                    {
                        unitNumber = numericMatch.Groups[1].Value + "0X";
                        continue;
                    }
                }

                if (remark == null)
                {
                    var remarkResult = ExtractQuotedBlock(lines, j);
                    if (remarkResult.Remark != null)
                    {
                        remark = remarkResult.Remark;
                        j = Math.Max(j, remarkResult.Index);
                        continue;
                    }
                }

                if (lastUpdated == null)
                {
                    var dateMatch = Regex.Match(probe, "(\\d{4})\\.(\\d{2})\\.(\\d{2})", RegexOptions.CultureInvariant);
                    if (dateMatch.Success)
                    {
                        lastUpdated = $"{dateMatch.Groups[1].Value}-{dateMatch.Groups[2].Value}-{dateMatch.Groups[3].Value}";
                        var officeLine = (j + 1) < count ? CleanLine(lines[j + 1]) : string.Empty;
                        office = string.IsNullOrWhiteSpace(officeLine) ? null : officeLine;
                        continue;
                    }
                }
            }

            entries.Add(new HouseBatchEntry
            {
                ClusterId = 1,
                BuildingNumber = buildingNumber,
                UnitNumber = unitNumber,
                Area = "48",
                Price = price,
                Remark = remark,
                Office = office,
                LastUpdatedDate = lastUpdated,
            });
        }

        return entries;
    }

    public static List<ParsedItem> ParseItemsText(string raw)
    {
        var items = new List<ParsedItem>();
        var lines = Regex.Split(raw ?? string.Empty, "\r?\n");
        var count = lines.Length;

        for (var i = 0; i < count; i++)
        {
            var line = CleanLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!Regex.IsMatch(line, "^매매\\s+", RegexOptions.CultureInvariant))
            {
                continue;
            }

            var item = new ParsedItem
            {
                Price = ParsePrice(line),
                Office = null,
                LastUpdatedDate = null,
                Remark = null
            };

            var remarkResult = ExtractQuotedBlock(lines, i + 1);
            if (remarkResult.Remark != null)
            {
                item.Remark = remarkResult.Remark;
                i = Math.Max(i, remarkResult.Index);
            }

            for (var j = i + 1; j < count; j++)
            {
                var probe = CleanLine(lines[j]);
                if (string.IsNullOrWhiteSpace(probe))
                {
                    continue;
                }

                var dateMatch = Regex.Match(probe, "(\\d{4})\\.(\\d{2})\\.(\\d{2})", RegexOptions.CultureInvariant);
                if (dateMatch.Success)
                {
                    item.LastUpdatedDate = $"{dateMatch.Groups[1].Value}-{dateMatch.Groups[2].Value}-{dateMatch.Groups[3].Value}";
                    var officeLine = (j + 1) < count ? CleanLine(lines[j + 1]) : string.Empty;
                    item.Office = string.IsNullOrWhiteSpace(officeLine) ? null : officeLine;
                    i = j;
                    break;
                }

                if (Regex.IsMatch(probe, "^매매\\s+", RegexOptions.CultureInvariant))
                {
                    break;
                }
            }

            if (item.Price != null || !string.IsNullOrWhiteSpace(item.Office) || !string.IsNullOrWhiteSpace(item.LastUpdatedDate) || !string.IsNullOrWhiteSpace(item.Remark))
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static (string? Remark, int Index) ExtractQuotedBlock(IReadOnlyList<string> lines, int start)
    {
        var count = lines.Count;
        var collecting = false;
        var parts = new List<string>();
        var index = start;

        for (var i = start; i < count; i++)
        {
            var line = CleanLine(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!collecting)
            {
                if (line.Contains('"'))
                {
                    collecting = true;
                    line = line.Replace("\"", string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        parts.Add(line);
                    }
                    index = i;
                    if (CountQuotes(lines[i]) >= 2)
                    {
                        collecting = false;
                        break;
                    }
                    continue;
                }
                break;
            }

            if (line.Contains('"'))
            {
                line = line.Replace("\"", string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    parts.Add(line);
                }
                index = i;
                collecting = false;
                break;
            }

            parts.Add(line);
            index = i;
        }

        if (parts.Count == 0)
        {
            return (null, start);
        }

        var remark = string.Join(' ', parts);
        return (remark.Trim(), index);
    }

    private static int CountQuotes(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == '\"')
            {
                count++;
            }
        }
        return count;
    }

    private static double? ParsePrice(string line)
    {
        var match = Regex.Match(line ?? string.Empty, "^매매\\s+(.+)$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups[1].Value.Trim();
        raw = raw.Replace(",", string.Empty).Replace(" ", string.Empty);
        double total = 0;
        var hasMan = false;

        var ok = Regex.Match(raw, "([0-9]+(?:\\.[0-9]+)?)억", RegexOptions.CultureInvariant);
        if (ok.Success)
        {
            total += double.Parse(ok.Groups[1].Value, CultureInfo.InvariantCulture) * 100000000;
        }

        var man = Regex.Match(raw, "([0-9]+(?:\\.[0-9]+)?)만", RegexOptions.CultureInvariant);
        if (man.Success)
        {
            total += double.Parse(man.Groups[1].Value, CultureInfo.InvariantCulture) * 10000;
            hasMan = true;
        }

        if (!hasMan)
        {
            var trailing = Regex.Match(raw, "억([0-9]+(?:\\.[0-9]+)?)", RegexOptions.CultureInvariant);
            if (trailing.Success)
            {
                total += double.Parse(trailing.Groups[1].Value, CultureInfo.InvariantCulture) * 10000;
            }
        }

        if (total > 0)
        {
            return total;
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        return null;
    }

    private static string UnitNumberFromLevel(string level)
    {
        return level switch
        {
            "고" => "ZXX",
            "중" => "YXX",
            _ => "XXX"
        };
    }

    private static string CleanLine(string? value)
    {
        return (value ?? string.Empty).Trim().Replace("\uFEFF", string.Empty).Replace("\u200B", string.Empty).Replace("\u200C", string.Empty).Replace("\u200D", string.Empty);
    }
}

internal sealed class HouseBatchEntry
{
    public int ClusterId { get; set; }
    public string BuildingNumber { get; set; } = string.Empty;
    public string? UnitNumber { get; set; }
    public string Area { get; set; } = string.Empty;
    public double? Price { get; set; }
    public string? Remark { get; set; }
    public string? Office { get; set; }
    public string? LastUpdatedDate { get; set; }
}

internal sealed class ParsedItem
{
    public double? Price { get; set; }
    public string? Office { get; set; }
    public string? LastUpdatedDate { get; set; }
    public string? Remark { get; set; }
}
