using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services;

public class ArAlertsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ArAlertsService> _logger;

    public ArAlertsService(AppDbContext db, ILogger<ArAlertsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<object> GetAgingSummaryAsync(int tenantId)
    {
        var now = DateTime.UtcNow;
        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId && i.BalanceRemaining > 0)
            .Select(i => new { i.StCustomerId, i.BalanceRemaining, i.InvoiceDate })
            .ToListAsync();

        decimal total = 0, d15 = 0, d30 = 0, d60 = 0, d90 = 0;
        int totalCount = 0, c15 = 0, c30 = 0, c60 = 0, c90 = 0;

        foreach (var inv in invoices)
        {
            var age = (now - inv.InvoiceDate).TotalDays;
            total += inv.BalanceRemaining;
            totalCount++;
            if (age >= 15) { d15 += inv.BalanceRemaining; c15++; }
            if (age >= 30) { d30 += inv.BalanceRemaining; c30++; }
            if (age >= 60) { d60 += inv.BalanceRemaining; c60++; }
            if (age >= 90) { d90 += inv.BalanceRemaining; c90++; }
        }

        // Count of unique customers with 15+ day overdue invoices (for badge)
        var overdueCustomerCount = invoices
            .Where(i => (now - i.InvoiceDate).TotalDays >= 15)
            .Select(i => i.StCustomerId)
            .Distinct()
            .Count();

        return new
        {
            total, totalCount,
            days15 = new { amount = d15, count = c15 },
            days30 = new { amount = d30, count = c30 },
            days60 = new { amount = d60, count = c60 },
            days90 = new { amount = d90, count = c90 },
            overdueCustomerCount,
        };
    }

    public async Task<object> GetCustomerArListAsync(int tenantId, string? search, string? bucket, string? status, int page, int pageSize)
    {
        var now = DateTime.UtcNow;

        // Get all open invoices
        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId && i.BalanceRemaining > 0)
            .ToListAsync();

        // Group by customer
        var grouped = invoices
            .GroupBy(i => i.StCustomerId)
            .Select(g => new
            {
                StCustomerId = g.Key,
                TotalOwed = g.Sum(i => i.BalanceRemaining),
                InvoiceCount = g.Count(),
                OldestDays = (int)g.Max(i => (now - i.InvoiceDate).TotalDays),
            })
            .ToList();

        // Apply bucket filter
        if (bucket == "15") grouped = grouped.Where(g => g.OldestDays >= 15).ToList();
        else if (bucket == "30") grouped = grouped.Where(g => g.OldestDays >= 30).ToList();
        else if (bucket == "60") grouped = grouped.Where(g => g.OldestDays >= 60).ToList();
        else if (bucket == "90") grouped = grouped.Where(g => g.OldestDays >= 90).ToList();

        // Load customers
        var stIds = grouped.Select(g => g.StCustomerId).ToList();
        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId && stIds.Contains(c.StCustomerId))
            .ToDictionaryAsync(c => c.StCustomerId);

        // Apply search filter
        if (!string.IsNullOrEmpty(search))
        {
            var q = search.ToLower();
            grouped = grouped.Where(g =>
                customers.TryGetValue(g.StCustomerId, out var c) &&
                c.Name.ToLower().Contains(q)
            ).ToList();
        }

        // Load AR statuses
        var customerIds = grouped
            .Where(g => customers.ContainsKey(g.StCustomerId))
            .Select(g => customers[g.StCustomerId].Id)
            .ToList();

        var statuses = await _db.ArStatuses
            .Where(s => s.TenantId == tenantId && customerIds.Contains(s.CustomerId))
            .ToDictionaryAsync(s => s.CustomerId);

        // Apply status filter
        if (!string.IsNullOrEmpty(status) && status != "all")
        {
            grouped = grouped.Where(g =>
            {
                if (!customers.TryGetValue(g.StCustomerId, out var c)) return false;
                statuses.TryGetValue(c.Id, out var arStatus);
                var st = arStatus?.Status ?? "active";
                return st == status;
            }).ToList();
        }

        // Load last contact dates
        var lastContacts = await _db.ArContactLogs
            .Where(l => l.TenantId == tenantId && customerIds.Contains(l.CustomerId))
            .GroupBy(l => l.CustomerId)
            .Select(g => new { CustomerId = g.Key, LastContact = g.Max(l => l.CreatedAt) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.LastContact);

        var total = grouped.Count;

        // Sort by oldest first, paginate
        var paged = grouped
            .OrderByDescending(g => g.OldestDays)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = paged.Select(g =>
        {
            customers.TryGetValue(g.StCustomerId, out var c);
            var cId = c?.Id ?? 0;
            statuses.TryGetValue(cId, out var arStatus);
            lastContacts.TryGetValue(cId, out var lastContact);
            return new
            {
                customerId = cId,
                customerName = c?.Name,
                customerPhone = c?.Phone,
                customerEmail = c?.Email,
                totalOwed = g.TotalOwed,
                invoiceCount = g.InvoiceCount,
                oldestDays = g.OldestDays,
                status = arStatus?.Status ?? "active",
                paymentPlanAmount = arStatus?.PaymentPlanAmount,
                paymentPlanNote = arStatus?.PaymentPlanNote,
                lastContact = lastContact,
            };
        });

        return new { items, total, page, pageSize };
    }

    public async Task<object?> GetCustomerDetailAsync(int tenantId, int customerId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId);
        if (customer == null) return null;

        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId && i.StCustomerId == customer.StCustomerId && i.BalanceRemaining > 0)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var contactLogs = await _db.ArContactLogs
            .Where(l => l.TenantId == tenantId && l.CustomerId == customerId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var arStatus = await _db.ArStatuses
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CustomerId == customerId);

        var now = DateTime.UtcNow;

        return new
        {
            customerId,
            customerName = customer.Name,
            customerPhone = customer.Phone,
            customerEmail = customer.Email,
            status = arStatus?.Status ?? "active",
            paymentPlanAmount = arStatus?.PaymentPlanAmount,
            paymentPlanNote = arStatus?.PaymentPlanNote,
            invoices = invoices.Select(i => new
            {
                stInvoiceId = i.StInvoiceId,
                invoiceDate = i.InvoiceDate,
                totalAmount = i.TotalAmount,
                balanceRemaining = i.BalanceRemaining,
                ageDays = (int)(now - i.InvoiceDate).TotalDays,
            }),
            contactLogs = contactLogs.Select(l => new
            {
                l.Id, l.ContactType, l.Outcome, l.Notes, l.FollowUpDate, l.CreatedAt,
            }),
        };
    }

    public async Task LogContactAsync(int tenantId, int customerId, string contactType, string outcome, string? notes, DateTime? followUpDate)
    {
        _db.ArContactLogs.Add(new ArContactLog
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ContactType = contactType,
            Outcome = outcome,
            Notes = notes,
            FollowUpDate = followUpDate,
        });
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int tenantId, int customerId, string status, decimal? paymentPlanAmount, string? paymentPlanNote)
    {
        var existing = await _db.ArStatuses
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CustomerId == customerId);

        if (existing == null)
        {
            existing = new ArStatus { TenantId = tenantId, CustomerId = customerId };
            _db.ArStatuses.Add(existing);
        }

        existing.Status = status;
        existing.PaymentPlanAmount = paymentPlanAmount;
        existing.PaymentPlanNote = paymentPlanNote;
        existing.StatusChangedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── AR Reminder Generation (called during sync) ──

    public async Task GenerateArRemindersAsync(int tenantId)
    {
        var now = DateTime.UtcNow;
        var thresholds = new[] { 15, 30, 60, 90 };

        // Get open invoices grouped by customer
        var invoices = await _db.Invoices
            .Where(i => i.TenantId == tenantId && i.BalanceRemaining > 0)
            .ToListAsync();

        var grouped = invoices
            .GroupBy(i => i.StCustomerId)
            .Select(g => new
            {
                StCustomerId = g.Key,
                TotalOwed = g.Sum(i => i.BalanceRemaining),
                OldestDays = (int)g.Max(i => (now - i.InvoiceDate).TotalDays),
                InvoiceCount = g.Count(),
            })
            .ToList();

        // Load customers
        var stIds = grouped.Select(g => g.StCustomerId).ToList();
        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId && stIds.Contains(c.StCustomerId))
            .ToDictionaryAsync(c => c.StCustomerId);

        // Exclude customers with non-active AR status
        var allCustomerIds = customers.Values.Select(c => c.Id).ToList();
        var excludedIds = await _db.ArStatuses
            .Where(s => s.TenantId == tenantId && allCustomerIds.Contains(s.CustomerId)
                && s.Status != "active")
            .Select(s => s.CustomerId)
            .ToListAsync();

        var excludedSet = new HashSet<int>(excludedIds);

        // Load AR templates
        var templates = await _db.OutreachTemplates
            .Where(t => t.TenantId == tenantId && t.Type == "ar_reminder")
            .ToListAsync();

        if (templates.Count == 0) return;

        var tenant = await _db.Tenants.FindAsync(tenantId);
        var companyName = tenant?.Name ?? "";

        int generated = 0;
        var cutoff = now.AddDays(-30);

        foreach (var entry in grouped)
        {
            if (!customers.TryGetValue(entry.StCustomerId, out var customer)) continue;
            if (excludedSet.Contains(customer.Id)) continue;

            foreach (var threshold in thresholds)
            {
                if (entry.OldestDays < threshold) continue;

                // Dedup: check if we already have an ar_reminder for this customer + threshold
                var thresholdTag = $"ar_{threshold}d";
                var exists = await _db.OutreachItems
                    .AnyAsync(i => i.TenantId == tenantId && i.Type == "ar_reminder"
                        && i.CustomerId == customer.Id
                        && (i.Status == "pending" || i.Status == "sent")
                        && i.CreatedAt >= cutoff
                        && i.Subject != null && i.Subject.Contains(thresholdTag));

                if (exists) continue;

                var data = new Dictionary<string, string>
                {
                    ["customerName"] = customer.Name,
                    ["companyName"] = companyName,
                    ["totalOwed"] = entry.TotalOwed.ToString("C2"),
                    ["oldestInvoiceAge"] = entry.OldestDays.ToString(),
                    ["invoiceCount"] = entry.InvoiceCount.ToString(),
                };

                foreach (var tmpl in templates)
                {
                    var renderedSubject = tmpl.Subject != null
                        ? RenderTemplate(tmpl.Subject, data) + $" [{thresholdTag}]"
                        : $"[{thresholdTag}]";
                    var renderedBody = RenderTemplate(tmpl.Body, data);

                    _db.OutreachItems.Add(new OutreachItem
                    {
                        TenantId = tenantId,
                        CustomerId = customer.Id,
                        Type = "ar_reminder",
                        Channel = tmpl.Channel,
                        Subject = renderedSubject,
                        Body = renderedBody,
                    });
                    generated++;
                }
            }
        }

        if (generated > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("[ArAlerts] Generated {Count} AR reminder items for tenant {TenantId}", generated, tenantId);
        }
    }

    private string RenderTemplate(string template, Dictionary<string, string> data)
    {
        var result = template;
        foreach (var (key, value) in data)
            result = result.Replace($"{{{{{key}}}}}", value);
        return result;
    }
}
