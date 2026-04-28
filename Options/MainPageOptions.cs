using System;
using System.Collections.Generic;
using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Editors;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Attributes;

namespace MediaInfoKeeper.Options
{
    public class MainPageOptions : EditableOptionsBase
    {
        public enum RefreshModeOption
        {
            [Description("补全缺失")]
            Fill,
            [Description("全部替换")]
            Replace
        }

        public override string EditorTitle => "基础设置";

        public override string EditorDescription => "媒体信息处理、媒体库范围和计划任务这些常用设置。改完记得保存。";

        [DisplayName("启用插件")]
        [Description("关闭后将不执行任何行为。")]
        public bool PlugginEnabled { get; set; } = true;

        [DisplayName("Emby入库扫描延迟（秒）")]
        [Description("控制 Emby 实时入库扫描的等待时间，Emby 默认值 90s。光速入库，不建议小于10s。")]
        [MinValue(5), MaxValue(90)]
        public int FileChangeRefreshDelaySeconds { get; set; } = 15;
        
        [Browsable(false)]
        public IEnumerable<EditorSelectOption> LibraryList { get; set; }

        [DisplayName("追更媒体库")]
        [Description("用于入库触发与删除 JSON 逻辑；留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string CatchupLibraries { get; set; } = string.Empty;

        [DisplayName("计划任务媒体库")]
        [Description("用于计划任务范围；留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string ScheduledTaskLibraries { get; set; } = string.Empty;

        [DisplayName("最近入库时间窗口（天）")]
        [Description("用于“刷新媒体元数据”计划任务，仅处理指定天数内入库的条目，0 表示不限制。")]
        [MinValue(0)]
        [MaxValue(3650)]
        public int RecentItemsDays { get; set; } = 3;

        [DisplayName("最近入库媒体筛选数量")]
        [Description("用于“提取媒体信息、扫描片头”计划任务，默认 100。")]
        [MinValue(1)]
        [MaxValue(100000000)]
        public int RecentItemsLimit { get; set; } = 100;

        [DisplayName("刷新模式")]
        [Description("依据 Emby 媒体库中的设置和元数据提供器，用新的数据更新元数据。")]
        public RefreshModeOption RefreshMetadataMode { get; set; } = RefreshModeOption.Fill;

        [DisplayName("替换现有图像")]
        [Description("基于媒体库选项，将删除全部现有图像，并下载新图像。在某些情况下，这可能会导致可用图像比以前更少。")]
        public bool ReplaceExistingImages { get; set; } = true;

        [DisplayName("替换现有视频预览缩略图")]
        [Description("如果在媒体库选项中启用此功能，将删除所有现有视频预览缩略图并生成新缩略图。")]
        public bool ReplaceExistingVideoPreviewThumbnails { get; set; } = true;

        public override IEditObjectContainer CreateEditContainer()
        {
            var container = (EditObjectContainer)base.CreateEditContainer();
            var root = container.EditorRoot;
            if (root?.EditorItems == null || root.EditorItems.Length == 0)
            {
                return container;
            }

            var itemLookup = new Dictionary<string, EditorBase>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in root.EditorItems)
            {
                var key = item.Name ?? item.Id;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!itemLookup.ContainsKey(key))
                {
                    itemLookup.Add(key, item);
                }
            }

            var groupedItems = new List<EditorBase>();
            var groupIndex = 0;

            void AddGroup(string title, string description, params string[] propertyNames)
            {
                var items = new List<EditorBase>();
                foreach (var propertyName in propertyNames)
                {
                    if (itemLookup.TryGetValue(propertyName, out var item))
                    {
                        items.Add(item);
                        itemLookup.Remove(propertyName);
                    }
                }

                if (items.Count == 0)
                {
                    return;
                }

                groupIndex++;
                var group = new EditorGroup(title, items.ToArray(), $"group{groupIndex}", root.Id, null)
                {
                    Description = description
                };
                groupedItems.Add(group);
            }

            AddGroup("插件", "",
                nameof(PlugginEnabled),
                nameof(FileChangeRefreshDelaySeconds));

            AddGroup("媒体库范围", "限制生效媒体库范围",
                nameof(CatchupLibraries),
                nameof(ScheduledTaskLibraries));

            AddGroup("计划任务", "计划任务中，各项任务用到的参数设置",
                nameof(RecentItemsDays),
                nameof(RecentItemsLimit),
                nameof(RefreshMetadataMode),
                nameof(ReplaceExistingImages),
                nameof(ReplaceExistingVideoPreviewThumbnails));

            var remaining = new List<EditorBase>();
            foreach (var item in root.EditorItems)
            {
                var key = item.Name ?? item.Id;
                if (!string.IsNullOrEmpty(key) && itemLookup.ContainsKey(key))
                {
                    remaining.Add(item);
                    itemLookup.Remove(key);
                }
            }

            if (remaining.Count > 0)
            {
                groupIndex++;
                groupedItems.Add(new EditorGroup("其他", remaining.ToArray(), $"group{groupIndex}", root.Id, null));
            }

            if (groupedItems.Count > 0)
            {
                root.EditorItems = groupedItems.ToArray();
            }

            return container;
        }
    }
}
