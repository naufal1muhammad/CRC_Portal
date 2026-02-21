namespace CRC.Api.Security;

public sealed class ApiKeyAuthenticationOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";

    /// <summary>
    /// The header name containing the API key. Default: X-Api-Key
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// The expected API key (set via configuration / secret store).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
