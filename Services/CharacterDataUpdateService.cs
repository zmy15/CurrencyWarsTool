using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace CurrencyWarsTool.Services;

public sealed class CharacterDataUpdateService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly Regex AnchorRegex = new("<a[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    public async Task UpdateAsync(IProgress<UpdateProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        var outputDir = AppContext.BaseDirectory;
        var characterDir = Path.Combine(outputDir, "Assets", "character");
        Directory.CreateDirectory(characterDir);

        progress?.Report(new UpdateProgressInfo(0, 1, "正在读取角色列表..."));
        using var listDoc = await GetJsonAsync("https://act-api-takumi-static.mihoyo.com/common/blackboard/sr_wiki/v1/home/content/list?app_sn=sr_wiki&channel_id=209", cancellationToken);

        var items = listDoc.RootElement
            .GetProperty("data")
            .GetProperty("list")[0]
            .GetProperty("children")[0]
            .GetProperty("list")
            .EnumerateArray()
            .ToArray();

        var characters = new List<CharacterItemDto>(items.Length);
        var total = items.Length;

        for (var i = 0; i < items.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];
            var title = item.GetProperty("title").GetString() ?? string.Empty;
            var contentId = item.GetProperty("content_id").GetInt32();
            var ext = item.GetProperty("ext").GetString() ?? "{}";
            var iconUrl = item.GetProperty("icon").GetString() ?? string.Empty;

            progress?.Report(new UpdateProgressInfo(i, total, $"正在更新：{title}"));

            var (cost, position) = ExtractPositionCost(ext);
            using var detailDoc = await GetJsonAsync($"https://act-api-takumi-static.mihoyo.com/common/blackboard/sr_wiki/v1/content/info?app_sn=sr_wiki&content_id={contentId}", cancellationToken);

            var htmlText = detailDoc.RootElement
                .GetProperty("data")
                .GetProperty("content")
                .GetProperty("contents")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            var bonds = ExtractBonds(htmlText);
            var fileName = Path.Combine(characterDir, $"{title}.png");
            characters.Add(new CharacterItemDto
            {
                name = title,
                cost = cost,
                position = position,
                bonds = bonds,
                file = $"avares://CurrencyWarsTool/Assets/character/{title}.png"
            });

            if (!File.Exists(fileName) && !string.IsNullOrWhiteSpace(iconUrl))
            {
                await DownloadAndResizeImageAsync(iconUrl, fileName, cancellationToken);
            }
        }

        progress?.Report(new UpdateProgressInfo(total, total, "正在写入 character.json..."));
        var characterJsonPath = Path.Combine(outputDir, "character.json");
        await File.WriteAllTextAsync(characterJsonPath, JsonSerializer.Serialize(characters, new JsonSerializerOptions
        {
            WriteIndented = true
        }), cancellationToken);

        progress?.Report(new UpdateProgressInfo(total, total, "更新完成"));
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    private static async Task DownloadAndResizeImageAsync(string iconUrl, string outputPath, CancellationToken cancellationToken)
    {
        using var imageStream = await HttpClient.GetStreamAsync(iconUrl, cancellationToken);
        using var image = await Image.LoadAsync(imageStream, cancellationToken);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(64, 64)
        }));
        await image.SaveAsPngAsync(outputPath, cancellationToken);
    }

    private static List<string> ExtractBonds(string htmlText)
    {
        if (string.IsNullOrWhiteSpace(htmlText))
        {
            return [];
        }

        var marker = "data-data=\"";
        var markerIndex = htmlText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return [];
        }

        var start = markerIndex + marker.Length;
        var end = htmlText.IndexOf('"', start);
        if (end <= start)
        {
            return [];
        }

        var encodedJson = htmlText[start..end];
        var decodedJson = Uri.UnescapeDataString(WebUtility.HtmlDecode(encodedJson));

        using var blocksDoc = JsonDocument.Parse(decodedJson);
        foreach (var block in blocksDoc.RootElement.EnumerateArray())
        {
            if (!block.TryGetProperty("tmplKey", out var tmplKeyEl) || tmplKeyEl.GetString() != "material")
            {
                continue;
            }

            var descHtml = block.GetProperty("data").GetProperty("desc").GetString() ?? string.Empty;
            var bonds = new List<string>();
            foreach (Match match in AnchorRegex.Matches(descHtml))
            {
                var text = TagRegex.Replace(match.Groups[1].Value, string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    bonds.Add(WebUtility.HtmlDecode(text));
                }
            }

            return bonds;
        }

        return [];
    }

    private static (int cost, int? position) ExtractPositionCost(string rawText)
    {
        using var outerDoc = JsonDocument.Parse(rawText);
        var textField = outerDoc.RootElement
            .GetProperty("c_210")
            .GetProperty("filter")
            .GetProperty("text")
            .GetString() ?? "[]";

        using var itemsDoc = JsonDocument.Parse(textField);
        var cost = 0;
        int? position = null;

        foreach (var item in itemsDoc.RootElement.EnumerateArray())
        {
            var text = item.GetString();
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('/'))
            {
                continue;
            }

            var splitIndex = text.IndexOf('/');
            var key = text[..splitIndex];
            var value = text[(splitIndex + 1)..];

            if (key == "费用" && int.TryParse(value, out var parsedCost))
            {
                cost = parsedCost;
            }

            if (key == "站位")
            {
                position = value switch
                {
                    "前台" => 0,
                    "后台" => 1,
                    "前后台" => 2,
                    _ => null
                };
            }
        }

        return (cost, position);
    }

    private sealed class CharacterItemDto
    {
        public string? name { get; set; }
        public int cost { get; set; }
        public List<string> bonds { get; set; } = [];
        public int? position { get; set; }
        public string? file { get; set; }
    }
}

public readonly record struct UpdateProgressInfo(int Current, int Total, string Message)
{
    public double Percentage => Total <= 0 ? 0 : (double)Current / Total * 100;
}
