using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;

namespace CK.Monitoring.ReceiverFunctionApp.Services
{
    public class LogEntryCollector
    {
        static string AccountName => Constants.AzureStorageAccountName;
        static string BlobContainerName => Constants.AzureBlobContainerName;

        private bool _initialized;
        private readonly StorageAccountProvider _storageAccountProvider;
        private readonly CloudBlobClient _blobClient;
        private readonly CloudBlobContainer _blobContainer;

        private readonly ConcurrentDictionary<string, ConcurrentQueue<ILogEntry>> _logEntriesByBlobName =
            new ConcurrentDictionary<string, ConcurrentQueue<ILogEntry>>();

        private readonly byte[] _fileHeader;

        public LogEntryCollector(StorageAccountProvider storageAccountProvider)
        {
            _storageAccountProvider = storageAccountProvider;

            var storageAccount = _storageAccountProvider.Get(AccountName);
            if (storageAccount == null)
                throw new InvalidOperationException($"StorageAccount {AccountName} is not configured.");

            _blobClient = storageAccount.CreateCloudBlobClient();
            _blobContainer = _blobClient.GetContainerReference(BlobContainerName);

            // See also: MonitorBinaryFileOutput.OpenNewFile() in CK-Monitoring
            _fileHeader = new byte[LogReader.FileHeader.Length + 2];
            using (var ms = new MemoryStream())
            using (var bw = new CKBinaryWriter(ms))
            {
                bw.Write(LogReader.FileHeader);
                bw.Write(LogReader.CurrentStreamVersion);
                bw.Flush();
                ms.Position = 0;
                _fileHeader = ms.ToArray();
            }
        }

        public void CollectEntry(string appId, ILogEntry e)
        {
            string blobName = GetBlobName(appId, e);
            var queue = _logEntriesByBlobName.GetOrAdd(blobName, (_) => new ConcurrentQueue<ILogEntry>());

            queue.Enqueue(e);
        }

        public async Task CommitEntriesAsync()
        {
            await InitializeBlobContainerAsync();
            var blobNames = _logEntriesByBlobName.Keys.ToArray();
            foreach (var blobName in blobNames)
            {
                if (_logEntriesByBlobName.TryRemove(blobName, out var queue))
                {
                    // Init blob
                    var blob = _blobContainer.GetAppendBlobReference(blobName);

                    // Create if not exists
                    await InitializeBlobAsync(blob);

                    // Acquire Lease
                    string? leaseId = null;
                    while (leaseId == null)
                    {
                        try
                        {
                            leaseId = await blob.AcquireLeaseAsync(TimeSpan.FromSeconds(15));
                        }
                        catch (StorageException ex)
                        {
                            if (ex.RequestInformation.HttpStatusCode == 409)
                            {
                                // Lease acquired by someone else. Wait and retry.
                                await Task.Delay(1000);
                            }
                            else throw;
                        }
                    }

                    AccessCondition accessCondition = AccessCondition.GenerateLeaseCondition(leaseId);

                    try
                    {
                        using (MemoryStream ms = new MemoryStream())
                        using (CKBinaryWriter bw = new CKBinaryWriter(ms, new UTF8Encoding(), leaveOpen: true))
                        {
                            while (queue.TryDequeue(out var logEntry))
                            {
                                logEntry.WriteLogEntry(bw);
                            }

                            bw.Flush();
                            ms.Position = 0;
                            await blob.AppendFromStreamAsync(ms, accessCondition, null, null);
                        }
                    }
                    finally
                    {
                        await blob.ReleaseLeaseAsync(accessCondition);
                    }
                }
            }
        }

        private async Task InitializeBlobContainerAsync()
        {
            if (_initialized) return;
            await _blobContainer.CreateIfNotExistsAsync();
            _initialized = true;
        }

        private async Task InitializeBlobAsync(CloudAppendBlob b)
        {
            if (!await b.ExistsAsync())
            {
                await b.UploadFromByteArrayAsync(_fileHeader, 0, _fileHeader.Length,
                    AccessCondition.GenerateIfNotExistsCondition(), null, null);
            }
        }

        private static string GetBlobName(string appId, ILogEntry logEntry)
        {
            return
                $"ckmon/{appId}/" +
                $"{logEntry.LogTime.TimeUtc:yyyy-MM-dd}/" +
                $"{appId}_{logEntry.LogTime.TimeUtc:HH}-00-00.{LogReader.CurrentStreamVersion}.ckmon";
        }

        public async Task<List<string>> GetAppNamesAsync()
        {
            var blobsAsync = _blobContainer.ListBlobsAsync("ckmon/", false, BlobListingDetails.None);

            List<string> appNames = new List<string>();
            await foreach (var dir in blobsAsync)
            {
                if (dir is CloudBlobDirectory appDir)
                {
                    string prefix = appDir.Prefix;
                    int idx0 = prefix.LastIndexOf('/', prefix.Length - 2) + 1;
                    int len = prefix.Length - 1 - idx0;
                    appNames.Add(prefix.Substring(idx0, len));
                }
            }

            return appNames;
        }

        public async Task<List<string>> GetAppLogNamesAsync()
        {
            var blobsAsync = _blobContainer.ListBlobsAsync("ckmon/", false, BlobListingDetails.None);

            List<string> appNames = new List<string>();
            await foreach (var dir in blobsAsync)
            {
                if (dir is CloudBlobDirectory appDir)
                {
                    string prefix = appDir.Prefix;
                    int idx0 = prefix.LastIndexOf('/', prefix.Length - 2) + 1;
                    int len = prefix.Length - 1 - idx0;
                    appNames.Add(prefix.Substring(idx0, len));
                }
            }

            return appNames;
        }

        public async Task<List<string>> GetAppFilesAsync(string appId)
        {
            var blobsAsync = _blobContainer.ListBlobsAsync($"ckmon/{appId}", true, BlobListingDetails.None);

            List<string> fileNames = new List<string>();
            await foreach (var blob in blobsAsync)
            {
                fileNames.Add(blob.Uri.ToString());
            }

            return fileNames;
        }
    }
}