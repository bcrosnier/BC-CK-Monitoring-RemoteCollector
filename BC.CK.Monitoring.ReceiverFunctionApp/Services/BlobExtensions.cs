using Microsoft.Azure.Storage.Blob;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;

namespace CK.Monitoring.ReceiverFunctionApp.Services
{
    public static class BlobExtensions
    {
        public static async IAsyncEnumerable<IListBlobItem> ListBlobsAsync(
            this CloudBlobContainer container,
            string prefix,
            bool useFlatBlobListing,
            BlobListingDetails blobListingDetails,
            int? maxResults = null,
            BlobRequestOptions? options = null,
            OperationContext? operationContext = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )

        {
            BlobContinuationToken? continuationToken = null;
            do
            {
                var response = await container.ListBlobsSegmentedAsync(
                    prefix,
                    useFlatBlobListing,
                    blobListingDetails,
                    maxResults,
                    continuationToken,
                    options,
                    operationContext,
                    cancellationToken
                );
                continuationToken = response.ContinuationToken;
                foreach (var listBlobItem in response.Results)
                {
                    yield return listBlobItem;
                }
            } while (continuationToken != null);
        }

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> @this)
        {
            List<T> l = new List<T>();
            await foreach (var x in @this)
            {
                l.Add(x);
            }
            return l;
        }
    }
}