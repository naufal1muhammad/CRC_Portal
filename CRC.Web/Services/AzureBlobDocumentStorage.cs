using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace CRC.Web.Services
{
    /// <summary>
    /// Azure Blob implementation of <see cref="IDocumentStorage"/>. Every patient and staff document lives in a
    /// single <b>private</b> container (created on first use with <see cref="PublicAccessType.None"/>) and is
    /// downloaded through a short-lived read SAS, so the container never has to be made public.
    /// <para>
    /// <see cref="BlobServiceClient"/> is thread-safe and is designed to be built once and reused — creating one
    /// per request would throw away its connection pooling. That is why this service is registered as a
    /// <b>singleton</b> in <c>Program.cs</c> and holds a single <see cref="BlobContainerClient"/> for the life
    /// of the application.
    /// </para>
    /// </summary>
    public class AzureBlobDocumentStorage : IDocumentStorage
    {
        private readonly BlobContainerClient _container;

        public AzureBlobDocumentStorage(IOptions<DocumentStorageOptions> options)
        {
            var settings = options.Value;
            var serviceClient = new BlobServiceClient(settings.ConnectionString);
            _container = serviceClient.GetBlobContainerClient(settings.ContainerName);
        }

        public async Task UploadAsync(Stream content, string blobName, string contentType)
        {
            // PublicAccessType.None is load-bearing: it is what keeps the container private, and therefore what
            // stops a blob URL from being readable the way a file under wwwroot was. Creating the container here
            // is a harmless no-op once it exists, and it means a fresh Azurite install works with no manual
            // provisioning step.
            await _container.CreateIfNotExistsAsync(PublicAccessType.None);

            var blob = _container.GetBlobClient(blobName);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            });
        }

        public Uri GetReadSasUrl(string blobName, TimeSpan ttl)
        {
            var blob = _container.GetBlobClient(blobName);

            // Signing a SAS requires the client to hold the account key, which is true for both Azurite's
            // well-known key and a storage-account connection string. If someone later switches this app to
            // Managed Identity, this is the line that will tell them: a user-delegation SAS is then required
            // instead, and it is better to fail loudly here than to hand out a URL that cannot work.
            if (!blob.CanGenerateSasUri)
            {
                throw new InvalidOperationException(
                    "Cannot generate a SAS URL — the storage client is not configured with an account key. " +
                    "Check DocumentStorage:ConnectionString, or switch to a user-delegation SAS if this app " +
                    "has moved to Managed Identity.");
            }

            var builder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = blobName,
                Resource = "b",
                // A small backdated start absorbs minor clock skew between the app and the storage service.
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.Add(ttl)
            };
            // READ ONLY. A download link must never be able to overwrite or delete the document it points at.
            builder.SetPermissions(BlobSasPermissions.Read);

            return blob.GenerateSasUri(builder);
        }

        public async Task DeleteAsync(string blobName)
        {
            var blob = _container.GetBlobClient(blobName);
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }
    }
}
