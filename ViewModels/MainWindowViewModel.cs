using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CurrencyWarsTool.Infrastructure;

namespace CurrencyWarsTool.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly Dictionary<string, BondDefinition> _bondDefinitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OtherItem> _otherDefinitions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CharacterItem> CharacterItems { get; private set; } = [];
        public IReadOnlyList<EquipmentItem> EquipmentItems { get; private set; } = [];
        public IReadOnlyDictionary<string, BondDefinition> BondDefinitions => _bondDefinitions;
        public IReadOnlyDictionary<string, OtherItem> OtherDefinitions => _otherDefinitions;

        /// <summary>
        /// 读取并缓存所有数据文件。
        /// </summary>
        public void LoadData()
        {
            LoadBondDefinitions();
            LoadCharacterItems();
            LoadEquipmentItems();
            LoadOtherItems();
        }

        /// <summary>
        /// 读取羁绊定义数据。
        /// </summary>
        private void LoadBondDefinitions()
        {
            _bondDefinitions.Clear();
            var bondsPath = AppPaths.BondsJsonPath;
            var items = ReadJsonFile<List<BondDefinition>>(bondsPath) ?? [];
            foreach (var bond in items)
            {
                if (string.IsNullOrWhiteSpace(bond.name))
                {
                    continue;
                }

                _bondDefinitions[bond.name] = bond;
            }
        }

        /// <summary>
        /// 读取角色数据。
        /// </summary>
        private void LoadCharacterItems()
        {
            var characterPath = AppPaths.CharacterJsonPath;
            CharacterItems = ReadJsonFile<List<CharacterItem>>(characterPath) ?? [];
        }

        /// <summary>
        /// 读取装备数据。
        /// </summary>
        private void LoadEquipmentItems()
        {
            var equipmentPath = AppPaths.EquipmentJsonPath;
            EquipmentItems = ReadJsonFile<List<EquipmentItem>>(equipmentPath) ?? [];
        }

        /// <summary>
        /// 读取其他图标数据。
        /// </summary>
        private void LoadOtherItems()
        {
            _otherDefinitions.Clear();
            var othersPath = AppPaths.OthersJsonPath;
            var items = ReadJsonFile<List<OtherItem>>(othersPath) ?? [];
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.name))
                {
                    continue;
                }

                _otherDefinitions[item.name] = item;
            }
        }

        /// <summary>
        /// 读取 JSON 文件并处理异常。
        /// </summary>
        private static T? ReadJsonFile<T>(string path)
        {
            if (!File.Exists(path))
            {
                return default;
            }

            try
            {
                // 直接反序列化字节，兼容 UTF-8 BOM。
                var bytes = File.ReadAllBytes(path);
                return JsonSerializer.Deserialize<T>(bytes);
            }
            catch (JsonException)
            {
                return default;
            }
            catch (IOException)
            {
                return default;
            }
        }
    }

    /// <summary>
    /// 羁绊数据结构。
    /// </summary>
    public sealed class BondDefinition
    {
        public string? name { get; set; }
        public List<int>? activate { get; set; }
        public string? file { get; set; }
    }

    /// <summary>
    /// 角色数据结构。
    /// </summary>
    public sealed class CharacterItem
    {
        public string? name { get; set; }
        public int? cost { get; set; }
        public List<string>? bonds { get; set; }
        public int? position { get; set; }
        public string? file { get; set; }
    }

    /// <summary>
    /// 装备数据结构。
    /// </summary>
    public sealed class EquipmentItem
    {
        public string? name { get; set; }
        public List<string>? bonds { get; set; }
        public string? file { get; set; }
    }

    /// <summary>
    /// 其他图标数据结构。
    /// </summary>
    public sealed class OtherItem
    {
        public string? name { get; set; }
        public string? file { get; set; }
    }
}
