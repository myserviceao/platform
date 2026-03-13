# Outreach Hub — Design Spec

## Overview

A human-in-the-loop customer outreach system that auto-generates suggested communications based on customer and job data, lets users review/edit/send via email or SMS, and tracks sent history. Covers PM reminders, post-service follow-ups, win-back campaigns, and seasonal campaigns.

## Principles

- **Human-in-the-loop**: The system drafts and suggests; a human reviews and sends.
- **Self-contained**: Single new page (`/app/outreach`), no changes to existing pages.
- **Template-driven**: Ships with defaults, users can create their own.
- **Value-ranked**: Win-back candidates sorted by lifetime spend and engagement history.

---

## Prerequisites

### Job.CompletedOn Field

The existing `Job` model has no `CompletedOn` column. The ST sync service already parses `completedOn` from the API response (for PM date calculation) but does not persist it. This feature requires:

1. Add `CompletedOn` (DateTime?) to the `Job` model.
2. Add `ALTER TABLE "Jobs" ADD COLUMN IF NOT EXISTS "CompletedOn" TIMESTAMP WITH TIME ZONE;` to DbMigrations.
3. Update the sync service to persist `completedOn` when upserting jobs.

Without this, the post-service follow-up generation logic cannot function.

### Existing PM Outreach Page

The app currently has a `PmOutreachController` and PM Outreach page. Once the Outreach Hub is live, the old PM Outreach page is **deprecated** — the Outreach Hub's PM Reminders tab replaces it. During implementation:

1. Remove "PM Outreach" from the sidebar nav.
2. Keep the old controller temporarily for backward compat, but add no new features to it.
3. Remove the old controller and page in a follow-up cleanup.

---

## Data Model

### OutreachTemplate

| Field       | Type     | Notes                                                        |
|-------------|----------|--------------------------------------------------------------|
| Id          | int      | PK                                                           |
| TenantId    | int      | FK to Tenants                                                |
| Name        | string   | e.g., "PM Reminder - Email"                                  |
| Type        | string   | `pm_reminder`, `post_service`, `win_back`, `seasonal`        |
| Channel     | string   | `email` or `sms` (one template per channel)                  |
| Subject     | string?  | Email subject line (null for SMS templates)                   |
| Body        | string   | Message body with merge tags like `{{customerName}}`         |
| IsDefault   | bool     | System-provided templates, cannot be deleted                 |
| CreatedAt   | DateTime |                                                              |
| UpdatedAt   | DateTime |                                                              |

Note: No `both` channel on templates. Each template targets one channel. A type can have separate email and SMS templates. When generating items, the system creates one item per channel if both templates exist for the type.

### OutreachItem

| Field         | Type      | Notes                                                      |
|---------------|-----------|-------------------------------------------------------------|
| Id            | int       | PK                                                         |
| TenantId      | int       | FK to Tenants                                               |
| CustomerId    | int       | FK to Customers                                             |
| JobId         | int?      | FK to Jobs (set for post_service items, null otherwise)     |
| Type          | string    | `pm_reminder`, `post_service`, `win_back`, `seasonal`       |
| Channel       | string    | `email` or `sms`                                            |
| Status        | string    | `pending`, `sent`, `dismissed`, `failed`                    |
| FailureReason | string?   | Error message if status is `failed`                         |
| Subject       | string?   | Rendered email subject                                      |
| Body          | string    | Rendered message body with customer data filled in          |
| ScheduledFor  | DateTime? | Optional, for seasonal campaigns                           |
| SentAt        | DateTime? |                                                             |
| DismissedAt   | DateTime? |                                                             |
| CreatedAt     | DateTime  |                                                             |
| UpdatedAt     | DateTime  |                                                             |

### OutreachSettings

| Field                     | Type    | Notes                                          |
|---------------------------|---------|-------------------------------------------------|
| Id                        | int     | PK                                              |
| TenantId                  | int     | FK to Tenants, unique                           |
| WinBackThresholdMonths    | int     | Default 12                                      |
| PostServiceDelayHours     | int     | Default 48                                      |
| ResendApiKey              | string? | Encrypted at rest. Masked in GET responses.     |
| ResendFromEmail           | string? | e.g., `service@theircompany.com`                |
| TwilioAccountSid          | string? | Encrypted at rest. Masked in GET responses.     |
| TwilioAuthToken           | string? | Encrypted at rest. Masked in GET responses.     |
| TwilioFromPhone           | string? | e.g., `+15551234567`                            |

Note: `PmReminderDaysBeforeDue` has been removed. PM reminder generation uses the existing `PmCustomer.PmStatus` field (Overdue/ComingDue) which is already computed by the sync service.

### Default Template Seeding

Default templates (8 total: 1 email + 1 SMS per type) are seeded lazily. When `GenerateOutreachItemsAsync` runs and finds no templates for a tenant, it creates the defaults. This avoids a global migration and handles new tenants automatically.

---

## Outreach Generation Logic

Runs after each ServiceTitan sync via `OutreachService.GenerateOutreachItemsAsync(tenantId)`.

**Deduplication rules:**
- **PM Reminder, Win-Back, Seasonal**: Skip if a pending or sent item of the same `type` + `customerId` exists within the last 30 days.
- **Post-Service**: Skip if a pending or sent item of the same `type` + `customerId` + `jobId` exists. This allows multiple follow-ups to the same customer for different jobs.

**Channel handling**: For each qualifying customer/job, if both an email and SMS template exist for the type, generate two items (one per channel). The user can dismiss whichever they don't want.

### PM Reminders

- Query `PmCustomers` where status is `Overdue` or `ComingDue`.
- Generate one `OutreachItem` per qualifying customer per channel.
- Available merge tags: `{{customerName}}`, `{{lastPmDate}}`, `{{daysOverdue}}`.

### Post-Service Follow-ups

- Query `Jobs` where `CompletedOn` is within the last `PostServiceDelayHours` and `Status` is `Completed`.
- Generate one item per completed job per channel.
- Available merge tags: `{{customerName}}`, `{{jobType}}`, `{{technicianName}}`, `{{completionDate}}`.

### Win-Back

- Query `Customers` whose most recent completed job (`CompletedOn`) is older than `WinBackThresholdMonths`.
- Rank by lifetime spend (sum of invoice totals) descending, then by total job count.
- Generate one item per qualifying customer per channel.
- Available merge tags: `{{customerName}}`, `{{lastServiceDate}}`, `{{monthsSinceService}}`, `{{lifetimeSpend}}`.

### Seasonal Campaigns

- **User-triggered**, not auto-generated.
- User picks a template, selects a customer segment, and the system bulk-generates draft items for review.
- Segment options: All customers, Customers with PM history, Customers without PM history, Customers with no service in last N months (user-configurable).

---

## Outreach Hub Page

Route: `/app/outreach`

### Header

- Title: "Outreach"
- Summary stats: total pending, sent today, sent this week
- "New Campaign" button (opens seasonal campaign creator)

### Tabs

1. **PM Reminders** — pending count badge
2. **Post-Service** — pending count badge
3. **Win-Back** — pending count badge
4. **Seasonal** — pending count badge
5. **Sent History**

### Queue View (tabs 1-4)

Paginated table of pending outreach items (default 50 per page).

| Column          | Notes                                                    |
|-----------------|----------------------------------------------------------|
| Customer        | Name + avatar initial                                    |
| Contact         | Phone and/or email                                       |
| Reason          | e.g., "PM overdue 45 days", "Completed 2 days ago"      |
| Channel         | Toggle: email / SMS                                      |
| Draft Preview   | Truncated message preview                                |
| Actions         | Edit, Send, Dismiss                                      |

- **Win-Back tab** additionally shows lifetime spend and months since last service.
- Win-back items sorted by lifetime spend descending (highest-value customers first).
- **Failed items** show with a warning badge and a "Retry" action.
- **Bulk actions**: Select multiple rows, "Send Selected", "Dismiss Selected".

### Message Editor (modal)

- Template selector dropdown (defaults to auto-selected template for the type).
- Subject line field (email only).
- Body textarea with rendered merge tags shown as actual customer values.
- Channel toggle (email / SMS).
- Live preview of the final message.
- Send button.

### Sent History Tab

- Paginated table: customer, type, channel, message preview, sent date, status (sent/failed).
- Filterable by type and date range.

---

## Template Management

Accessible from a "Manage Templates" link within the Outreach Hub.

### Default Templates

- 1 email + 1 SMS template per outreach type = 8 total defaults.
- Marked `IsDefault = true`. Cannot be deleted, but can be duplicated and edited.
- Seeded lazily on first outreach generation per tenant.

### Template Editor

- List view grouped by type.
- Create/edit form: name, type, channel (email or sms), subject (email), body.
- **Merge tag helper**: Clickable list of available tags for the selected type. Clicking inserts tag at cursor position.
- **Live preview panel**: Shows template rendered with sample data.

### Available Merge Tags by Type

| Tag                      | Available In                        |
|--------------------------|-------------------------------------|
| `{{customerName}}`       | All types                           |
| `{{companyName}}`        | All types                           |
| `{{phone}}`              | All types                           |
| `{{lastPmDate}}`         | PM Reminder                         |
| `{{daysOverdue}}`        | PM Reminder                         |
| `{{jobType}}`            | Post-Service                        |
| `{{technicianName}}`     | Post-Service                        |
| `{{completionDate}}`     | Post-Service                        |
| `{{lastServiceDate}}`    | Win-Back                            |
| `{{monthsSinceService}}` | Win-Back                            |
| `{{lifetimeSpend}}`      | Win-Back                            |

---

## Sending Infrastructure

### Email — Resend

- REST API integration.
- Configurable "from" address per tenant (e.g., `service@theircompany.com`).
- API key stored in `OutreachSettings`, encrypted at rest.

### SMS — Twilio

- Account SID, Auth Token, and "from" phone number stored in `OutreachSettings`, encrypted at rest.
- SMS templates should target under 160 characters.

### Error Handling

- If a send fails (API error, invalid credentials, rate limit), set the item status to `failed` with the error in `FailureReason`.
- Failed items remain visible in the queue with a warning badge and "Retry" action.
- Retry resets status to `pending` and attempts to send again.

### Configuration

A settings section within the Outreach Hub where the tenant enters:
- Resend API key + from email address
- Twilio Account SID, Auth Token, from phone number
- Win-back threshold months and post-service delay hours

GET responses mask sensitive fields (show `****` + last 4 characters for API keys/tokens). Sending is disabled until credentials are configured — the UI shows a setup prompt if missing.

---

## Backend Architecture

### OutreachService

- `GenerateOutreachItemsAsync(tenantId)` — runs generation logic for all types, seeds default templates if needed, respects deduplication.
- `SendOutreachItemAsync(itemId)` — renders final message, sends via Resend or Twilio based on item channel, updates status to `sent` or `failed`.
- `DismissOutreachItemAsync(itemId)` — marks as `dismissed`.
- `BulkSendAsync(itemIds)` — sends multiple items, returns per-item success/failure.
- `BulkDismissAsync(itemIds)` — dismisses multiple items.
- `RenderTemplate(template, customerData)` — replaces merge tags with actual values.

### OutreachController — `/api/outreach`

| Method | Route                          | Description                          |
|--------|--------------------------------|--------------------------------------|
| GET    | `/api/outreach`                | List items (filter by type, status, page, pageSize) |
| GET    | `/api/outreach/stats`          | Pending/sent/failed counts by type   |
| PUT    | `/api/outreach/{id}`           | Edit draft (subject, body, channel)  |
| POST   | `/api/outreach/{id}/send`      | Send single item                     |
| POST   | `/api/outreach/{id}/retry`     | Retry a failed item                  |
| POST   | `/api/outreach/bulk-send`      | Send selected items                  |
| POST   | `/api/outreach/{id}/dismiss`   | Dismiss item                         |
| POST   | `/api/outreach/bulk-dismiss`   | Dismiss selected items               |
| GET    | `/api/outreach/templates`      | List templates                       |
| POST   | `/api/outreach/templates`      | Create template                      |
| PUT    | `/api/outreach/templates/{id}` | Edit template                        |
| DELETE | `/api/outreach/templates/{id}` | Delete (non-default only)            |
| POST   | `/api/outreach/campaigns`      | Generate seasonal campaign items     |
| GET    | `/api/outreach/settings`       | Get settings (credentials masked)    |
| PUT    | `/api/outreach/settings`       | Update settings and credentials      |

---

## Navigation & Integration

### Sidebar

- Add "Outreach" to Main nav, between "Customers" and "Accounts Payable".
- Icon: `icon-[tabler--mail-forward]` (distinct from PM Outreach's `icon-[tabler--send]`)
- Badge showing total pending count across all types.
- Remove "PM Outreach" from sidebar (deprecated by Outreach Hub).

### Routes

- `/app/outreach` — Outreach Hub page.
- No sub-routes; tabs and modals handle everything within the page.

### Sync Integration

- After existing ServiceTitan sync completes, call `OutreachService.GenerateOutreachItemsAsync(tenantId)`.
- Keeps outreach suggestions fresh without a separate background job.

---

## Scope Summary

| Component             | Count | Details                                           |
|-----------------------|-------|---------------------------------------------------|
| Prerequisites         | 1     | Add Job.CompletedOn field + sync update            |
| New models            | 3     | OutreachTemplate, OutreachItem, OutreachSettings   |
| New service           | 1     | OutreachService                                    |
| New controller        | 1     | OutreachController (16 endpoints)                  |
| New page              | 1     | Outreach Hub (5 tabs, editor, template mgmt)       |
| New integrations      | 2     | Resend (email), Twilio (SMS)                       |
| Sync modification     | 2     | Persist Job.CompletedOn + hook outreach generation |
| Nav changes           | 2     | Add Outreach, remove PM Outreach                   |
| Deprecated            | 1     | PmOutreachController (remove in follow-up)         |

---

## Out of Scope (Future)

- Embedded outreach shortcuts on PM Tracker, Customer Detail, etc. (Approach B follow-up)
- Automated sending (no human review)
- Analytics/conversion tracking (did the customer book after outreach?)
- A/B testing of templates
- Review request integration (Google/Yelp)
- PmOutreachController full removal (follow-up cleanup)
