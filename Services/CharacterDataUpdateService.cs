using HtmlAgilityPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace CurrencyWarsTool.Services;

public sealed class CharacterDataUpdateService
{
    private static readonly HttpClient HttpClient = new();
    private const string CharacterListUrl = "https://act-api-takumi-static.mihoyo.com/common/blackboard/sr_wiki/v1/home/content/list?app_sn=sr_wiki&channel_id=209";
    private const string CharacterInfoUrlTemplate = "https://act-api-takumi-static.mihoyo.com/common/blackboard/sr_wiki/v1/content/info?app_sn=sr_wiki&content_id={0}";

    public async Task UpdateAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var characterDir = Path.Combine(AppContext.BaseDirectory, "Assets", "character");
        Directory.CreateDirectory(characterDir);

        progress?.Report(new DownloadProgress(0, "正在读取角色列表..."));

        using var listResponse = await HttpClient.GetAsync(CharacterListUrl, cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        await using var listStream = await listResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var listJson = await JsonDocument.ParseAsync(listStream, cancellationToken: cancellationToken);

        var items = listJson.RootElement
            .GetProperty("data")
            .GetProperty("list")[0]
            .GetProperty("children")[0]
            .GetProperty("list");

        var characters = new List<CharacterRecord>(items.GetArrayLength());
        var total = items.GetArrayLength();
        var index = 0;

        foreach (var item in items.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            var name = item.GetProperty("title").GetString() ?? string.Empty;
            progress?.Report(new DownloadProgress(CalculatePercent(index - 1, total), $"正在处理：{name} ({index}/{total})"));

            var ext = item.GetProperty("ext").GetString() ?? "{}";
            var contentId = item.GetProperty("content_id").GetInt32();
            var iconUrl = item.GetProperty("icon").GetString() ?? string.Empty;

            var detailUrl = string.Format(CultureInfo.InvariantCulture, CharacterInfoUrlTemplate, contentId);
            using var detailResponse = await HttpClient.GetAsync(detailUrl, cancellationToken);
            detailResponse.EnsureSuccessStatusCode();
            await using var detailStream = await detailResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var detailJson = await JsonDocument.ParseAsync(detailStream, cancellationToken: cancellationToken);

            var html = detailJson.RootElement
                .GetProperty("data")
                .GetProperty("content")
                .GetProperty("contents")[0]
                .GetProperty("text")
                .GetString();

            var positionCost = ExtractPositionCost(ext);
            var character = new CharacterRecord
            {
                name = name,
                file = $"avares://CurrencyWarsTool/Assets/character/{name}.png",
                cost = positionCost.cost,
                position = positionCost.position,
                bonds = ExtractBonds(html)
            };
            characters.Add(character);

            var localFileName = Path.Combine(characterDir, $"{name}.png");
            if (!File.Exists(localFileName))
            {
                await DownloadAndResizeImageAsync(iconUrl, localFileName, cancellationToken);
            }

            progress?.Report(new DownloadProgress(CalculatePercent(index, total), $"已完成：{name} ({index}/{total})"));
        }

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "character.json");
        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(characters, jsonOptions);
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

        progress?.Report(new DownloadProgress(100, "角色数据更新完成"));
    }

    private static async Task DownloadAndResizeImageAsync(string imageUrl, string localPath, CancellationToken cancellationToken)
    {
        using var imageResponse = await HttpClient.GetAsync(imageUrl, cancellationToken);
        imageResponse.EnsureSuccessStatusCode();

        await using var networkStream = await imageResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var image = await Image.LoadAsync(networkStream, cancellationToken);
        image.Mutate(context => context.Resize(new ResizeOptions
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

    private static List<string> ExtractBonds(string? htmlText)
    {
        if (string.IsNullOrWhiteSpace(htmlText))
        {
            return [];
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(htmlText);

        var container = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'obc-tmpl')]");
        var encodedData = container?.GetAttributeValue("data-data", string.Empty);
        if (string.IsNullOrWhiteSpace(encodedData))
        {
            return [];
        }

        var decoded = WebUtility.UrlDecode(encodedData);
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return [];
        }

        using var blocksJson = JsonDocument.Parse(decoded);
        foreach (var block in blocksJson.RootElement.EnumerateArray())
        {
            if (!block.TryGetProperty("tmplKey", out var tmplKey) || tmplKey.GetString() != "material")
            {
                continue;
            }

            if (!block.TryGetProperty("data", out var data) || !data.TryGetProperty("desc", out var descElement))
            {
                return [];
            }

            var descHtml = descElement.GetString();
            if (string.IsNullOrWhiteSpace(descHtml))
            {
                return [];
            }

            var descDoc = new HtmlDocument();
            descDoc.LoadHtml(descHtml);
            var links = descDoc.DocumentNode.SelectNodes("//a");

            var bonds = new List<string>();
            if (links is null)
            {
                return bonds;
            }

            foreach (var link in links)
            {
                var text = link.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    bonds.Add(text);
                }
            }

            return bonds;
        }

        return [];
    }

    private static (int cost, int? position) ExtractPositionCost(string rawText)
    {
        using var outer = JsonDocument.Parse(rawText);
        var textField = outer.RootElement.GetProperty("c_210").GetProperty("filter").GetProperty("text").GetString() ?? "[]";
        using var itemsJson = JsonDocument.Parse(textField);

        var cost = 0;
        int? position = null;

        foreach (var item in itemsJson.RootElement.EnumerateArray())
        {
            var text = item.GetString();
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('/'))
            {
                continue;
            }

            var parts = text.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1];

            if (key == "费用" && int.TryParse(value, out var parsedCost))
            {
                cost = parsedCost;
            }
            else if (key == "站位")
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

    private sealed class CharacterRecord
    {
        public string name { get; set; } = string.Empty;
        public int cost { get; set; }
        public List<string> bonds { get; set; } = [];
        public int? position { get; set; }
        public string file { get; set; } = string.Empty;
    }
}

public sealed record DownloadProgress(int Percentage, string Message);
