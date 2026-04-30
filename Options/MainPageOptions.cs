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
        [Description("计划任务默认范围；各任务未单独设置时继承这里。留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        [Browsable(false)]
        public string ScheduledTaskLibraries { get; set; } = string.Empty;

        [Browsable(false)]
        [DisplayName("最近入库时间窗口（天）")]
        [Description("计划任务默认时间窗口；对应任务未单独设置时继承这里，0 表示不限制。")]
        [MinValue(0)]
        [MaxValue(3650)]
        public int RecentItemsDays { get; set; } = 3;

        [Browsable(false)]
        [DisplayName("最近入库媒体筛选数量")]
        [Description("计划任务默认最近条目数量；对应任务未单独设置时继承这里。")]
        [MinValue(1)]
        [MaxValue(100000000)]
        public int RecentItemsLimit { get; set; } = 100;

        [DisplayName("媒体库范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string RefreshRecentMetadataLibraries { get; set; } = string.Empty;

        [DisplayName("刷新最近入库时间窗口（天）")]
        [Description("仅处理指定天数内入库的条目，0 表示不限制。")]
        [MinValue(0)]
        [MaxValue(3650)]
        public int RefreshRecentMetadataDays { get; set; } = 3;

        [DisplayName("刷新模式")]
        [Description("依据 Emby 媒体库中的设置和元数据提供器，用新的数据更新元数据。")]
        public RefreshModeOption RefreshMetadataMode { get; set; } = RefreshModeOption.Fill;

        [DisplayName("替换现有图像")]
        [Description("基于媒体库选项，将删除全部现有图像，并下载新图像。")]
        public bool ReplaceExistingImages { get; set; } = true;

        [DisplayName("替换现有视频预览缩略图")]
        [Description("如果在媒体库选项中启用此功能，将删除现有视频预览缩略图并生成新的缩略图。")]
        public bool ReplaceExistingVideoPreviewThumbnails { get; set; } = true;

        [DisplayName("媒体库范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string ScanRecentIntroLibraries { get; set; } = string.Empty;

        [DisplayName("扫描最近条目数量")]
        [MinValue(1)]
        [MaxValue(100000000)]
        public int ScanRecentIntroLimit { get; set; } = 100;

        [DisplayName("媒体库范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string ExtractRecentMediaInfoLibraries { get; set; } = string.Empty;

        [DisplayName("提取最近条目数量")]
        [MinValue(1)]
        [MaxValue(100000000)]
        public int ExtractRecentMediaInfoLimit { get; set; } = 100;

        [DisplayName("备份媒体信息范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string ExportExistingMediaInfoLibraries { get; set; } = string.Empty;

        [DisplayName("恢复媒体信息范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string RestoreMediaInfoLibraries { get; set; } = string.Empty;

        [DisplayName("扫描外挂字幕范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string ScanExternalSubtitleLibraries { get; set; } = string.Empty;

        [DisplayName("媒体库范围")]
        [Description("留空表示全部。")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string DownloadDanmuXmlLibraries { get; set; } = string.Empty;

        [DisplayName("下载最近入库时间窗口（天）")]
        [Description("仅处理指定天数内入库的条目，0 表示不限制。")]
        [MinValue(0)]
        [MaxValue(3650)]
        public int DownloadDanmuXmlDays { get; set; } = 3;

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

                groupIndex++;
                var group = new EditorGroup(title, items.ToArray(), $"group{groupIndex}", root.Id, null)
                {
                    Description = description
                };
                groupedItems.Add(group);
            }

            AddGroup("插件", "",
                nameof(PlugginEnabled),
                nameof(FileChangeRefreshDelaySeconds),
                nameof(CatchupLibraries));

            AddGroup("计划任务配置","参数配置");
            
            AddGroup("刷新媒体元数据", "",
                nameof(RefreshRecentMetadataDays),
                nameof(RefreshMetadataMode),
                nameof(ReplaceExistingImages),
                nameof(ReplaceExistingVideoPreviewThumbnails),
                nameof(RefreshRecentMetadataLibraries));

            AddGroup("扫描片头", "",
                nameof(ScanRecentIntroLimit),
                nameof(ScanRecentIntroLibraries));

            AddGroup("提取媒体信息", "",
                nameof(ExtractRecentMediaInfoLimit),
                nameof(ExtractRecentMediaInfoLibraries));
            
            AddGroup("下载弹幕", "",
                nameof(DownloadDanmuXmlDays),
                nameof(DownloadDanmuXmlLibraries));
            
            AddGroup("其他计划任务", "",
                nameof(ExportExistingMediaInfoLibraries),
                nameof(RestoreMediaInfoLibraries),
                nameof(ScanExternalSubtitleLibraries));


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
