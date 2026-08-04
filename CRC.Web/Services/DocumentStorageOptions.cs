namespace CRC.Web.Services
{
    /// <summary>
    /// Settings for the document blob store, bound from the <c>DocumentStorage</c> section of configuration
    /// in <c>Program.cs</c>. Kept as an options class rather than read ad hoc so the storage service takes a
    /// single injected dependency and the two values have exactly one name each across every environment.
    /// <para>
    /// <see cref="ConnectionString"/> is <c>UseDevelopmentStorage=true</c> locally — the well-known shorthand
    /// for the <b>Azurite</b> emulator, so no Azure resource is needed to run or debug the portal and no real
    /// patient file leaves the developer's machine. In Azure App Service both values are overridden at runtime
    /// by the app settings <c>DocumentStorage__ConnectionString</c> and <c>DocumentStorage__ContainerName</c>
    /// (App Service expresses a configuration colon as a <b>double underscore</b>), which is why
    /// <c>appsettings.json</c> never has to carry a production secret.
    /// </para>
    /// </summary>
    public class DocumentStorageOptions
    {
        /// <summary>The configuration section these settings are bound from.</summary>
        public const string SectionName = "DocumentStorage";

        /// <summary>
        /// Azure Storage connection string. <c>UseDevelopmentStorage=true</c> for Azurite; the storage
        /// account's connection string in Azure. It carries the account key, which is what lets the app sign
        /// the read SAS URLs that downloads depend on.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Name of the single <b>private</b> container that holds every document — patient documents under the
        /// <c>patients/</c> prefix and staff documents under <c>staff/</c>.
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;
    }
}
