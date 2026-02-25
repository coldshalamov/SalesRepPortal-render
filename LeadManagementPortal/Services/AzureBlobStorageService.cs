using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LeadManagementPortal.Models;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LeadManagementPortal.Services
{
    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly AzureStorageOptions _options;

        public AzureBlobStorageService(IOptions<AzureStorageOptions> options)
        {
            _options = options.Value;
            
            if (!string.IsNullOrEmpty(_options.ConnectionString))
            {
                _containerClient = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            }
            else
            {
                var connectionString = $"DefaultEndpointsProtocol=https;AccountName={_options.AccountName};AccountKey={_options.AccountKey};EndpointSuffix=core.windows.net";
                _containerClient = new BlobContainerClient(connectionString, _options.ContainerName);
            }
            
            _containerClient.CreateIfNotExists();
        }

        public async Task<string> UploadAsync(Stream content, string contentType, string key, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(key);
            var blobUploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };
            
            // This will overwrite by default if no conditions are specified
            await blobClient.UploadAsync(content, blobUploadOptions, ct);
            return key;
        }

        public Task<string> GetPreSignedDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(key);
            
            if (!blobClient.CanGenerateSasUri)
            {
                 return Task.FromResult(string.Empty);
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerClient.Name,
                BlobName = key,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expires)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return Task.FromResult(sasUri.ToString());
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(key);
            return await blobClient.ExistsAsync(ct);
        }

        public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(key);
            var response = await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions: null, cancellationToken: ct);
            return response.Value;
        }
    }
}
