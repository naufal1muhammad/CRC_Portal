namespace CRC.Api
{
    /// <summary>
    /// The Agent API's configuration, bound from the <c>Agent</c> section in
    /// <c>CRC.Web/Program.cs</c>. One setting, because the shared key is the whole of this surface's access
    /// control (CoreFlow.md §13.0). Kept as an options class rather than read ad hoc so
    /// <see cref="Infrastructure.AgentApiKeyFilter"/> takes a single injected dependency and the setting
    /// has exactly one name in every environment — the same shape <c>DocumentStorageOptions</c> follows.
    /// </summary>
    public class AgentApiOptions
    {
        /// <summary>The configuration section these settings are bound from.</summary>
        public const string SectionName = "Agent";

        /// <summary>
        /// The shared secrets an agent request may present in the <c>X-Agent-Key</c> header. <b>Any member
        /// is accepted</b>; <see cref="Infrastructure.AgentApiKeyFilter"/> compares the supplied value
        /// against every one of them in fixed time.
        /// <para>
        /// <b>An ARRAY is what makes a key rotation overlappable.</b> With one key there is no version and
        /// no overlap window, so rotating it is a hard cutover — the external caller's credential has to
        /// change in the same minute the app setting does. With two valid at once, a rotation becomes: add
        /// the new key, move the caller over, remove the old one (CoreFlow.md §13.6). It is also the half
        /// that per-caller keys are built on later (§13.7) — what is still missing there is a NAME on each
        /// key, not the array.
        /// </para>
        /// <para>
        /// 🔴 <b>The real keys are Azure App Service app settings — <c>Agent__ApiKey__0</c>,
        /// <c>Agent__ApiKey__1</c>, … — and never live in <c>appsettings.json</c>.</b> Both
        /// <c>appsettings.json</c> and <c>appsettings.Development.json</c> are in source control, so the
        /// first carries an empty array and the second a single development-only value that is worthless
        /// if it leaks. TWO underscores between every segment: App Service expresses a configuration colon
        /// as a <b>double</b> underscore, so <c>Agent__ApiKey__0</c> binds to <c>Agent:ApiKey:0</c> while
        /// <c>Agent_ApiKey_0</c> binds to nothing at all. This is the rule
        /// <c>DocumentStorage__ConnectionString</c> already follows and <c>DOCUMENTSTORAGE.md</c> owns; the
        /// index is simply a further segment.
        /// </para>
        /// <para>
        /// 🔴 <b>A SCALAR <c>Agent__ApiKey</c> app setting now binds to NOTHING.</b> It bound to this
        /// property while it was a <c>string</c>; the moment it is a <c>string[]</c> the same setting
        /// matches no element and this array comes back empty. That is a one-time hard cutover and it is
        /// why <b>the indexed setting must be added BEFORE the web app is published</b> — publish first and
        /// every agent request answers 401 until the setting is fixed. The failure is a clean 401 with an
        /// error in <c>app-*.log</c> naming the setting, not an open door, which is exactly the property
        /// the next paragraph exists to protect.
        /// </para>
        /// <para>
        /// <b>Empty or absent is the fail-closed default, and it never means "no key required".</b> A null
        /// array, an array with no members, and an array whose only members are empty or whitespace are all
        /// "not configured", and the filter answers 401 to all three. It defaults to an EMPTY ARRAY rather
        /// than null so that nothing downstream has to guard for one — but the filter guards anyway,
        /// because configuration binding is not the only thing that can set this property.
        /// </para>
        /// </summary>
        public string[] ApiKey { get; set; } = Array.Empty<string>();
    }
}
