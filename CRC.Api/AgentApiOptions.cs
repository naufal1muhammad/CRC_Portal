namespace CRC.Api
{
    /// <summary>
    /// The Agent API's configuration, bound from the <c>Agent</c> section in
    /// <c>CRC.Web/Program.cs</c>. One value, because the shared key is the whole of this surface's access
    /// control (CoreFlow.md §13.0). Kept as an options class rather than read ad hoc so
    /// <see cref="Infrastructure.AgentApiKeyFilter"/> takes a single injected dependency and the setting
    /// has exactly one name in every environment — the same shape <c>DocumentStorageOptions</c> follows.
    /// </summary>
    public class AgentApiOptions
    {
        /// <summary>The configuration section these settings are bound from.</summary>
        public const string SectionName = "Agent";

        /// <summary>
        /// The shared secret an agent request presents in the <c>X-Agent-Key</c> header.
        /// <para>
        /// 🔴 <b>The real key is an Azure App Service app setting — <c>Agent__ApiKey</c>, with TWO
        /// underscores — and never lives in <c>appsettings.json</c>.</b> Both <c>appsettings.json</c> and
        /// <c>appsettings.Development.json</c> are in source control, so the first carries an empty
        /// placeholder and the second a development-only value that is worthless if it leaks. This is the
        /// rule <c>DocumentStorage__ConnectionString</c> already follows; <c>DOCUMENTSTORAGE.md</c>
        /// explains the underscore, and it is worth repeating because the failure is silent: App Service
        /// expresses a configuration colon as a <b>double</b> underscore, and a setting written
        /// <c>Agent_ApiKey</c> binds to nothing at all, leaves this property empty, and the filter then
        /// correctly refuses every request with a 401.
        /// </para>
        /// <para>
        /// Empty is the fail-closed default and is treated as a misconfiguration by the filter — never as
        /// "no key required".
        /// </para>
        /// </summary>
        public string ApiKey { get; set; } = "";
    }
}
