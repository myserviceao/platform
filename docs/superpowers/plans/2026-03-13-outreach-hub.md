# Outreach Hub Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a human-in-the-loop customer outreach system with auto-generated PM reminders, post-service follow-ups, win-back campaigns, and seasonal campaigns — delivered via email (Resend) and SMS (Twilio).

**Architecture:** Three new EF Core models (OutreachTemplate, OutreachItem, OutreachSettings) with a new OutreachService handling generation, template rendering, and sending. A single OutreachController exposes 16 REST endpoints. The frontend is one new React page with tabbed queue views, a message editor modal, and template management. Outreach items are auto-generated during the existing ServiceTitan sync.

**Tech Stack:** .NET 8 / C# backend, EF Core + PostgreSQL, React 18 + TypeScript frontend, Tailwind CSS + FlyOnUI, Resend (email), Twilio (SMS)

**Spec:** `docs/superpowers/specs/2026-03-13-outreach-hub-design.md`

---

## File Structure

### Backend — New Files
- `backend/Models/OutreachTemplate.cs` — Template entity
- `backend/Models/OutreachItem.cs` — Outreach queue item entity
- `backend/Models/OutreachSettings.cs` — Per-tenant settings entity
- `backend/Services/OutreachService.cs` — Generation, rendering, sending logic
- `backend/Controllers/OutreachController.cs` — 16 REST endpoints

### Backend — Modified Files
- `backend/Models/Job.cs` — Add `CompletedOn` field
- `backend/Data/AppDbContext.cs` — Register 3 new DbSets + entity config
- `backend/Data/DbMigrations.cs` — Add 4 new table/column migrations
- `backend/Services/ServiceTitan/ServiceTitanSyncService.cs` — Persist CompletedOn, hook outreach generation
- `backend/Program.cs` — Register OutreachService

### Frontend — New Files
- `frontend/src/pages/App/Outreach/OutreachPage.tsx` — Main Outreach Hub page
- `frontend/src/pages/App/Outreach/MessageEditorModal.tsx` — Edit/send modal
- `frontend/src/pages/App/Outreach/TemplateManager.tsx` — Template CRUD UI
- `frontend/src/pages/App/Outreach/OutreachSettings.tsx` — Credentials + thresholds config
- `frontend/src/pages/App/Outreach/CampaignModal.tsx` — Seasonal campaign creator

### Frontend — Modified Files
- `frontend/src/App.tsx` — Add outreach route
- `frontend/src/components/layout/AppShell.tsx` — Add nav item, remove PM Outreach

---

## Chunk 1: Prerequisites + Backend Models

### Task 1: Add Job.CompletedOn field

**Files:**
- Modify: `backend/Models/Job.cs`
- Modify: `backend/Data/DbMigrations.cs`
- Modify: `backend/Services/ServiceTitan/ServiceTitanSyncService.cs`

- [ ] **Step 1: Add CompletedOn to Job model**

In `backend/Models/Job.cs`, add after the `TechnicianName` property:

```csharp
public DateTime? CompletedOn { get; set; }
```

- [ ] **Step 2: Add migration for CompletedOn**

In `backend/Data/DbMigrations.cs`, add before the closing `}` of `RunAsync`:

```csharp
await db.Database.ExecuteSqlRawAsync(@"
    ALTER TABLE ""Jobs"" ADD COLUMN IF NOT EXISTS ""CompletedOn"" TIMESTAMP WITH TIME ZONE;
");
```

- [ ] **Step 3: Persist CompletedOn in sync service**

In `backend/Services/ServiceTitan/ServiceTitanSyncService.cs`, find the job upsert section where `createdOn` is parsed (around line 220). Add completedOn parsing right after the `createdOn` block:

```csharp
DateTime? completedOn = null;
if (job.TryGetProperty("completedOn", out var compProp) && compProp.ValueKind == JsonValueKind.String)
    if (DateTime.TryParse(compProp.GetString(), null,
        System.Globalization.DateTimeStyles.AssumeUniversal |
        System.Globalization.DateTimeStyles.AdjustToUniversal, out var compDate))
        completedOn = DateTime.SpecifyKind(compDate, DateTimeKind.Utc);
```

Then in the `new Job { ... }` block, add `CompletedOn = completedOn,` after `CreatedOn = createdOn,`.

In the `else` (update existing) block, add `existingJob.CompletedOn = completedOn;` alongside the other field updates.

- [ ] **Step 4: Build and verify**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add backend/Models/Job.cs backend/Data/DbMigrations.cs backend/Services/ServiceTitan/ServiceTitanSyncService.cs
git commit -m "feat: add Job.CompletedOn field and persist during sync"
```

---

### Task 2: Create OutreachTemplate model

**Files:**
- Create: `backend/Models/OutreachTemplate.cs`

- [ ] **Step 1: Create the model file**

```csharp
namespace MyServiceAO.Models;

public class OutreachTemplate
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // pm_reminder, post_service, win_back, seasonal
    public string Channel { get; set; } = "email"; // email or sms
    public string? Subject { get; set; } // email subject (null for sms)
    public string Body { get; set; } = "";
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/OutreachTemplate.cs
git commit -m "feat: add OutreachTemplate model"
```

---

### Task 3: Create OutreachItem model

**Files:**
- Create: `backend/Models/OutreachItem.cs`

- [ ] **Step 1: Create the model file**

```csharp
namespace MyServiceAO.Models;

public class OutreachItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CustomerId { get; set; }
    public int? JobId { get; set; } // set for post_service items

    public string Type { get; set; } = ""; // pm_reminder, post_service, win_back, seasonal
    public string Channel { get; set; } = "email"; // email or sms
    public string Status { get; set; } = "pending"; // pending, sent, dismissed, failed
    public string? FailureReason { get; set; }

    public string? Subject { get; set; } // rendered email subject
    public string Body { get; set; } = ""; // rendered message body

    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/OutreachItem.cs
git commit -m "feat: add OutreachItem model"
```

---

### Task 4: Create OutreachSettings model

**Files:**
- Create: `backend/Models/OutreachSettings.cs`

- [ ] **Step 1: Create the model file**

```csharp
namespace MyServiceAO.Models;

public class OutreachSettings
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int WinBackThresholdMonths { get; set; } = 12;
    public int PostServiceDelayHours { get; set; } = 48;

    // Resend (email)
    public string? ResendApiKey { get; set; }
    public string? ResendFromEmail { get; set; }

    // Twilio (SMS)
    public string? TwilioAccountSid { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? TwilioFromPhone { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/OutreachSettings.cs
git commit -m "feat: add OutreachSettings model"
```

---

### Task 5: Register models in DbContext and add migrations

**Files:**
- Modify: `backend/Data/AppDbContext.cs`
- Modify: `backend/Data/DbMigrations.cs`

- [ ] **Step 1: Add DbSets to AppDbContext**

Add after the existing DbSet declarations (around line 20):

```csharp
public DbSet<OutreachTemplate> OutreachTemplates => Set<OutreachTemplate>();
public DbSet<OutreachItem> OutreachItems => Set<OutreachItem>();
public DbSet<OutreachSettings> OutreachSettings => Set<OutreachSettings>();
```

- [ ] **Step 2: Add entity configuration in OnModelCreating**

Add before the closing `}` of `OnModelCreating`:

```csharp
// OutreachTemplate
modelBuilder.Entity<OutreachTemplate>(e =>
{
    e.HasKey(t => t.Id);
    e.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId);
});

// OutreachItem
modelBuilder.Entity<OutreachItem>(e =>
{
    e.HasKey(i => i.Id);
    e.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId);
});

// OutreachSettings
modelBuilder.Entity<OutreachSettings>(e =>
{
    e.HasKey(s => s.Id);
    e.HasIndex(s => s.TenantId).IsUnique();
    e.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId);
});
```

- [ ] **Step 3: Add table migrations in DbMigrations.cs**

Add before the closing `}` of `RunAsync`:

```csharp
// OutreachTemplates table
await db.Database.ExecuteSqlRawAsync(@"
    CREATE TABLE IF NOT EXISTS ""OutreachTemplates"" (
        ""Id"" SERIAL PRIMARY KEY,
        ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
        ""Name"" TEXT NOT NULL DEFAULT '',
        ""Type"" TEXT NOT NULL DEFAULT '',
        ""Channel"" TEXT NOT NULL DEFAULT 'email',
        ""Subject"" TEXT,
        ""Body"" TEXT NOT NULL DEFAULT '',
        ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
    );
");

// OutreachItems table
await db.Database.ExecuteSqlRawAsync(@"
    CREATE TABLE IF NOT EXISTS ""OutreachItems"" (
        ""Id"" SERIAL PRIMARY KEY,
        ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
        ""CustomerId"" INTEGER NOT NULL,
        ""JobId"" INTEGER,
        ""Type"" TEXT NOT NULL DEFAULT '',
        ""Channel"" TEXT NOT NULL DEFAULT 'email',
        ""Status"" TEXT NOT NULL DEFAULT 'pending',
        ""FailureReason"" TEXT,
        ""Subject"" TEXT,
        ""Body"" TEXT NOT NULL DEFAULT '',
        ""ScheduledFor"" TIMESTAMP WITH TIME ZONE,
        ""SentAt"" TIMESTAMP WITH TIME ZONE,
        ""DismissedAt"" TIMESTAMP WITH TIME ZONE,
        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
    );
");

// OutreachSettings table
await db.Database.ExecuteSqlRawAsync(@"
    CREATE TABLE IF NOT EXISTS ""OutreachSettings"" (
        ""Id"" SERIAL PRIMARY KEY,
        ""TenantId"" INTEGER NOT NULL REFERENCES ""Tenants""(""Id"") ON DELETE CASCADE,
        ""WinBackThresholdMonths"" INTEGER NOT NULL DEFAULT 12,
        ""PostServiceDelayHours"" INTEGER NOT NULL DEFAULT 48,
        ""ResendApiKey"" TEXT,
        ""ResendFromEmail"" TEXT,
        ""TwilioAccountSid"" TEXT,
        ""TwilioAuthToken"" TEXT,
        ""TwilioFromPhone"" TEXT,
        UNIQUE(""TenantId"")
    );
");
```

- [ ] **Step 4: Build and verify**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add backend/Data/AppDbContext.cs backend/Data/DbMigrations.cs
git commit -m "feat: register outreach models in DbContext and add migrations"
```

---

## Chunk 2: OutreachService

### Task 6: Create OutreachService — template rendering and default seeding

**Files:**
- Create: `backend/Services/OutreachService.cs`
- Modify: `backend/Program.cs`

- [ ] **Step 1: Create OutreachService with template rendering and default seeding**

Create `backend/Services/OutreachService.cs`:

```csharp
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

        // Find customers whose most recent completed job is older than threshold
        var lastJobPerCustomer = await _db.Jobs
            .Where(j => j.TenantId == tenantId && j.Status == "Completed" && j.CompletedOn != null)
            .GroupBy(j => j.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, LastCompleted = g.Max(j => j.CompletedOn) })
            .Where(x => x.LastCompleted < cutoff)
            .ToListAsync();

        var customers = await _db.Customers
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.StCustomerId, c => c);

        // Get lifetime spend per customer
        var spendMap = await _db.Invoices
            .Where(i => i.TenantId == tenantId)
            .GroupBy(i => i.StCustomerId)
            .Select(g => new { StCustomerId = g.Key, Total = g.Sum(i => i.TotalAmount) })
            .ToDictionaryAsync(x => x.StCustomerId, x => x.Total);

        // Sort by lifetime spend descending
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
```

- [ ] **Step 2: Register OutreachService in Program.cs**

Add after the other service registrations (around line 44):

```csharp
builder.Services.AddScoped<OutreachService>();
```

Add the using statement at the top if needed:

```csharp
using MyServiceAO.Services;
```

- [ ] **Step 3: Build and verify**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add backend/Services/OutreachService.cs backend/Program.cs
git commit -m "feat: add OutreachService with generation, rendering, and sending"
```

---

### Task 7: Hook outreach generation into sync

**Files:**
- Modify: `backend/Services/ServiceTitan/ServiceTitanSyncService.cs`

- [ ] **Step 1: Add OutreachService dependency**

Add `OutreachService` to the constructor. In the constructor parameters, add `OutreachService outreach` and store it as `_outreach`. Add the field:

```csharp
private readonly OutreachService _outreach;
```

Update the constructor to include it:
```csharp
public ServiceTitanSyncService(AppDbContext db, ServiceTitanClient client, ServiceTitanOAuthService oauth, OutreachService outreach, ILogger<ServiceTitanSyncService> logger)
{
    _db = db; _client = client; _oauth = oauth; _outreach = outreach; _logger = logger;
}
```

- [ ] **Step 2: Call outreach generation before snapshot save**

Find the line `// Update snapshot timestamp` (around line 968) and add before it:

```csharp
// Generate outreach items
try
{
    await _outreach.GenerateOutreachItemsAsync(tenantId);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "[Sync] Outreach generation failed (non-fatal)");
}
```

- [ ] **Step 3: Build and verify**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add backend/Services/ServiceTitan/ServiceTitanSyncService.cs
git commit -m "feat: hook outreach generation into ServiceTitan sync"
```

---

## Chunk 3: OutreachController

### Task 8: Create OutreachController

**Files:**
- Create: `backend/Controllers/OutreachController.cs`

- [ ] **Step 1: Create the controller**

Create `backend/Controllers/OutreachController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;
using MyServiceAO.Services;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api/outreach")]
public class OutreachController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OutreachService _outreach;

    public OutreachController(AppDbContext db, OutreachService outreach)
    {
        _db = db;
        _outreach = outreach;
    }

    private int? TenantId => HttpContext.Session.GetInt32("tenantId");

    // GET /api/outreach?type=pm_reminder&status=pending&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> List(string? type = null, string? status = null, int page = 1, int pageSize = 50)
    {
        if (TenantId == null) return Unauthorized();

        var query = _db.OutreachItems
            .Where(i => i.TenantId == TenantId.Value);

        if (!string.IsNullOrEmpty(type)) query = query.Where(i => i.Type == type);
        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load customer data for display
        var customerIds = items.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await _db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var result = items.Select(i =>
        {
            customers.TryGetValue(i.CustomerId, out var c);
            return new
            {
                i.Id, i.CustomerId, i.JobId, i.Type, i.Channel, i.Status,
                i.FailureReason, i.Subject, i.Body,
                i.ScheduledFor, i.SentAt, i.DismissedAt, i.CreatedAt, i.UpdatedAt,
                customerName = c?.Name,
                customerPhone = c?.Phone,
                customerEmail = c?.Email,
            };
        });

        return Ok(new { items = result, total, page, pageSize });
    }

    // GET /api/outreach/stats
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        if (TenantId == null) return Unauthorized();

        var items = await _db.OutreachItems
            .Where(i => i.TenantId == TenantId.Value)
            .GroupBy(i => new { i.Type, i.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var sentToday = await _db.OutreachItems
            .CountAsync(i => i.TenantId == TenantId.Value && i.Status == "sent" && i.SentAt >= today);
        var sentThisWeek = await _db.OutreachItems
            .CountAsync(i => i.TenantId == TenantId.Value && i.Status == "sent" && i.SentAt >= weekAgo);

        return Ok(new
        {
            byTypeAndStatus = items,
            sentToday,
            sentThisWeek,
            totalPending = items.Where(i => i.Status == "pending").Sum(i => i.Count),
        });
    }

    // PUT /api/outreach/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditOutreachRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var item = await _db.OutreachItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId.Value);
        if (item == null) return NotFound();
        if (item.Status != "pending" && item.Status != "failed") return BadRequest("Can only edit pending or failed items");

        if (req.Subject != null) item.Subject = req.Subject;
        if (req.Body != null) item.Body = req.Body;
        if (req.Channel != null) item.Channel = req.Channel;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // POST /api/outreach/{id}/send
    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id)
    {
        if (TenantId == null) return Unauthorized();
        var success = await _outreach.SendItemAsync(id, TenantId.Value);
        return Ok(new { success });
    }

    // POST /api/outreach/{id}/retry
    [HttpPost("{id:int}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        if (TenantId == null) return Unauthorized();
        var item = await _db.OutreachItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId.Value);
        if (item == null) return NotFound();
        item.Status = "pending";
        item.FailureReason = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var success = await _outreach.SendItemAsync(id, TenantId.Value);
        return Ok(new { success });
    }

    // POST /api/outreach/bulk-send
    [HttpPost("bulk-send")]
    public async Task<IActionResult> BulkSend([FromBody] BulkRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var (sent, failed) = await _outreach.BulkSendAsync(req.Ids, TenantId.Value);
        return Ok(new { sent, failed });
    }

    // POST /api/outreach/{id}/dismiss
    [HttpPost("{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (TenantId == null) return Unauthorized();
        await _outreach.DismissItemAsync(id, TenantId.Value);
        return Ok();
    }

    // POST /api/outreach/bulk-dismiss
    [HttpPost("bulk-dismiss")]
    public async Task<IActionResult> BulkDismiss([FromBody] BulkRequest req)
    {
        if (TenantId == null) return Unauthorized();
        await _outreach.BulkDismissAsync(req.Ids, TenantId.Value);
        return Ok();
    }

    // ── Templates ──

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates()
    {
        if (TenantId == null) return Unauthorized();
        var templates = await _db.OutreachTemplates
            .Where(t => t.TenantId == TenantId.Value)
            .OrderBy(t => t.Type).ThenBy(t => t.Channel)
            .ToListAsync();
        return Ok(templates);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var template = new OutreachTemplate
        {
            TenantId = TenantId.Value,
            Name = req.Name,
            Type = req.Type,
            Channel = req.Channel,
            Subject = req.Subject,
            Body = req.Body,
        };
        _db.OutreachTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> EditTemplate(int id, [FromBody] TemplateRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var template = await _db.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId.Value);
        if (template == null) return NotFound();
        template.Name = req.Name;
        template.Type = req.Type;
        template.Channel = req.Channel;
        template.Subject = req.Subject;
        template.Body = req.Body;
        template.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        if (TenantId == null) return Unauthorized();
        var template = await _db.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId.Value);
        if (template == null) return NotFound();
        if (template.IsDefault) return BadRequest("Cannot delete default templates");
        _db.OutreachTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── Campaigns ──

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var count = await _outreach.GenerateSeasonalCampaignAsync(TenantId.Value, req.TemplateId, req.Segment, req.MonthsThreshold);
        return Ok(new { generated = count });
    }

    // ── Settings ──

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        if (TenantId == null) return Unauthorized();
        var settings = await _db.OutreachSettings.FirstOrDefaultAsync(s => s.TenantId == TenantId.Value);
        if (settings == null)
            return Ok(new { winBackThresholdMonths = 12, postServiceDelayHours = 48, emailConfigured = false, smsConfigured = false });

        return Ok(new
        {
            settings.WinBackThresholdMonths,
            settings.PostServiceDelayHours,
            resendApiKey = OutreachService.MaskSecret(settings.ResendApiKey),
            settings.ResendFromEmail,
            twilioAccountSid = OutreachService.MaskSecret(settings.TwilioAccountSid),
            twilioAuthToken = OutreachService.MaskSecret(settings.TwilioAuthToken),
            settings.TwilioFromPhone,
            emailConfigured = !string.IsNullOrEmpty(settings.ResendApiKey),
            smsConfigured = !string.IsNullOrEmpty(settings.TwilioAccountSid),
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] SettingsRequest req)
    {
        if (TenantId == null) return Unauthorized();
        var settings = await _db.OutreachSettings.FirstOrDefaultAsync(s => s.TenantId == TenantId.Value);
        if (settings == null)
        {
            settings = new OutreachSettings { TenantId = TenantId.Value };
            _db.OutreachSettings.Add(settings);
        }

        if (req.WinBackThresholdMonths.HasValue) settings.WinBackThresholdMonths = req.WinBackThresholdMonths.Value;
        if (req.PostServiceDelayHours.HasValue) settings.PostServiceDelayHours = req.PostServiceDelayHours.Value;

        // Only update credentials if not masked value
        if (req.ResendApiKey != null && !req.ResendApiKey.StartsWith("****"))
            settings.ResendApiKey = req.ResendApiKey;
        if (req.ResendFromEmail != null) settings.ResendFromEmail = req.ResendFromEmail;
        if (req.TwilioAccountSid != null && !req.TwilioAccountSid.StartsWith("****"))
            settings.TwilioAccountSid = req.TwilioAccountSid;
        if (req.TwilioAuthToken != null && !req.TwilioAuthToken.StartsWith("****"))
            settings.TwilioAuthToken = req.TwilioAuthToken;
        if (req.TwilioFromPhone != null) settings.TwilioFromPhone = req.TwilioFromPhone;

        await _db.SaveChangesAsync();
        return Ok();
    }
}

// ── Request DTOs ──

public record EditOutreachRequest(string? Subject, string? Body, string? Channel);
public record BulkRequest(List<int> Ids);
public record TemplateRequest(string Name, string Type, string Channel, string? Subject, string Body);
public record CampaignRequest(int TemplateId, string Segment, int? MonthsThreshold);
public record SettingsRequest(int? WinBackThresholdMonths, int? PostServiceDelayHours, string? ResendApiKey, string? ResendFromEmail, string? TwilioAccountSid, string? TwilioAuthToken, string? TwilioFromPhone);
```

- [ ] **Step 2: Build and verify**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add backend/Controllers/OutreachController.cs
git commit -m "feat: add OutreachController with 16 endpoints"
```

---

## Chunk 4: Frontend — Outreach Hub Page

### Task 9: Create the main OutreachPage

**Files:**
- Create: `frontend/src/pages/App/Outreach/OutreachPage.tsx`

- [ ] **Step 1: Create the OutreachPage**

Create `frontend/src/pages/App/Outreach/OutreachPage.tsx`. This is a large file — the main page with tabs, queue table, stats header, bulk actions, and inline channel toggles. It imports the other components (MessageEditorModal, TemplateManager, OutreachSettings, CampaignModal) which will be created in subsequent tasks.

The page should follow the existing pattern from `ApPage.tsx`:
- `useState` for tab, loading, data
- `useEffect` to fetch from `/api/outreach/stats` and `/api/outreach?type=X&status=pending`
- Tabs: PM Reminders, Post-Service, Win-Back, Seasonal, Sent History
- Queue table with columns: Customer, Contact, Reason, Channel, Draft Preview, Actions
- Summary stats in header: total pending, sent today, sent this week
- Bulk action bar when items are selected
- Failed items show warning badge + Retry button
- Win-Back tab shows extra columns (lifetime spend, months since service)

**Key implementation notes:**
- Use `credentials: 'include'` on all fetch calls
- Tab state maps to `type` query param on API
- Sent History tab uses `status=sent` filter
- Each row has checkbox for bulk select
- Channel shown as badge (toggleable via PUT to edit)
- Actions: Edit (opens modal), Send (POST), Dismiss (POST)

This file will be approximately 400-500 lines. The implementer should read `ApPage.tsx` and `PmOutreachPage.tsx` for exact styling patterns (FlyOnUI classes like `rounded-box`, `badge badge-soft`, `table table-sm`, `tabs tabs-bordered`, etc.).

- [ ] **Step 2: Verify no TypeScript errors**

Run: `cd frontend && npx -p typescript tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/App/Outreach/OutreachPage.tsx
git commit -m "feat: add OutreachPage with tabbed queue view"
```

---

### Task 10: Create MessageEditorModal

**Files:**
- Create: `frontend/src/pages/App/Outreach/MessageEditorModal.tsx`

- [ ] **Step 1: Create the modal component**

A modal that receives an outreach item, lets the user:
- Select a different template from dropdown (fetches from `/api/outreach/templates`)
- Edit subject (email only) and body
- Toggle channel (email/sms)
- See a live preview
- Send or save changes

Props: `item`, `onClose`, `onSaved`, `onSent`

Use FlyOnUI modal pattern: `<dialog>` or div with `modal` class. Follow existing modal patterns in the codebase.

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/App/Outreach/MessageEditorModal.tsx
git commit -m "feat: add MessageEditorModal for outreach item editing"
```

---

### Task 11: Create TemplateManager

**Files:**
- Create: `frontend/src/pages/App/Outreach/TemplateManager.tsx`

- [ ] **Step 1: Create the template management component**

A panel/page that shows all templates grouped by type, with:
- List view with type grouping headers
- Create/Edit form (inline or modal): name, type dropdown, channel dropdown, subject (shown if email), body textarea
- Merge tag helper: clickable tag pills that insert `{{tagName}}` at cursor
- Available tags change based on selected type
- Delete button (disabled for `isDefault` templates)
- Preview panel showing rendered template with sample data

Fetches from: `GET /api/outreach/templates`
Creates: `POST /api/outreach/templates`
Updates: `PUT /api/outreach/templates/{id}`
Deletes: `DELETE /api/outreach/templates/{id}`

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/App/Outreach/TemplateManager.tsx
git commit -m "feat: add TemplateManager for outreach templates"
```

---

### Task 12: Create OutreachSettings component

**Files:**
- Create: `frontend/src/pages/App/Outreach/OutreachSettings.tsx`

- [ ] **Step 1: Create the settings component**

A settings panel with:
- **Thresholds section**: Win-back months (number input), Post-service delay hours (number input)
- **Email section**: Resend API key (password input, shows masked value), From email address
- **SMS section**: Twilio Account SID (password input, masked), Auth Token (password input, masked), From phone number
- Save button that PUTs to `/api/outreach/settings`
- Status indicators showing "Configured" or "Not configured" for email and SMS

Fetches from: `GET /api/outreach/settings`
Saves to: `PUT /api/outreach/settings`

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/App/Outreach/OutreachSettings.tsx
git commit -m "feat: add OutreachSettings for credentials and thresholds"
```

---

### Task 13: Create CampaignModal

**Files:**
- Create: `frontend/src/pages/App/Outreach/CampaignModal.tsx`

- [ ] **Step 1: Create the campaign creator modal**

A modal for creating seasonal campaigns:
- Template selector (dropdown, filtered to `seasonal` type templates)
- Segment selector (radio buttons or dropdown):
  - All customers
  - Customers with PM history
  - Customers without PM history
  - Customers with no service in last N months (shows number input when selected)
- "Generate" button that POSTs to `/api/outreach/campaigns`
- Shows result: "Generated X draft items for review"

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/App/Outreach/CampaignModal.tsx
git commit -m "feat: add CampaignModal for seasonal outreach campaigns"
```

---

## Chunk 5: Frontend Integration + Navigation

### Task 14: Add route and navigation

**Files:**
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/components/layout/AppShell.tsx`

- [ ] **Step 1: Add route in App.tsx**

Add import at top:
```tsx
import { OutreachPage } from '@/pages/App/Outreach/OutreachPage'
```

Add route alongside existing routes (after the customers route):
```tsx
<Route path="outreach" element={<OutreachPage />} />
```

- [ ] **Step 2: Update navigation in AppShell.tsx**

Add "Outreach" to the navItems array, between Customers and Accounts Payable:
```tsx
{
  label: 'Outreach',
  icon: 'icon-[tabler--mail-forward]',
  path: '/app/outreach',
},
```

Remove "PM Outreach" from the PMs/Maintenances children array (the entry with `path: '/app/pm-outreach'`).

- [ ] **Step 3: Add "Outreach" to the global search pageResults array**

Find the `pageResults` array in AppShell.tsx and add:
```tsx
{ type: 'page', label: 'Outreach', path: '/app/outreach' },
```

- [ ] **Step 4: Build frontend and verify**

Run: `cd frontend && npx -p typescript tsc --noEmit`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add frontend/src/App.tsx frontend/src/components/layout/AppShell.tsx
git commit -m "feat: add Outreach to navigation and routing, deprecate PM Outreach"
```

---

### Task 15: Final build verification and push

- [ ] **Step 1: Build backend**

Run: `cd backend && dotnet build --nologo --verbosity quiet`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: Build frontend**

Run: `cd frontend && npx -p typescript tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Push all commits**

Run: `git push origin main`
Expected: Successful push
