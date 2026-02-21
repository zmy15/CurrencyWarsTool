using CurrencyWarsTool.Infrastructure;
using HtmlAgilityPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CurrencyWarsTool.Services;

public sealed class CharacterDataUpdateService
{
    private static readonly HttpClient HttpClient = new();

    private const string BaseUrl = "https://wiki.biligame.com";
    private const string CharacterListUrl = "https://wiki.biligame.com/sr/%E8%A7%92%E8%89%B2%E4%B8%80%E8%A7%88%EF%BC%88%E8%B4%A7%E5%B8%81%EF%BC%89";

    public async Task UpdateAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var characterDir = AppPaths.CharacterAssetsDirectory;
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(AppPaths.DataDirectory);

        progress?.Report(new DownloadProgress(0, "正在读取角色列表..."));

        var html = await HttpClient.GetStringAsync(CharacterListUrl, cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var rows = document.DocumentNode.SelectNodes("//tr[contains(@class,'divsort')]");
        if (rows == null || rows.Count == 0)
        {
            await File.WriteAllTextAsync(AppPaths.CharacterJsonPath, "[]", cancellationToken);
            progress?.Report(new DownloadProgress(100, "角色数据更新完成"));
            return;
        }

        var total = rows.Count;

        var result = new List<CharacterRecord>(total);
        var downloaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = rows[i];
            progress?.Report(new DownloadProgress(CalculatePercent(i, total), $"正在处理角色 ({i + 1}/{total})"));

            var imgTag = row.SelectSingleNode(".//img");
            if (imgTag is null)
            {
                continue;
            }

            var nameRaw = imgTag.GetAttributeValue("alt", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nameRaw))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(nameRaw);
            if (!downloaded.Add(name))
            {
                continue;
            }

            var cost = ParseIntOrDefault(row.GetAttributeValue("data-param6", "0"), 0);
            var bonds = ParseBonds(row.GetAttributeValue("data-param5", string.Empty));
            var position = ParsePosition(row.GetAttributeValue("data-param2", string.Empty));

            var imgUrlRaw = imgTag.GetAttributeValue("src", string.Empty);
            if (string.IsNullOrWhiteSpace(imgUrlRaw))
            {
                continue;
            }

            var imgUrl = new Uri(new Uri(BaseUrl), imgUrlRaw).ToString();
            var localFilePath = Path.Combine(characterDir, $"{name}.png");

            if (!File.Exists(localFilePath))
            {
                try
                {
                    await DownloadAndResizeImageAsync(imgUrl, localFilePath, cancellationToken);
                }
                catch
                {
                    // 下载失败时保持与原脚本思路一致：跳过该角色
                    continue;
                }
            }

            result.Add(new CharacterRecord
            {
                name = name,
                cost = cost,
                bonds = bonds,
                position = position,
                file = $"Assets/character/{name}.png"
            });

            progress?.Report(new DownloadProgress(CalculatePercent(i + 1, total), $"已处理：{name} ({i + 1}/{total})"));
        }

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

        await File.WriteAllTextAsync(AppPaths.CharacterJsonPath, json, cancellationToken);
        progress?.Report(new DownloadProgress(100, "角色数据更新完成"));
    }

    private static int ParsePosition(string raw)
    {
        return raw.Trim() switch
        {
            "前" => 0,
            "后" => 1,
            "前后" => 2,
            _ => 0
        };
    }

    private static int ParseIntOrDefault(string raw, int defaultValue)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static List<string> ParseBonds(string raw)
    {
        return raw
            .Replace("，", ",", StringComparison.Ordinal)
            .Replace(" ", ",", StringComparison.Ordinal)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static bond => !string.IsNullOrWhiteSpace(bond))
            .ToList();
    }

    private static async Task DownloadAndResizeImageAsync(string imageUrl, string localPath, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(imageUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var image = await Image.LoadAsync(stream, cancellationToken);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(64, 64)
        }));

        await image.SaveAsPngAsync(localPath, cancellationToken);
    }

    private static int CalculatePercent(int current, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round(current * 100d / total), 0, 100);
    }

    private sealed class CharacterRecord
    {
        public string name { get; set; } = string.Empty;
        public int cost { get; set; }
        public List<string> bonds { get; set; } = [];
        public int position { get; set; }
        public string file { get; set; } = string.Empty;
    }
}

public sealed record DownloadProgress(int Percentage, string Message);
