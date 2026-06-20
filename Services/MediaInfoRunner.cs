using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace MediaInfoKeeper.Services
{
    public static class MediaInfoRunner
    {
        private static readonly object GateSync = new object();
        private static readonly object ExtractionSync = new object();
        private static readonly Dictionary<long, Task<bool>> RunningExtractions =
            new Dictionary<long, Task<bool>>();
        private static int configuredConcurrency;
        private static int activeCount;
        private static TaskCompletionSource<bool> availability =
            CreateAvailabilitySource();

        public static async Task<bool> ExtractMediaInfoAsync(
            long internalId,
            string source = "媒体信息提取",
            CancellationToken cancellationToken = default,
            MediaStreamType[] requiredStreamTypes = null)
        {
            var item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
            if (item == null)
            {
                return false;
            }

            var displayName = item.FileName ?? item.Path ?? item.Name;
            Task<bool> extractionTask;
            var isOwner = false;
            lock (ExtractionSync)
            {
                if (!RunningExtractions.TryGetValue(internalId, out extractionTask))
                {
                    extractionTask = RunExtractionAsync(internalId, source, cancellationToken, requiredStreamTypes);
                    RunningExtractions[internalId] = extractionTask;
                    isOwner = true;
                }
            }

            if (!isOwner)
            {
                Plugin.SharedLogger?.Info($"{source} 提取媒体信息跳过 已在队列: {displayName}");
                return await extractionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await extractionTask.ConfigureAwait(false);
            }
            finally
            {
                lock (ExtractionSync)
                {
                    RunningExtractions.Remove(internalId);
                }
            }
        }

        private static async Task<bool> RunExtractionAsync(
            long internalId,
            string source,
            CancellationToken cancellationToken,
            MediaStreamType[] requiredStreamTypes)
        {
            var item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
            if (item == null)
            {
                return false;
            }

            await WaitForTurnAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
                if (item == null)
                {
                    return false;
                }

                return await Plugin.MediaInfoService
                    .ExtractMediaInfoAsync(item, source, cancellationToken, requiredStreamTypes)
                    .ConfigureAwait(false);
            }
            finally
            {
                ReleaseTurn();
            }
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
            return Math.Max(1, Plugin.Instance?.Options?.MediaInfo?.MaxConcurrentCount ?? 3);
        }

        private static TaskCompletionSource<bool> CreateAvailabilitySource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
