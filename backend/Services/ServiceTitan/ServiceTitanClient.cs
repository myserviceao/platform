using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace MyServiceAO.Services.ServiceTitan;

public class ServiceTitanClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ServiceTitanClient> _logger;
    private readonly string _appKey;

    private const string BaseUrl = "https://api.servicetitan.io";
    private const string AuthUrl = "https://auth.servicetitan.io/connect/token";

    public ServiceTitanClient(HttpClient http, ILogger<ServiceTitanClient> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _appKey = config["ST_APP_KEY"] ?? throw new InvalidOperationException("ST_APP_KEY is not configured");
    }

    public async Task<string?> GetAccessTokenAsync(string clientId, string clientSecret)
    {
        _logger.LogInformation("[ST] Requesting token clientId={ClientId}", clientId);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        var response = await _http.PostAsync(AuthUrl, form);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[ST] Token request failed status={Status} body={Body}", response.StatusCode, body);
            return null;
        }

        var json = System.Text.Json.JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString();
    }

    public async Task<string> GetInvoicesExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/accounting/v2/tenant/{stTenantId}/export/invoices?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={Uri.EscapeDataString(from)}";
        _logger.LogInformation("[ST] GET invoices url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    public async Task<string> GetJobsExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/jpm/v2/tenant/{stTenantId}/export/jobs?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={Uri.EscapeDataString(from)}";
        _logger.LogInformation("[ST] GET jobs url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    public async Task<string> GetRecurringServiceEventsExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/memberships/v2/tenant/{stTenantId}/export/recurring-service-events?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={Uri.EscapeDataString(from)}";
        _logger.LogInformation("[ST] GET recurring-service-events url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    private async Task<string> GetAsync(string accessToken, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("ST-App-Key", _appKey);

        var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[ST] Response status={Status} length={Length}", response.StatusCode, body.Length);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"ST API error {(int)response.StatusCode}: {body}");

        return body;
    }
}
