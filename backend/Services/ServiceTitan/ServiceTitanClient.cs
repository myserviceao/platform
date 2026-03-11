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

    public async Task<Dictionary<long, string>> GetJobTypeMapAsync(string accessToken, string stTenantId)
    {
        var map = new Dictionary<long, string>();
        int page = 1;
        while (true)
        {
            var url = $"{BaseUrl}/jpm/v2/tenant/{stTenantId}/job-types?page={page}&pageSize=200";
            var json = await GetAsync(accessToken, url);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) break;
            foreach (var item in data.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt64();
                var name = item.GetProperty("name").GetString() ?? "Unknown";
                map[id] = name;
            }
            page++;
        }
        _logger.LogInformation("[ST] Job type map loaded count={Count}", map.Count);
        return map;
    }

    public async Task<Dictionary<long, string>> GetCustomerNameMapAsync(string accessToken, string stTenantId)
    {
        var map = new Dictionary<long, string>();
        string? continueFrom = null;
        do
        {
            var url = $"{BaseUrl}/crm/v2/tenant/{stTenantId}/export/customers";
            if (continueFrom != null) url += $"?from={Uri.EscapeDataString(continueFrom)}";
            var json = await GetAsync(accessToken, url);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) break;
            foreach (var cust in data.EnumerateArray())
            {
                if (cust.TryGetProperty("id", out var idProp) && cust.TryGetProperty("name", out var nameProp))
                    map[idProp.GetInt64()] = nameProp.GetString() ?? "Unknown";
            }
            var hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
            continueFrom = hasMore && root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;
            if (!hasMore) break;
        } while (continueFrom != null);
        _logger.LogInformation("[ST] Customer name map loaded count={Count}", map.Count);
        return map;
    }

    public async Task<string> GetInvoicesExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/accounting/v2/tenant/{stTenantId}/export/invoices?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from)) url += $"&from={Uri.EscapeDataString(from)}";
        _logger.LogInformation("[ST] GET invoices url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    public async Task<string> GetJobsExportAsync(string accessToken, string stTenantId, string? from = null)
    {
        var url = $"{BaseUrl}/jpm/v2/tenant/{stTenantId}/export/jobs?includeRecentChanges=true";
        if (!string.IsNullOrEmpty(from)) url += $"&from={Uri.EscapeDataString(from)}";
        _logger.LogInformation("[ST] GET jobs url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    public async Task<string> GetAppointmentsAsync(string accessToken, string stTenantId,
        DateTime startsOnOrAfter, DateTime startsOnOrBefore, int page = 1, int pageSize = 500)
    {
        var from = startsOnOrAfter.ToString("O");
        var to   = startsOnOrBefore.ToString("O");
        var url  = $"{BaseUrl}/jpm/v2/tenant/{stTenantId}/appointments"
                 + $"?pageSize={pageSize}&page={page}"
                 + $"&startsOnOrAfter={Uri.EscapeDataString(from)}"
                 + $"&startsOnOrBefore={Uri.EscapeDataString(to)}";
        _logger.LogInformation("[ST] GET appointments url={Url}", url);
        return await GetAsync(accessToken, url);
    }

    public async Task<string> GetAppointmentAssignmentsAsync(string accessToken, string stTenantId,
        IEnumerable<long> appointmentIds, int pageSize = 500)
    {
        var ids = string.Join(",", appointmentIds);
        var url = $"{BaseUrl}/dispatch/v2/tenant/{stTenantId}/appointment-assignments"
                + $"?appointmentIds={ids}&pageSize={pageSize}&active=Any";
        _logger.LogInformation("[ST] GET appt-assignments url={Url}", url);
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
