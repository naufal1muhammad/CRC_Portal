namespace CRC.Web.Services
{
    /// <summary>
    /// Abstraction over the document blob store. Patient and staff documents live in a <b>private</b> Azure
    /// Blob container; SQL holds only the metadata and the blob key
    /// (<c>dbo.PatientDocument.BlobName</c> / <c>dbo.StaffDocument.BlobName</c>) — <b>no blob bytes ever touch
    /// the database</b>.
    /// <para>
    /// This exists because documents used to be written under <c>wwwroot/uploads/</c> and their web path stored
    /// in the database. Static-file serving sits before authentication in the pipeline and performs no
    /// authorisation check at all, so every such file was a permanently public URL. Keeping the bytes in a
    /// private container removes that class of mistake entirely: there is no URL to leak, because the container
    /// is not readable without a signature.
    /// </para>
    /// <para>
    /// Downloads are therefore served through <b>short-lived read SAS URLs</b> minted on demand by an
    /// authenticated endpoint, which is what lets the container stay private. A SAS URL is a temporary
    /// credential — it is returned to the browser for one click and is <b>never persisted</b>, never stored in a
    /// database column and never rendered into a page's markup.
    /// </para>
    /// <para>
    /// The implementation is <see cref="AzureBlobDocumentStorage"/>, registered as a singleton in
    /// <c>Program.cs</c> and bound to <c>DocumentStorage:ConnectionString</c> /
    /// <c>DocumentStorage:ContainerName</c> (see <see cref="DocumentStorageOptions"/>).
    /// </para>
    /// </summary>
    public interface IDocumentStorage
    {
        /// <summary>
        /// Uploads (or overwrites) a blob from the given stream. <paramref name="blobName"/> is the key within
        /// the container — build it with
        /// <see cref="CRC.Web.Infrastructure.DocumentValidation.BuildBlobName(string, string, string)"/> so the
        /// key shape stays consistent, e.g. <c>patients/PAT-000042/9f1c….pdf</c>. The
        /// <paramref name="contentType"/> is stored on the blob so a later SAS download is served with the right
        /// <c>Content-Type</c> header.
        /// </summary>
        Task UploadAsync(Stream content, string blobName, string contentType);

        /// <summary>
        /// Mints a short-lived, <b>read-only</b> SAS URL for the blob, valid for <paramref name="ttl"/>. The
        /// returned URL grants temporary read access to an otherwise-private blob — hand it to the caller for
        /// one download and never persist it.
        /// </summary>
        Uri GetReadSasUrl(string blobName, TimeSpan ttl);

        /// <summary>
        /// Hard-deletes the blob, so removing a document row also reclaims its storage rather than orphaning
        /// patient data in the container forever. A no-op when the blob is already gone.
        /// </summary>
        Task DeleteAsync(string blobName);
    }
}
