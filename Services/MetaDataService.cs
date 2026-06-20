using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;

namespace MediaInfoKeeper.Services
{
    public class MetaDataService
    {
        private readonly ILibraryManager libraryManager;
        private readonly IProviderManager providerManager;

        public MetaDataService(ILibraryManager libraryManager, IProviderManager providerManager)
        {
            this.libraryManager = libraryManager;
            this.providerManager = providerManager;
        }

        internal async Task RefreshMetaDataAsync(
            BaseItem item,
            MetadataRefreshOptions options,
            CancellationToken cancellationToken)
        {
            if (item == null || options == null)
            {
                return;
            }

            var collectionFolders = this.libraryManager.GetCollectionFolders(item).Cast<BaseItem>().ToArray();
            var libraryOptions = this.libraryManager.GetLibraryOptions(item);

            await this.providerManager
                .RefreshSingleItem(item, options, collectionFolders, libraryOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
