using System.Net.Http.Headers;

namespace MyServiceAO.Services.ServiceTitan;

/// <summary>
/// Low-level HTTP client for the ServiceTitan API.
/// All ST endpoint URLs and field names are documented here.
///
/// Base URLs:
///   Production:   https://api.servicetitan.io
///   Auth:         https://auth.servicetitan.io/connect/token
///
/// All data endpoints follow: /{api}/v2/tenant/{tenantId}/{resource}
/// Export endpoints return: { hasMore, continueFrom, data[] }
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

    // ── Auth ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST https://auth.servicetitan.io/connect/token
    /// Grant type: client_credentials
    /// Returns access_token valid ~60 minutes.
    /// </summary>
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

    // ── Accounting ────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /accounting/v2/tenant/{tenantId}/export/invoices
    /// Query params: from (date string e.g. "2025-01-01"), includeRecentChanges=true
    ///
    /// Response fields used:
    ///   data[].total        — invoice total (string, parse as decimal)
    ///   data[].balance      — outstanding balance (string, parse as decimal). >0 = unpaid (AR)
    ///   data[].invoiceDate  — date-time string, used to bucket by month
    ///   data[].active       — bool, filter to true only
    ///   hasMore             — bool, paginate if true
    ///   continueFrom        — token for next page
    /// </summary>
    public async Task<string> GetInvoicesExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/accounting/v2/tenant/{stTenantId}/export/invoices?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={from}";

        _logger.LogInformation("[ST] GET invoices url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    // ── Job Planning & Management ─────────────────────────────────────────────

    /// <summary>
    /// GET /jpm/v2/tenant/{tenantId}/export/jobs
    /// Query params: from (date string), includeRecentChanges=true
    ///
    /// Response fields used:
    ///   data[].jobStatus    — string. Open WOs = NOT "Completed" or "Canceled"
    ///                         Known values: "Scheduled", "InProgress", "Hold", "Completed", "Canceled"
    ///   data[].active       — bool, filter to true only
    ///   hasMore / continueFrom — pagination
    /// </summary>
    public async Task<string> GetJobsExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/jpm/v2/tenant/{stTenantId}/export/jobs?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={from}";

        _logger.LogInformation("[ST] GET jobs url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    // ── Memberships ───────────────────────────────────────────────────────────

    /// <summary>
    /// GET /memberships/v2/tenant/{tenantId}/export/recurring-service-events
    /// Query params: from (date string), includeRecentChanges=true
    ///
    /// Response fields used:
    ///   data[].dueDate      — date-time string. Overdue = dueDate < today
    ///   data[].status       — string. Overdue = not "Completed" or "Canceled"
    ///                         Known values: "Scheduled", "Completed", "Canceled"
    ///   data[].active       — bool, filter to true only
    ///   hasMore / continueFrom — pagination
    /// </summary>
    public async Task<string> GetRecurringServiceEventsExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/memberships/v2/tenant/{stTenantId}/export/recurring-service-events?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from))
            url += $"&from={from}";

        _logger.LogInformation("[ST] GET recurring-service-events url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GetAsync(string accessToken, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[ST] Response status={Status} length={Length}", response.StatusCode, body.Length);

        return body;
    }
}