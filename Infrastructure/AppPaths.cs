using System;
using System.IO;

namespace CurrencyWarsTool.Infrastructure;

public static class AppPaths
{
    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string DataDirectory => Path.Combine(RootDirectory, "Data");
    public static string CharacterJsonPath => Path.Combine(DataDirectory, "character.json");
    public static string BondsJsonPath => Path.Combine(DataDirectory, "bonds.json");
    public static string EquipmentJsonPath => Path.Combine(DataDirectory, "equipment.json");
    public static string OthersJsonPath => Path.Combine(DataDirectory, "others.json");
    public static string CharacterAssetsDirectory => Path.Combine(RootDirectory, "Assets", "character");

    private static string ResolveRootDirectory()
    {
        // 优先从工作目录向上找项目根，方便开发时直接更新仓库内资源。
        var fromCurrent = FindDirectoryContainingFile(Directory.GetCurrentDirectory(), "CurrencyWarsTool.csproj");
        if (!string.IsNullOrWhiteSpace(fromCurrent))
        {
            return fromCurrent;
        }

        // 回退到程序目录向上查找，兼容从发布目录运行。
        var fromBase = FindDirectoryContainingFile(AppContext.BaseDirectory, "CurrencyWarsTool.csproj");
        if (!string.IsNullOrWhiteSpace(fromBase))
        {
            return fromBase;
        }

        return AppContext.BaseDirectory;
    }

    private static string? FindDirectoryContainingFile(string startDirectory, string targetFileName)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var filePath = Path.Combine(dir.FullName, targetFileName);
            if (File.Exists(filePath))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
