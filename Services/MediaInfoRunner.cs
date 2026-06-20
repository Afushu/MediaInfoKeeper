using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MediaInfoKeeper.Services
{
    public static class MediaInfoRunner
    {
        private static readonly object GateSync = new object();
        private static int configuredConcurrency;
        private static int activeCount;
        private static TaskCompletionSource<bool> availability =
            CreateAvailabilitySource();

        public static async Task<bool> ExtractMediaInfoAsync(
            long internalId,
            string source = "媒体信息提取",
            Action<MetadataRefreshOptions> configureRefreshOptions = null,
            CancellationToken cancellationToken = default,
            MediaStreamType[] requiredStreamTypes = null)
        {
            var item = Plugin.LibraryManager?.GetItemById(internalId) as BaseItem;
            if (item == null)
            {
                return false;
            }

            await WaitForTurnAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Plugin.MediaInfoService
                    .ExtractMediaInfoAsync(item, source, configureRefreshOptions, cancellationToken, requiredStreamTypes)
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
