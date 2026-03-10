namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Isolated ServiceTitan API client.
/// All ST field names and response shapes are logged clearly here.
/// OAuth2 token management is handled per-tenant.
/// </summary>
public class ServiceTitanClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ServiceTitanClient> _logger;

    private const string BaseUrl = "https://api.servicetitan.io";
    private const string AuthUrl = "https://auth.servicetitan.io/connect/token";

    public ServiceTitanClient(HttpClient http, ILogger<ServiceTitanClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a fresh OAuth2 access token using client credentials.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(string clientId, string clientSecret)
    {
        _logger.LogInformation("[ST] Requesting access token for clientId={ClientId}", clientId);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        var response = await _http.PostAsync(AuthUrl, form);
        var body = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[ST] Token response status={Status} body={Body}", response.StatusCode, body);

        if (!response.IsSuccessStatusCode) return null;

        var json = System.Text.Json.JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString();
    }

    /// <summary>
    /// Fetches jobs from the export endpoint.
    /// ST field names: id, number, status, customerId, locationId, etc.
    /// </summary>
    public async Task<string> GetJobsAsync(string accessToken, string tenantId, string? continueFrom = null)
    {
        var url = $"{BaseUrl}/jpm/v2/tenant/{tenantId}/export/jobs";
        if (!string.IsNullOrEmpty(continueFrom))
            url += $"?continueFrom={continueFrom}";

        _logger.LogInformation("[ST] GET {Url}", url);

        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[ST] Jobs response status={Status} length={Length}", response.StatusCode, body.Length);

        return body;
    }
}
