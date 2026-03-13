using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services;

public class OutreachService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OutreachService> _logger;

    public OutreachService(AppDbContext db, ILogger<OutreachService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Template Rendering ──

    public string RenderTemplate(string template, Dictionary<string, string> data)
    {
        var result = template;
        foreach (var (key, value) in data)
            result = result.Replace($"{{{{{key}}}}}", value);
        return result;
    }

    // ── Default Template Seeding ──

    public async Task EnsureDefaultTemplatesAsync(int tenantId)
    {
        var existing = await _db.OutreachTemplates
            .AnyAsync(t => t.TenantId == tenantId && t.IsDefault);
        if (existing) return;

        var defaults = new List<OutreachTemplate>
        {
            // PM Reminder
            new() { TenantId = tenantId, Name = "PM Reminder - Email", Type = "pm_reminder", Channel = "email", IsDefault = true,
                Subject = "Time for your maintenance checkup!",
                Body = "Hi {{customerName}},\n\nIt's been a while since your last preventive maintenance service ({{lastPmDate}}). Regular maintenance keeps your system running efficiently and prevents costly breakdowns.\n\nWould you like to schedule your tune-up? Give us a call or reply to this email.\n\nBest,\n{{companyName}}" },
            new() { TenantId = tenantId, Name = "PM Reminder - SMS", Type = "pm_reminder", Channel = "sms", IsDefault = true,
                Body = "Hi {{customerName}}, your HVAC maintenance is {{daysOverdue}} days overdue. Call us to schedule your tune-up! - {{companyName}}" },

            // Post-Service
            new() { TenantId = tenantId, Name = "Post-Service Follow-up - Email", Type = "post_service", Channel = "email", IsDefault = true,
                Subject = "How was your recent service?",
                Body = "Hi {{customerName}},\n\nThank you for choosing us for your recent {{jobType}} service on {{completionDate}}. Your technician {{technicianName}} enjoyed working with you.\n\nIf you have any questions or concerns about the work, please don't hesitate to reach out.\n\nBest,\n{{companyName}}" },
            new() { TenantId = tenantId, Name = "Post-Service Follow-up - SMS", Type = "post_service", Channel = "sms", IsDefault = true,
                Body = "Hi {{customerName}}, thanks for your recent {{jobType}} service! Questions? Reply or call us. - {{companyName}}" },

            // Win-Back
            new() { TenantId = tenantId, Name = "Win-Back - Email", Type = "win_back", Channel = "email", IsDefault = true,
                Subject = "We miss you! Time for a checkup?",
                Body = "Hi {{customerName}},\n\nIt's been {{monthsSinceService}} months since your last service with us ({{lastServiceDate}}). We'd love to help keep your home comfortable.\n\nAs a valued customer, we'd like to offer you a special rate on your next service call. Give us a call to schedule!\n\nBest,\n{{companyName}}" },
            new() { TenantId = tenantId, Name = "Win-Back - SMS", Type = "win_back", Channel = "sms", IsDefault = true,
                Body = "Hi {{customerName}}, it's been {{monthsSinceService}} months since your last visit! Call us to schedule service. - {{companyName}}" },

            // Seasonal
            new() { TenantId = tenantId, Name = "Seasonal Campaign - Email", Type = "seasonal", Channel = "email", IsDefault = true,
                Subject = "Get ready for the season ahead!",
                Body = "Hi {{customerName}},\n\nThe season is changing and it's a great time to make sure your HVAC system is ready. We're offering seasonal tune-ups to keep your home comfortable all year round.\n\nCall us today to schedule your appointment!\n\nBest,\n{{companyName}}" },
            new() { TenantId = tenantId, Name = "Seasonal Campaign - SMS", Type = "seasonal", Channel = "sms", IsDefault = true,
                Body = "Hi {{customerName}}, seasonal tune-up time! Call us to schedule and keep your system running smoothly. - {{companyName}}" },

            // AR Reminder
            new() { TenantId = tenantId, Name = "AR Reminder - Email", Type = "ar_reminder", Channel = "email", IsDefault = true,
                Subject = "Outstanding balance on your account",
                Body = "Hi {{customerName}},\n\nThis is a friendly reminder that you have an outstanding balance of {{totalOwed}} with {{invoiceCount}} open invoice(s). The oldest invoice is {{oldestInvoiceAge}} days past due.\n\nPlease contact us to arrange payment or if you have any questions about your account.\n\nThank you,\n{{companyName}}" },
            new() { TenantId = tenantId, Name = "AR Reminder - SMS", Type = "ar_reminder", Channel = "sms", IsDefault = true,
                Body = "Hi {{customerName}}, you have an outstanding balance of {{totalOwed}} ({{oldestInvoiceAge}} days). Please contact us to arrange payment. - {{companyName}}" },
        };

        _db.OutreachTemplates.AddRange(defaults);
        await _db.SaveChangesAsync();
        _logger.LogInformation("[Outreach] Seeded {Count} default templates for tenant {TenantId}", defaults.Count, tenantId);
    }

    // ── Outreach Generation ──

    public async Task GenerateOutreachItemsAsync(int tenantId)
    {
        await EnsureDefaultTemplatesAsync(tenantId);

        var settings = await _db.OutreachSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        var winBackMonths = settings?.WinBackThresholdMonths ?? 12;
        var postServiceHours = settings?.PostServiceDelayHours ?? 48;

        var templates = await _db.OutreachTemplates
            .Where(t => t.TenantId == tenantId)
            .ToListAsync();

        var tenant = await _db.Tenants.FindAsync(tenantId);
        var companyName = tenant?.Name ?? "";

        int generated = 0;
        generated += await GeneratePmRemindersAsync(tenantId, templates, companyName);
        generated += await GeneratePostServiceAsync(tenantId, templates, companyName, postServiceHours);
        generated += await GenerateWinBackAsync(tenantId, templates, companyName, winBackMonths);

        _logger.LogInformation("[Outreach] Generated {Count} outreach items for tenant {TenantId}", generated, tenantId);
    }

    private async Task<bool> HasRecentItem(int tenantId, string type, int customerId, int? jobId = null)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var query = _db.OutreachItems
            .Where(i => i.TenantId == tenantId && i.Type == type && i.CustomerId == customerId
                && (i.Status == "pending" || i.Status == "sent")
                && i.CreatedAt >= cutoff);

        if (type == "post_service" && jobId.HasValue)
            query = query.Where(i => i.JobId == jobId.Value);

        return await query.AnyAsync();
    }

    private async Task<int> GeneratePmRemindersAsync(int tenantId, List<OutreachTemplate> templates, string companyName)
    {
        var pmTemplates = templates.Where(t => t.Type == "pm_reminder").ToList();
        if (pmTemplates.Count == 0) return 0;

        var pmCustomers = await _db.PmCustomers
            .Where(p => p.TenantId == tenantId && (p.PmStatus == "Overdue" || p.PmStatus == "ComingDue"))
            .ToListAsync();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.StCustomerId, c => c);

        int count = 0;
        foreach (var pm in pmCustomers)
        {
            if (!customers.TryGetValue(pm.StCustomerId, out var customer)) continue;
            if (await HasRecentItem(tenantId, "pm_reminder", customer.Id)) continue;

            var daysOverdue = pm.LastPmDate.HasValue
                ? (int)(DateTime.UtcNow - pm.LastPmDate.Value).TotalDays
                : 0;

            var data = new Dictionary<string, string>
            {
                ["customerName"] = customer.Name,
                ["companyName"] = companyName,
                ["phone"] = customer.Phone ?? "",
                ["lastPmDate"] = pm.LastPmDate?.ToString("MMM d, yyyy") ?? "N/A",
                ["daysOverdue"] = daysOverdue.ToString(),
            };

            foreach (var tmpl in pmTemplates)
            {
                _db.OutreachItems.Add(new OutreachItem
                {
                    TenantId = tenantId,
                    CustomerId = customer.Id,
                    Type = "pm_reminder",
                    Channel = tmpl.Channel,
                    Subject = tmpl.Subject != null ? RenderTemplate(tmpl.Subject, data) : null,
                    Body = RenderTemplate(tmpl.Body, data),
                });
                count++;
            }
        }

        await _db.SaveChangesAsync();
        return count;
    }

    private async Task<int> GeneratePostServiceAsync(int tenantId, List<OutreachTemplate> templates, string companyName, int delayHours)
    {
        var postTemplates = templates.Where(t => t.Type == "post_service").ToList();
        if (postTemplates.Count == 0) return 0;

        var cutoff = DateTime.UtcNow.AddHours(-delayHours);
        var completedJobs = await _db.Jobs
            .Where(j => j.TenantId == tenantId && j.Status == "Completed" && j.CompletedOn != null && j.CompletedOn >= cutoff)
            .ToListAsync();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.StCustomerId, c => c);

        int count = 0;
        foreach (var job in completedJobs)
        {
            if (!customers.TryGetValue(job.StCustomerId, out var customer)) continue;
            if (await HasRecentItem(tenantId, "post_service", customer.Id, job.Id)) continue;

            var data = new Dictionary<string, string>
            {
                ["customerName"] = customer.Name,
                ["companyName"] = companyName,
                ["phone"] = customer.Phone ?? "",
                ["jobType"] = job.JobTypeName ?? "service",
                ["technicianName"] = job.TechnicianName ?? "our team",
                ["completionDate"] = job.CompletedOn?.ToString("MMM d, yyyy") ?? "",
            };

            foreach (var tmpl in postTemplates)
            {
                _db.OutreachItems.Add(new OutreachItem
                {
                    TenantId = tenantId,
                    CustomerId = customer.Id,
                    JobId = job.Id,
                    Type = "post_service",
                    Channel = tmpl.Channel,
                    Subject = tmpl.Subject != null ? RenderTemplate(tmpl.Subject, data) : null,
                    Body = RenderTemplate(tmpl.Body, data),
                });
                count++;
            }
        }

        await _db.SaveChangesAsync();
        return count;
    }

    private async Task<int> GenerateWinBackAsync(int tenantId, List<OutreachTemplate> templates, string companyName, int thresholdMonths)
    {
        var wbTemplates = templates.Where(t => t.Type == "win_back").ToList();
        if (wbTemplates.Count == 0) return 0;

        var cutoff = DateTime.UtcNow.AddMonths(-thresholdMonths);

        var lastJobPerCustomer = await _db.Jobs
            .Where(j => j.TenantId == tenantId && j.Status == "Completed" && j.CompletedOn != null)
            .GroupBy(j => j.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, LastCompleted = g.Max(j => j.CompletedOn) })
            .Where(x => x.LastCompleted < cutoff)
            .ToListAsync();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.StCustomerId, c => c);

        var spendMap = await _db.Invoices
            .Where(i => i.TenantId == tenantId)
            .GroupBy(i => i.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, Total = g.Sum(i => i.TotalAmount) })
            .ToDictionaryAsync(x => x.StCustomerId, x => x.Total);

        var ranked = lastJobPerCustomer
            .OrderByDescending(x => spendMap.GetValueOrDefault(x.StCustomerId, 0))
            .ToList();

        int count = 0;
        foreach (var entry in ranked)
        {
            if (!customers.TryGetValue(entry.StCustomerId, out var customer)) continue;
            if (await HasRecentItem(tenantId, "win_back", customer.Id)) continue;

            var monthsSince = entry.LastCompleted.HasValue
                ? (int)((DateTime.UtcNow - entry.LastCompleted.Value).TotalDays / 30)
                : thresholdMonths;

            var data = new Dictionary<string, string>
            {
                ["customerName"] = customer.Name,
                ["companyName"] = companyName,
                ["phone"] = customer.Phone ?? "",
                ["lastServiceDate"] = entry.LastCompleted?.ToString("MMM d, yyyy") ?? "N/A",
                ["monthsSinceService"] = monthsSince.ToString(),
                ["lifetimeSpend"] = spendMap.GetValueOrDefault(entry.StCustomerId, 0).ToString("C0"),
            };

            foreach (var tmpl in wbTemplates)
            {
                _db.OutreachItems.Add(new OutreachItem
                {
                    TenantId = tenantId,
                    CustomerId = customer.Id,
                    Type = "win_back",
                    Channel = tmpl.Channel,
                    Subject = tmpl.Subject != null ? RenderTemplate(tmpl.Subject, data) : null,
                    Body = RenderTemplate(tmpl.Body, data),
                });
                count++;
            }
        }

        await _db.SaveChangesAsync();
        return count;
    }

    // ── Seasonal Campaign Generation ──

    public async Task<int> GenerateSeasonalCampaignAsync(int tenantId, int templateId, string segment, int? monthsThreshold = null)
    {
        var template = await _db.OutreachTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId);
        if (template == null) return 0;

        var tenant = await _db.Tenants.FindAsync(tenantId);
        var companyName = tenant?.Name ?? "";

        IQueryable<Customer> query = _db.Customers.Where(c => c.TenantId == tenantId);

        if (segment == "with_pm")
        {
            var pmCustomerIds = await _db.PmCustomers
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.StCustomerId)
                .ToListAsync();
            query = query.Where(c => pmCustomerIds.Contains(c.StCustomerId));
        }
        else if (segment == "without_pm")
        {
            var pmCustomerIds = await _db.PmCustomers
                .Where(p => p.TenantId == tenantId)
                .Select(p => p.StCustomerId)
                .ToListAsync();
            query = query.Where(c => !pmCustomerIds.Contains(c.StCustomerId));
        }
        else if (segment == "no_recent_service" && monthsThreshold.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-monthsThreshold.Value);
            var recentCustomerIds = await _db.Jobs
                .Where(j => j.TenantId == tenantId && j.CompletedOn >= cutoff)
                .Select(j => j.StCustomerId)
                .Distinct()
                .ToListAsync();
            query = query.Where(c => !recentCustomerIds.Contains(c.StCustomerId));
        }

        var customers = await query.ToListAsync();
        int count = 0;

        foreach (var customer in customers)
        {
            if (await HasRecentItem(tenantId, "seasonal", customer.Id)) continue;

            var data = new Dictionary<string, string>
            {
                ["customerName"] = customer.Name,
                ["companyName"] = companyName,
                ["phone"] = customer.Phone ?? "",
            };

            _db.OutreachItems.Add(new OutreachItem
            {
                TenantId = tenantId,
                CustomerId = customer.Id,
                Type = "seasonal",
                Channel = template.Channel,
                Subject = template.Subject != null ? RenderTemplate(template.Subject, data) : null,
                Body = RenderTemplate(template.Body, data),
            });
            count++;
        }

        await _db.SaveChangesAsync();
        return count;
    }

    // ── Send / Dismiss ──

    public async Task<bool> SendItemAsync(int itemId, int tenantId)
    {
        var item = await _db.OutreachItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId);
        if (item == null) return false;

        var settings = await _db.OutreachSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        try
        {
            if (item.Channel == "email")
            {
                if (string.IsNullOrEmpty(settings?.ResendApiKey) || string.IsNullOrEmpty(settings?.ResendFromEmail))
                    throw new Exception("Email not configured. Set Resend API key and from address in Outreach Settings.");

                var customer = await _db.Customers.FindAsync(item.CustomerId);
                if (string.IsNullOrEmpty(customer?.Email))
                    throw new Exception("Customer has no email address.");

                await SendEmailViaResendAsync(settings.ResendApiKey, settings.ResendFromEmail, customer.Email, item.Subject ?? "", item.Body);
            }
            else if (item.Channel == "sms")
            {
                if (string.IsNullOrEmpty(settings?.TwilioAccountSid) || string.IsNullOrEmpty(settings?.TwilioAuthToken) || string.IsNullOrEmpty(settings?.TwilioFromPhone))
                    throw new Exception("SMS not configured. Set Twilio credentials in Outreach Settings.");

                var customer = await _db.Customers.FindAsync(item.CustomerId);
                if (string.IsNullOrEmpty(customer?.Phone))
                    throw new Exception("Customer has no phone number.");

                await SendSmsViaTwilioAsync(settings.TwilioAccountSid, settings.TwilioAuthToken, settings.TwilioFromPhone, customer.Phone, item.Body);
            }

            item.Status = "sent";
            item.SentAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            item.Status = "failed";
            item.FailureReason = ex.Message;
            item.UpdatedAt = DateTime.UtcNow;
            _logger.LogWarning(ex, "[Outreach] Failed to send item {ItemId}", itemId);
        }

        await _db.SaveChangesAsync();
        return item.Status == "sent";
    }

    public async Task DismissItemAsync(int itemId, int tenantId)
    {
        var item = await _db.OutreachItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId);
        if (item == null) return;
        item.Status = "dismissed";
        item.DismissedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<(int sent, int failed)> BulkSendAsync(List<int> itemIds, int tenantId)
    {
        int sent = 0, failed = 0;
        foreach (var id in itemIds)
        {
            if (await SendItemAsync(id, tenantId)) sent++;
            else failed++;
        }
        return (sent, failed);
    }

    public async Task BulkDismissAsync(List<int> itemIds, int tenantId)
    {
        var items = await _db.OutreachItems
            .Where(i => itemIds.Contains(i.Id) && i.TenantId == tenantId && i.Status == "pending")
            .ToListAsync();
        foreach (var item in items)
        {
            item.Status = "dismissed";
            item.DismissedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    // ── Email via Resend ──

    private async Task SendEmailViaResendAsync(string apiKey, string from, string to, string subject, string body)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        var payload = new
        {
            from,
            to = new[] { to },
            subject,
            text = body,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await http.PostAsync("https://api.resend.com/emails", content);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Resend API error: {err}");
        }
    }

    // ── SMS via Twilio ──

    private async Task SendSmsViaTwilioAsync(string accountSid, string authToken, string from, string to, string body)
    {
        using var http = new HttpClient();
        var authBytes = System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("From", from),
            new KeyValuePair<string, string>("To", to),
            new KeyValuePair<string, string>("Body", body),
        });
        var response = await http.PostAsync($"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json", formData);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Twilio API error: {err}");
        }
    }

    // ── Credential Masking ──

    public static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8) return value != null ? "****" : null;
        return "****" + value[^4..];
    }
}
