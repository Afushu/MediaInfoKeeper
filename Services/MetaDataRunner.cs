using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

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
