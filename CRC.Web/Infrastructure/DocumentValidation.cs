namespace CRC.Web.Infrastructure
{
    /// <summary>
    /// Shared server-side rules for uploaded patient and staff documents: what may be uploaded, what the stored
    /// file name may look like, and what key the blob gets in the container.
    /// <para>
    /// This lives in one place on purpose. The reference implementation (HEART) keeps the same logic private
    /// inside its single upload controller, but nucentra has <b>three</b> upload endpoints across <b>two</b>
    /// controllers — patient documents in <c>StaffPatientController</c>, staff documents in
    /// <c>StaffController</c> — so a private copy in each would be three copies free to drift apart. One
    /// rejected file type here is a rejected file type everywhere.
    /// </para>
    /// <para>
    /// These are <b>server-side</b> checks and the only ones that count; anything the browser does is a
    /// convenience. Every file in a batch should be validated <i>before</i> any blob is written, so a bad file
    /// at the end of a selection cannot leave half an upload behind.
    /// </para>
    /// </summary>
    public static class DocumentValidation
    {
        /// <summary>Per-file size cap: 20 MB, matching HEART.</summary>
        public const long MaxDocumentBytes = 20L * 1024 * 1024;

        // The only file types accepted: PDF, PNG, JPEG, DOCX. Validation requires BOTH the extension and the
        // reported content-type to be in their allowed sets, so a renamed or spoofed file is rejected — an
        // uploaded .html or .svg is exactly the kind of active content that must never reach storage.
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".docx" };

        private static readonly HashSet<string> AllowedContentTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "image/png",
                "image/jpeg",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };

        /// <summary>
        /// Gate for one uploaded file: non-empty, within the size cap, and an allowed extension <b>and</b>
        /// content-type. Returns a friendly message naming the file when it is rejected, so the caller can show
        /// the user which file in a batch was the problem.
        /// </summary>
        public static (bool Ok, string? Message) Validate(IFormFile file)
        {
            var name = SafeFileName(file.FileName);

            if (file.Length <= 0)
            {
                return (false, $"\"{name}\" is empty.");
            }
            if (file.Length > MaxDocumentBytes)
            {
                return (false, $"\"{name}\" is larger than the 20 MB limit.");
            }

            var extension = Path.GetExtension(name);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension)
                || !AllowedContentTypes.Contains(file.ContentType ?? string.Empty))
            {
                return (false, $"\"{name}\" is not an allowed file type. Only PDF, PNG, JPEG and DOCX are accepted.");
            }

            return (true, null);
        }

        /// <summary>
        /// Normalises the browser-supplied file name for storage: strips any path a browser may have included,
        /// falls back to "file" when nothing usable is left, and bounds the result to <b>255</b> characters.
        /// <para>
        /// 255 is not arbitrary — <c>dbo.PatientDocument.FileName</c> and <c>dbo.StaffDocument.FileName</c> are
        /// both <c>VARCHAR(255)</c>. The trailing characters are kept rather than the leading ones so a
        /// truncated name still ends in its extension.
        /// </para>
        /// </summary>
        public static string SafeFileName(string? fileName)
        {
            var name = Path.GetFileName(fileName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                name = "file";
            }
            return name.Length > 255 ? name[^255..] : name;
        }

        /// <summary>
        /// Builds the key the document gets inside the private container:
        /// <c>{prefix}/{ownerId}/{guid:N}{ext}</c>. <paramref name="prefix"/> is <c>"patients"</c> or
        /// <c>"staff"</c>.
        /// <para>
        /// A fresh GUID is the file name, so two uploads of the same document never collide and nothing the user
        /// typed ends up in the key. The owner id is kept as a path segment because both real id shapes are safe
        /// path segments and readable in Storage Explorer: <c>Patient_ID</c> looks like <c>PAT-000042</c>
        /// (<c>spPatientBasic_Insert</c>) and <c>Staff_ID</c> like <c>END-00003</c> (<c>spStaff_Insert</c>), so
        /// files group by owner — e.g. <c>patients/PAT-000042/9f1c….pdf</c>, <c>staff/END-00003/4b7a….pdf</c>.
        /// </para>
        /// </summary>
        public static string BuildBlobName(string prefix, string ownerId, string originalFileName)
        {
            var extension = Path.GetExtension(SafeFileName(originalFileName)).ToLowerInvariant();
            return $"{prefix}/{ownerId}/{Guid.NewGuid():N}{extension}";
        }
    }
}
