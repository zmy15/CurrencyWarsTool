using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurrencyWarsTool.ViewModels;
using CurrencyWarsTool.Infrastructure;

namespace CurrencyWarsTool.Views
{
    public partial class MainWindow : Window
    {
        // 拆装工具图标路径
        private const string ClearEquipmentToolName = "精密拆装扳手";
        private const string FrontIconName = "前台";
        private const string BackIconName = "后台";
        private const string FrontBackIconName = "前后台";
        // 底部面板容器
        private readonly StackPanel? _imagePanel;
        // 左侧羁绊统计容器
        private readonly StackPanel? _bondsPanel;
        // 右侧装备容器
        private readonly UniformGrid? _equipmentPanel;
        // 棋盘数量统计
        private readonly TextBlock? _boardCount;
        // 棋盘格子缓存
        private readonly List<Border> _boardBorders = new();
        // cost 分行容器缓存
        private readonly Dictionary<int, WrapPanel> _costRows = new();
        // 视图模型
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _imagePanel = this.FindControl<StackPanel>("ImagePanel");
            _bondsPanel = this.FindControl<StackPanel>("BondsPanel");
            _boardCount = this.FindControl<TextBlock>("BoardCount");
            _equipmentPanel = this.FindControl<UniformGrid>("EquipmentPanel");
            _viewModel = DataContext as MainWindowViewModel ?? new MainWindowViewModel();
            DataContext ??= _viewModel;
            InitializeBoardBorders();
            // 读取数据
            _viewModel.LoadData();
            LoadPanelImages();
            LoadEquipmentIcons();
            UpdateBondsSummary();
            UpdateBoardCount();
        }

        private void LoadEquipmentIcons()
        {
            if (_equipmentPanel is null)
            {
                return;
            }

            // 根据数据生成装备图标
            _equipmentPanel.Children.Clear();
            foreach (var item in _viewModel.EquipmentItems)
            {
                // 每个装备图标绑定可拖拽的元数据
                var metadata = new EquipmentMetadata(item.bonds ?? [], item.file);
                var image = new Image
                {
                    Source = LoadImageSource(item.file),
                    Width = 44,
                    Height = 44,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4),
                    Tag = metadata
                };

                image.PointerPressed += OnEquipmentDragStart;

                var container = new Border
                {
                    Width = 52,
                    Height = 52,
                    Margin = new Thickness(4),
                    Child = image,
                    Tag = metadata
                };

                container.PointerPressed += OnEquipmentDragStart;
                _equipmentPanel.Children.Add(container);
            }

            AddClearEquipmentTool();
        }

        private void AddClearEquipmentTool()
        {
            if (_equipmentPanel is null)
            {
                return;
            }

            // 追加拆装工具
            var clearToolFile = GetOtherFile(ClearEquipmentToolName);
            if (string.IsNullOrWhiteSpace(clearToolFile))
            {
                return;
            }

            var metadata = new EquipmentMetadata([], clearToolFile, isClearTool: true);
            var image = new Image
            {
                Source = LoadImageSource(clearToolFile),
                Width = 44,
                Height = 44,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2),
                Tag = metadata
            };

            image.PointerPressed += OnEquipmentDragStart;

            var container = new Border
            {
                Width = 52,
                Height = 52,
                Margin = new Thickness(2),
                Child = image,
                Tag = metadata
            };

            container.PointerPressed += OnEquipmentDragStart;
            _equipmentPanel.Children.Add(container);
        }

        private async void OnEquipmentDragStart(object? sender, PointerPressedEventArgs e)
        {
            // 装备拖拽起始
            var metadata = sender switch
            {
                Image image => image.Tag as EquipmentMetadata,
                Border border => border.Tag as EquipmentMetadata,
                _ => null
            };

            // 普通装备需要有羁绊，拆装工具允许无羁绊
            if (metadata is null || (metadata.Bonds.Count == 0 && !metadata.IsClearTool))
            {
                return;
            }

            var data = new DataObject();
            // 传递装备羁绊与图标路径，供放置时处理
            data.Set("equipment-bonds", metadata.Bonds);
            data.Set("equipment-file", metadata.File);
            data.Set("equipment-clear", metadata.IsClearTool);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
            e.Handled = true;
        }

        private async void OnDragStart(object? sender, PointerPressedEventArgs e)
        {
            // 角色拖拽起始
            var image = sender as Image;
            var panelItem = sender as Border;
            if (image is null && panelItem?.Child is Image childImage)
            {
                image = childImage;
            }

            if (image?.Source is null)
            {
                return;
            }

            var data = new DataObject();
            // 传递角色基础数据、装备数据与位置
            data.Set("image-source", image.Source);
            data.Set("dragged-image", image);
            data.Set("image-position", GetPositionFromTag(image.Tag));
            data.Set("image-cost", GetCostFromTag(image.Tag));
            data.Set("image-base-bonds", GetBaseBondsFromTag(image.Tag));
            data.Set("image-equipment-bonds", GetEquipmentBondsFromTag(image.Tag));
            data.Set("image-equipment", GetEquipmentFilesFromTag(image.Tag));

            if (panelItem?.Tag is ImageMetadata)
            {
                if (panelItem.Parent is Panel panelFromItem)
                {
                    data.Set("source-panel", panelFromItem);
                    data.Set("source-panel-item", panelItem);
                }
                else
                {
                    return;
                }
            }
            else if (image.Parent is Border parentBorder && parentBorder.Tag is ImageMetadata)
            {
                if (parentBorder.Parent is Panel panelFromItem)
                {
                    data.Set("source-panel", panelFromItem);
                    data.Set("source-panel-item", parentBorder);
                }
                else
                {
                    return;
                }
            }
            else if (image.Parent is Panel contentPanel && contentPanel.Parent is Border contentBorder && contentBorder.Tag is ImageMetadata)
            {
                if (contentBorder.Parent is Panel panelFromItem)
                {
                    data.Set("source-panel", panelFromItem);
                    data.Set("source-panel-item", contentBorder);
                }
                else
                {
                    return;
                }
            }
            else if (image.Parent is Grid grid && grid.Parent is StackPanel contentPanelInner && contentPanelInner.Parent is Border contentBorderInner)
            {
                if (contentBorderInner.Tag is ImageMetadata)
                {
                    if (contentBorderInner.Parent is Panel panelFromItem)
                    {
                        data.Set("source-panel", panelFromItem);
                        data.Set("source-panel-item", contentBorderInner);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    data.Set("source-border", contentBorderInner);
                }
            }
            else if (image.Parent is Panel boardPanel && boardPanel.Parent is Border boardBorder)
            {
                data.Set("source-border", boardBorder);
            }
            else if (image.Parent is Border border)
            {
                data.Set("source-border", border);
            }
            else
            {
                return;
            }
            var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            if (result == DragDropEffects.None)
            {
                ReturnToPanelIfNeeded(data);
            }
            e.Handled = true;
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            // 拖拽悬停处理
            if (e.Data.Contains("equipment-bonds"))
            {
                e.DragEffects = sender is Border border && GetBoardImage(border) is not null
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
            else
            {
                e.DragEffects = e.Data.Contains("image-source") ? DragDropEffects.Move : DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void ReturnToPanelIfNeeded(DataObject data)
        {
            // 拖拽取消时回收到下方区域
            if (data.Get("source-border") is not Border sourceBorder)
            {
                return;
            }

            var sourceImage = GetBoardImage(sourceBorder);
            if (sourceImage?.Source is null)
            {
                return;
            }

            // 清空棋盘格子并恢复角色到下方行
            sourceBorder.Child = null;
            sourceBorder.Background = Brushes.Transparent;
            var position = GetPositionFromTag(sourceImage.Tag);
            var cost = GetCostFromTag(sourceImage.Tag);
            var baseBonds = GetBaseBondsFromTag(sourceImage.Tag);
            var equipmentBonds = GetEquipmentBondsFromTag(sourceImage.Tag);
            var equipmentFiles = GetEquipmentFilesFromTag(sourceImage.Tag);
            AddToCostRow(cost, CreatePanelItem(sourceImage.Source, position, cost, baseBonds, equipmentBonds, equipmentFiles));
            UpdateBondsSummary();
            UpdateBoardCount();
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            // 拖拽落点处理
            if (e.Data.Contains("equipment-bonds") && sender is Border border && GetBoardImage(border) is Image targetImage)
            {
                // 装备拖拽落在角色上
                if (e.Data.Get("equipment-bonds") is IReadOnlyList<string> bonds)
                {
                    var equipmentFile = e.Data.Get("equipment-file") as string;
                    var isClearTool = e.Data.Get("equipment-clear") as bool? == true;
                    AddEquipmentBonds(border, targetImage, bonds, equipmentFile, isClearTool);
                    UpdateBondsSummary();
                }

                e.Handled = true;
                return;
            }

            if (e.Data.Get("image-source") is not IImage imageSource)
            {
                return;
            }

            if (sender is Border panelItemBorder && panelItemBorder.Tag is ImageMetadata && panelItemBorder.Parent is Panel panelParent)
            {
                // 下方面板上的角色也允许处理拖放逻辑
                HandleDropOnPanel(panelParent, imageSource, e);
                e.Handled = true;
                return;
            }

            switch (sender)
            {
                case Border boardBorder:
                    HandleDropOnBorder(boardBorder, imageSource, e);
                    break;
                case Panel panel:
                    HandleDropOnPanel(panel, imageSource, e);
                    break;
            }

            e.Handled = true;
        }

        private void HandleDropOnBorder(Border targetBorder, IImage imageSource, DragEventArgs e)
        {
            // 放置到棋盘格子
            var sourceBorder = e.Data.Get("source-border") as Border;
            if (sourceBorder == targetBorder)
            {
                return;
            }

            var sourcePanel = e.Data.Get("source-panel") as Panel;
            var sourcePanelItem = e.Data.Get("source-panel-item") as Control;
            var draggedImage = e.Data.Get("dragged-image") as Image;
            var targetImage = GetBoardImage(targetBorder);
            var swappedToSource = false;

            if (targetImage?.Source is not null)
            {
                // 目标格子有角色时执行交换逻辑
                if (sourceBorder is not null)
                {
                    var targetPosition = GetPositionFromTag(targetImage.Tag);
                    var targetCost = GetCostFromTag(targetImage.Tag);
                    var targetBaseBonds = GetBaseBondsFromTag(targetImage.Tag);
                    var targetEquipmentBonds = GetEquipmentBondsFromTag(targetImage.Tag);
                    var targetEquipment = GetEquipmentFilesFromTag(targetImage.Tag);
                    sourceBorder.Child = CreateBoardContent(targetImage.Source, targetPosition, targetCost, targetBaseBonds, targetEquipmentBonds, targetEquipment);
                    UpdateBorderForPlacement(sourceBorder, targetPosition, targetCost);
                    swappedToSource = true;
                }
                else if (sourcePanel is not null)
                {
                    // 从下方面板拖到棋盘，目标角色回到下方
                    var targetPosition = GetPositionFromTag(targetImage.Tag);
                    var targetCost = GetCostFromTag(targetImage.Tag);
                    var targetBaseBonds = GetBaseBondsFromTag(targetImage.Tag);
                    var targetEquipmentBonds = GetEquipmentBondsFromTag(targetImage.Tag);
                    var targetEquipment = GetEquipmentFilesFromTag(targetImage.Tag);
                    AddToCostRow(targetCost, CreatePanelItem(targetImage.Source, targetPosition, targetCost, targetBaseBonds, targetEquipmentBonds, targetEquipment));
                }
            }

            if (draggedImage is not null)
            {
                // 移除拖拽来源的原始角色
                if (sourcePanel is not null)
                {
                    if (sourcePanelItem is not null)
                    {
                        sourcePanel.Children.Remove(sourcePanelItem);
                    }
                    else
                    {
                        sourcePanel.Children.Remove(draggedImage);
                    }
                }
                else if (sourceBorder is not null && !swappedToSource)
                {
                    // 从棋盘拖到棋盘时清空原格子
                    sourceBorder.Child = null;
                    sourceBorder.Background = Brushes.Transparent;
                }
            }

            var position = GetPositionFromTag(e.Data.Get("image-position"));
            var cost = GetCostFromTag(e.Data.Get("image-cost"));
            var baseBonds = GetBaseBondsFromTag(e.Data.Get("image-base-bonds"));
            var equipmentBonds = GetEquipmentBondsFromTag(e.Data.Get("image-equipment-bonds"));
            var equipmentFiles = GetEquipmentFilesFromTag(e.Data.Get("image-equipment"));
            targetBorder.Child = CreateBoardContent(imageSource, position, cost, baseBonds, equipmentBonds, equipmentFiles);
            UpdateBorderForPlacement(targetBorder, position, cost);
            UpdateBondsSummary();
            UpdateBoardCount();
        }

        private void HandleDropOnPanel(Panel targetPanel, IImage imageSource, DragEventArgs e)
        {
            // 放置到下方行
            var sourceBorder = e.Data.Get("source-border") as Border;
            var sourcePanel = e.Data.Get("source-panel") as Panel;
            var sourcePanelItem = e.Data.Get("source-panel-item") as Control;
            var draggedImage = e.Data.Get("dragged-image") as Image;

            if (sourceBorder is null && sourcePanel is not null && targetPanel.Tag is not null)
            {
                // 限制同 cost 行内移动
                var sourceCost = GetCostFromTag(sourcePanelItem?.Tag ?? draggedImage?.Tag);
                var targetCost = GetCostFromTag(targetPanel.Tag);
                if (sourceCost is not null && targetCost is not null && sourceCost != targetCost)
                {
                    return;
                }
            }

            if (draggedImage is not null)
            {
                if (sourcePanel is not null)
                {
                    if (sourcePanelItem is not null)
                    {
                        sourcePanel.Children.Remove(sourcePanelItem);
                    }
                    else
                    {
                        sourcePanel.Children.Remove(draggedImage);
                    }
                }
                else if (sourceBorder is not null)
                {
                    sourceBorder.Child = null;
                    sourceBorder.Background = Brushes.Transparent;
                }
            }

            var position = GetPositionFromTag(e.Data.Get("image-position"));
            var cost = GetCostFromTag(e.Data.Get("image-cost"));
            var baseBonds = GetBaseBondsFromTag(e.Data.Get("image-base-bonds"));
            var equipmentBonds = GetEquipmentBondsFromTag(e.Data.Get("image-equipment-bonds"));
            var equipmentFiles = GetEquipmentFilesFromTag(e.Data.Get("image-equipment"));
            AddToCostRow(cost, CreatePanelItem(imageSource, position, cost, baseBonds, equipmentBonds, equipmentFiles));
            UpdateBondsSummary();
            UpdateBoardCount();
        }

        private Control CreateBoardContent(IImage source, int? position, int? cost, IReadOnlyList<string>? baseBonds, IReadOnlyList<string>? equipmentBonds, IReadOnlyList<string>? equipmentFiles)
        {
            // 生成棋盘中的角色内容
            var image = CreateCharacterImage(source, position, cost, baseBonds, equipmentBonds, equipmentFiles);
            var imageContainer = CreateCharacterImageContainer(image, position);
            var equipmentPanel = new WrapPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            if (equipmentFiles is not null)
            {
                foreach (var file in equipmentFiles.Take(3))
                {
                    equipmentPanel.Children.Add(CreateEquipmentBadge(file));
                }
            }

            var container = new StackPanel
            {
                Spacing = 2,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            container.Children.Add(imageContainer);
            container.Children.Add(equipmentPanel);
            return container;
        }

        private void UpdateBoardCount()
        {
            // 更新棋盘数量统计
            if (_boardCount is null)
            {
                return;
            }

            var count = _boardBorders.Count(border => GetBoardImage(border) is not null);
            _boardCount.Text = $"{count}/13";
        }

        private Border CreatePanelItem(IImage source, int? position = null, int? cost = null, IReadOnlyList<string>? baseBonds = null, IReadOnlyList<string>? equipmentBonds = null, IReadOnlyList<string>? equipmentFiles = null)
        {
            // 生成下方面板中的角色内容
            var image = CreateCharacterImage(source, position, cost, baseBonds, equipmentBonds, equipmentFiles);
            var imageContainer = CreateCharacterImageContainer(image, position);

            var equipmentPanel = new WrapPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            if (equipmentFiles is not null)
            {
                foreach (var file in equipmentFiles.Take(3))
                {
                    equipmentPanel.Children.Add(CreateEquipmentBadge(file));
                }
            }

            var contentPanel = new StackPanel
            {
                Spacing = 2,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            contentPanel.Children.Add(imageContainer);
            contentPanel.Children.Add(equipmentPanel);

            var panelItem = new Border
            {
                Width = 60,
                Height = 60,
                Background = GetBrushForCost(cost),
                Margin = new Thickness(0, 0, 12, 0),
                Child = contentPanel,
                Tag = new ImageMetadata(position, cost, baseBonds, equipmentBonds, equipmentFiles)
            };

            panelItem.SetValue(DragDrop.AllowDropProperty, true);
            panelItem.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            panelItem.AddHandler(DragDrop.DropEvent, OnDrop);
            panelItem.PointerPressed += OnDragStart;
            return panelItem;
        }

        private void LoadPanelImages()
        {
            // 读取角色并填充面板
            if (_imagePanel is null)
            {
                return;
            }

            _imagePanel.Children.Clear();
            _costRows.Clear();
            EnsureCostRows();

            var items = _viewModel.CharacterItems;
            if (items.Count == 0)
            {
                AddToCostRow(null, CreatePanelItem(LoadImageSource(null)));
                return;
            }

            foreach (var item in items)
            {
                AddToCostRow(item.cost, CreatePanelItem(LoadImageSource(item.file), item.position, item.cost, item.bonds));
            }
        }

        private void UpdateBorderForPlacement(Border border, int? position, int? cost)
        {
            // 更新棋盘格子高亮
            var row = GetPositionFromTag(border.Tag);
            var shouldHighlight = position switch
            {
                0 => row == 1,
                1 => row == 0,
                _ => false
            };

            border.Background = shouldHighlight ? Brushes.Red : GetBrushForCost(cost);
        }

        private void EnsureCostRows()
        {
            // 初始化 cost 分行容器
            if (_imagePanel is null)
            {
                return;
            }

            _imagePanel.Children.Clear();
            for (var cost = 1; cost <= 5; cost++)
            {
                var row = new WrapPanel
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Tag = cost,
                    Margin = cost == 5 ? new Thickness(0) : new Thickness(0, 0, 0, 8)
                };

                row.SetValue(DragDrop.AllowDropProperty, true);
                row.AddHandler(DragDrop.DragOverEvent, OnDragOver);
                row.AddHandler(DragDrop.DropEvent, OnDrop);

                _costRows[cost] = row;
                _imagePanel.Children.Add(row);
            }
        }

        private void AddToCostRow(int? cost, Control element)
        {
            // 将角色添加到对应 cost 行
            if (_imagePanel is null)
            {
                return;
            }

            if (cost is not null && _costRows.TryGetValue(cost.Value, out var row))
            {
                row.Children.Add(element);
                return;
            }

            if (_costRows.TryGetValue(1, out var fallbackRow))
            {
                fallbackRow.Children.Add(element);
            }
        }

        private static IBrush GetBrushForCost(int? cost)
        {
            // cost 对应颜色
            return cost switch
            {
                2 => Brushes.Green,
                3 => Brushes.Blue,
                4 => Brushes.Purple,
                5 => Brushes.Gold,
                _ => Brushes.Transparent
            };
        }

        private Image CreateCharacterImage(IImage source, int? position, int? cost, IReadOnlyList<string>? baseBonds, IReadOnlyList<string>? equipmentBonds, IReadOnlyList<string>? equipmentFiles)
        {
            var image = new Image
            {
                Source = source,
                Width = 50,
                Height = 50,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = new ImageMetadata(position, cost, baseBonds, equipmentBonds, equipmentFiles)
            };

            image.PointerPressed += OnDragStart;
            return image;
        }

        private Control CreateCharacterImageContainer(Image image, int? position)
        {
            var container = new Grid
            {
                Width = 50,
                Height = 50
            };

            container.Children.Add(image);

            var positionFile = GetPositionIconFile(position);
            if (!string.IsNullOrWhiteSpace(positionFile))
            {
                container.Children.Add(new Image
                {
                    Source = LoadImageSource(positionFile),
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    IsHitTestVisible = false
                });
            }

            return container;
        }

        private string? GetPositionIconFile(int? position)
        {
            var iconName = position switch
            {
                0 => FrontIconName,
                1 => BackIconName,
                2 => FrontBackIconName,
                _ => null
            };

            return iconName is null ? null : GetOtherFile(iconName);
        }

        private string? GetOtherFile(string name)
        {
            return _viewModel.OtherDefinitions.TryGetValue(name, out var item) ? item.file : null;
        }

        private static int? GetPositionFromTag(object? tag)
        {
            return tag switch
            {
                ImageMetadata metadata => metadata.Position,
                int position => position,
                string text when int.TryParse(text, out var position) => position,
                _ => null
            };
        }

        private static int? GetCostFromTag(object? tag)
        {
            return tag switch
            {
                ImageMetadata metadata => metadata.Cost,
                int cost => cost,
                string text when int.TryParse(text, out var cost) => cost,
                _ => null
            };
        }

        private static IReadOnlyList<string>? GetBondsFromTag(object? tag)
        {
            // 读取合并后的羁绊
            return tag switch
            {
                ImageMetadata metadata => CombineBonds(metadata.BaseBonds, metadata.EquipmentBonds),
                IReadOnlyList<string> bonds => bonds,
                _ => null
            };
        }

        private static IReadOnlyList<string>? GetBaseBondsFromTag(object? tag)
        {
            return tag switch
            {
                ImageMetadata metadata => metadata.BaseBonds,
                IReadOnlyList<string> bonds => bonds,
                _ => null
            };
        }

        private static IReadOnlyList<string>? GetEquipmentBondsFromTag(object? tag)
        {
            return tag switch
            {
                ImageMetadata metadata => metadata.EquipmentBonds,
                IReadOnlyList<string> bonds => bonds,
                _ => null
            };
        }

        private static Image? GetBoardImage(Border border)
        {
            // 获取棋盘格子里的角色图片
            return FindImage(border.Child as Control);
        }

        private static WrapPanel? GetBoardEquipmentPanel(Border border)
        {
            // 获取棋盘格子里的装备容器
            return FindWrapPanel(border.Child as Control);
        }

        private static Image? FindImage(Control? control)
        {
            switch (control)
            {
                case Image image:
                    return image;
                case Border border:
                    return FindImage(border.Child as Control);
                case Panel panel:
                    foreach (var child in panel.Children.OfType<Control>())
                    {
                        var result = FindImage(child);
                        if (result is not null)
                        {
                            return result;
                        }
                    }
                    break;
            }

            return null;
        }

        private static WrapPanel? FindWrapPanel(Control? control)
        {
            switch (control)
            {
                case WrapPanel wrapPanel:
                    return wrapPanel;
                case Border border:
                    return FindWrapPanel(border.Child as Control);
                case Panel panel:
                    foreach (var child in panel.Children.OfType<Control>())
                    {
                        var result = FindWrapPanel(child);
                        if (result is not null)
                        {
                            return result;
                        }
                    }
                    break;
            }

            return null;
        }

        private Border CreateEquipmentBadge(string file)
        {
            // 生成装备徽标
            return new Border
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(1, 0, 1, 0),
                Child = new Image
                {
                    Source = LoadImageSource(file),
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform
                }
            };
        }

        private static IReadOnlyList<string>? GetEquipmentFilesFromTag(object? tag)
        {
            return tag switch
            {
                ImageMetadata metadata => metadata.EquipmentFiles,
                IReadOnlyList<string> files => files,
                _ => null
            };
        }

        private void AddEquipmentBonds(Border targetBorder, Image targetImage, IReadOnlyList<string> equipmentBonds, string? equipmentFile, bool isClearTool)
        {
            // 处理装备绑定与清除
            if (targetImage.Tag is not ImageMetadata metadata)
            {
                return;
            }

            var boardEquipmentPanel = GetBoardEquipmentPanel(targetBorder);
            if (isClearTool)
            {
                // 拆装工具：清空装备列表
                targetImage.Tag = new ImageMetadata(metadata.Position, metadata.Cost, metadata.BaseBonds, [], []);
                boardEquipmentPanel?.Children.Clear();
                return;
            }

            var updatedEquipmentBonds = metadata.EquipmentBonds is null
                ? new List<string>(equipmentBonds)
                : new List<string>(metadata.EquipmentBonds.Concat(equipmentBonds));

            if (equipmentBonds.Any(bond =>
                    (metadata.BaseBonds?.Contains(bond) == true) ||
                    (metadata.EquipmentBonds?.Contains(bond) == true)))
            {
                // 避免装备羁绊与已有羁绊冲突
                return;
            }

            var updatedEquipment = metadata.EquipmentFiles is null
                ? new List<string>()
                : new List<string>(metadata.EquipmentFiles);

            if (!string.IsNullOrWhiteSpace(equipmentFile) && updatedEquipment.Count < 3)
            {
                // 限制最多 3 个装备
                updatedEquipment.Add(equipmentFile);
            }
            else if (!string.IsNullOrWhiteSpace(equipmentFile))
            {
                return;
            }

            targetImage.Tag = new ImageMetadata(metadata.Position, metadata.Cost, metadata.BaseBonds, updatedEquipmentBonds, updatedEquipment);
            if (boardEquipmentPanel is not null && !string.IsNullOrWhiteSpace(equipmentFile) && updatedEquipment.Count <= 3)
            {
                boardEquipmentPanel.Children.Add(CreateEquipmentBadge(equipmentFile));
            }
        }

        private static IReadOnlyList<string>? CombineBonds(IReadOnlyList<string>? baseBonds, IReadOnlyList<string>? equipmentBonds)
        {
            // 合并基础羁绊与装备羁绊
            if (baseBonds is null && equipmentBonds is null)
            {
                return null;
            }

            var combined = new List<string>();
            if (baseBonds is not null)
            {
                combined.AddRange(baseBonds);
            }

            if (equipmentBonds is not null)
            {
                combined.AddRange(equipmentBonds);
            }

            return combined;
        }

        private sealed class ImageMetadata(int? position, int? cost, IReadOnlyList<string>? baseBonds, IReadOnlyList<string>? equipmentBonds, IReadOnlyList<string>? equipmentFiles)
        {
            public int? Position { get; } = position;
            public int? Cost { get; } = cost;
            public IReadOnlyList<string>? BaseBonds { get; } = baseBonds;
            public IReadOnlyList<string>? EquipmentBonds { get; } = equipmentBonds;
            public IReadOnlyList<string>? EquipmentFiles { get; } = equipmentFiles;
        }

        private sealed class EquipmentMetadata(IReadOnlyList<string> bonds, string? file, bool isClearTool = false)
        {
            public IReadOnlyList<string> Bonds { get; } = bonds;
            public string? File { get; } = file;
            public bool IsClearTool { get; } = isClearTool;
        }

        private void InitializeBoardBorders()
        {
            _boardBorders.Clear();
            var topRow = this.FindControl<UniformGrid>("TopRow");
            var bottomRow = this.FindControl<UniformGrid>("BottomRow");
            if (topRow is not null)
            {
                _boardBorders.AddRange(topRow.Children.OfType<Border>());
            }

            if (bottomRow is not null)
            {
                _boardBorders.AddRange(bottomRow.Children.OfType<Border>());
            }
        }

        private void UpdateBondsSummary()
        {
            if (_bondsPanel is null)
            {
                return;
            }

            var bondCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var border in _boardBorders)
            {
                var image = GetBoardImage(border);
                if (image is null)
                {
                    continue;
                }

                var bonds = GetBondsFromTag(image.Tag);
                if (bonds is null)
                {
                    continue;
                }

                foreach (var bond in bonds)
                {
                    if (string.IsNullOrWhiteSpace(bond))
                    {
                        continue;
                    }

                    bondCounts.TryGetValue(bond, out var count);
                    bondCounts[bond] = count + 1;
                }
            }

            _bondsPanel.Children.Clear();
            if (bondCounts.Count == 0)
            {
                return;
            }

            var orderedEntries = bondCounts
                .Select(entry => (entry.Key, entry.Value, Definition: _viewModel.BondDefinitions.GetValueOrDefault(entry.Key)))
                .Where(entry => entry.Definition is not null)
                .OrderBy(entry => GetBondSortCategory(entry.Definition!, entry.Value))
                .ThenByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in orderedEntries)
            {
                _bondsPanel.Children.Add(CreateBondEntry(entry.Definition!, entry.Value));
            }
        }
        private static int GetBondSortCategory(BondDefinition definition, int count)
        {
            var activateList = definition.activate ?? [];
            if (activateList.Count == 1 && activateList[0] == 1)
            {
                return 0;
            }

            if (activateList.Count > 0 && count >= activateList.Min())
            {
                return 1;
            }

            return 2;
        }

        // 羁绊数据由 ViewModel 统一加载。

        private Control CreateBondEntry(BondDefinition definition, int count)
        {
            var container = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var iconGrid = new Grid
            {
                Width = 50,
                Height = 50
            };

            var iconBackground = new Border
            {
                Width = 36,
                Height = 36,
                Background = GetBondBackground(definition.activate, count),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                RenderTransform = new RotateTransform(45),
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };

            var icon = new Image
            {
                Source = LoadImageSource(definition.file),
                Width = 26,
                Height = 26,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var badge = new Border
            {
                Background = Brushes.Black,
                CornerRadius = new CornerRadius(10),
                Width = 20,
                Height = 20,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
            };

            var badgeText = new TextBlock
            {
                Text = count.ToString(),
                Foreground = Brushes.White,
                FontSize = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            badge.Child = badgeText;
            iconGrid.Children.Add(iconBackground);
            iconGrid.Children.Add(icon);
            iconGrid.Children.Add(badge);

            var textPanel = new StackPanel
            {
                Margin = new Thickness(8, 0, 0, 0)
            };

            var nameText = new TextBlock
            {
                Text = definition.name ?? string.Empty,
                FontSize = 16,
                Foreground = Brushes.White,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    OffsetX = 0,
                    OffsetY = 0,
                    Opacity = 0.9
                }
            };

            var activateText = new TextBlock
            {
                FontSize = 16,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Gray,
                    BlurRadius = 4,
                    OffsetX = 1,
                    OffsetY = 1,
                    Opacity = 0.8
                }
            };

            var activateList = definition.activate ?? [];
            var activeThreshold = activateList.Where(v => v <= count).DefaultIfEmpty().Max();
            for (var i = 0; i < activateList.Count; i++)
            {
                var value = activateList[i];
                if (i > 0)
                {
                    activateText.Inlines.Add(new Run("/"));
                }

                var run = new Run(value.ToString());
                if (value == activeThreshold && activeThreshold != 0)
                {
                    run.FontWeight = FontWeight.Bold;
                    run.Foreground = Brushes.Gold;
                }
                else
                {
                    run.Foreground = Brushes.LightGray;
                }

                activateText.Inlines.Add(run);
            }

            if (activateList.Count == 0)
            {
                activateText.Text = count.ToString();
            }

            textPanel.Children.Add(nameText);
            textPanel.Children.Add(activateText);

            container.Children.Add(iconGrid);
            container.Children.Add(textPanel);
            Grid.SetColumn(textPanel, 1);

            return container;
        }

        private static IBrush GetBondBackground(IReadOnlyList<int>? activateList, int count)
        {
            if (activateList is null || activateList.Count == 0)
            {
                return Brushes.Gray;
            }

            var minThreshold = activateList.Min();
            if (count < minThreshold)
            {
                return Brushes.Gray;
            }

            var maxThreshold = activateList.Max();
            if (maxThreshold == 1 && count >= 1)
            {
                return Brushes.HotPink;
            }

            if (count >= maxThreshold)
            {
                return Brushes.MediumPurple;
            }

            return Brushes.SaddleBrown;
        }

        private IImage LoadImageSource(string? file)
        {
            var assetPath = string.IsNullOrWhiteSpace(file) ? "/Assets/avalonia-logo.ico" : file;
            if (assetPath.StartsWith("/", StringComparison.Ordinal))
            {
                var uri = new Uri($"avares://CurrencyWarsTool{assetPath}");
                return new Bitmap(AssetLoader.Open(uri));
            }

            if (assetPath.StartsWith("avares://", StringComparison.Ordinal))
            {
                return new Bitmap(AssetLoader.Open(new Uri(assetPath)));
            }

            var filePath = Path.Combine(AppPaths.RootDirectory, assetPath);
            return File.Exists(filePath)
                ? new Bitmap(filePath)
                : new Bitmap(AssetLoader.Open(new Uri("avares://CurrencyWarsTool/Assets/avalonia-logo.ico")));
        }

    }
}