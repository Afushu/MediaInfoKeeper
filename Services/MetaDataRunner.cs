using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;

namespace MediaInfoKeeper.Services
{
    public static class MetaDataRunner
    {
        private static readonly object GateSync = new object();
        private static int configuredConcurrency;
        private static int activeCount;
        private static TaskCompletionSource<bool> availability =
            CreateAvailabilitySource();

        public static async Task RefreshMetaDataAsync(
            long internalId,
            MetadataRefreshOptions options,
            CancellationToken cancellationToken = default)
        {
            var item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
            await WaitForTurnAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Plugin.MetaDataService
                    .RefreshMetaDataAsync(item, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ReleaseTurn();
            }
        }

        public static async Task RefreshMetaDataAsync(
            long internalId,
            CancellationToken cancellationToken = default)
        {
            var item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
            if (item == null)
            {
                return;
            }

            var displayName = item.FileName ?? item.Path ?? item.Name;
            var logger = Plugin.SharedLogger;
            var refreshOptions = GetRefreshOptions();

            try
            {
                await RefreshMetaDataAsync(internalId, refreshOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.Error($"入库元数据: 刷新失败 item={displayName}");
                logger?.Error(ex.Message);
                logger?.Debug(ex.StackTrace);
            }
        }

        private static MetadataRefreshOptions GetRefreshOptions()
        {
            return new MetadataRefreshOptions(new DirectoryService(Plugin.SharedLogger, Plugin.FileSystem))
            {
                EnableRemoteContentProbe = false,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllMetadata = true,
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllImages = true,
                EnableThumbnailImageExtraction = Plugin.Instance.Options.MetaData.EnableImageCapture,
                EnableSubtitleDownloading = false
            };
        }

        private static async Task WaitForTurnAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task waiter;
                lock (GateSync)
                {
                    var maxConcurrent = GetMaxConcurrent();
                    if (configuredConcurrency != maxConcurrent)
                    {
                        configuredConcurrency = maxConcurrent;
                        if (activeCount < configuredConcurrency)
                        {
                            SignalAvailability();
                        }
                    }

                    if (activeCount < configuredConcurrency)
                    {
                        activeCount++;
                        return;
                    }

                    waiter = availability.Task;
                }

                await waiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static void ReleaseTurn()
        {
            lock (GateSync)
            {
                if (activeCount > 0)
                {
                    activeCount--;
                }

                if (activeCount < configuredConcurrency)
                {
                    SignalAvailability();
                }
            }
        }

        private static void SignalAvailability()
        {
            var current = availability;
            availability = CreateAvailabilitySource();
            current.TrySetResult(true);
        }

        private static int GetMaxConcurrent()
        {
            return Math.Max(1, Plugin.Instance?.Options?.MetaData?.MaxConcurrentCount ?? 3);
        }

        private static TaskCompletionSource<bool> CreateAvailabilitySource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
