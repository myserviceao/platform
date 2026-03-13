# Outreach Hub — Design Spec

## Overview

A human-in-the-loop customer outreach system that auto-generates suggested communications based on customer and job data, lets users review/edit/send via email or SMS, and tracks sent history. Covers PM reminders, post-service follow-ups, win-back campaigns, and seasonal campaigns.

## Principles

- **Human-in-the-loop**: The system drafts and suggests; a human reviews and sends.
- **Self-contained**: Single new page (`/app/outreach`), no changes to existing pages.
- **Template-driven**: Ships with defaults, users can create their own.
- **Value-ranked**: Win-back candidates sorted by lifetime spend and engagement history.

---

## Data Model

### OutreachTemplate

| Field       | Type     | Notes                                                        |
|-------------|----------|--------------------------------------------------------------|
| Id          | int      | PK                                                           |
| TenantId    | int      | FK to Tenants                                                |
| Name        | string   | e.g., "PM Reminder - Email"                                  |
| Type        | string   | `pm_reminder`, `post_service`, `win_back`, `seasonal`        |
| Channel     | string   | `email`, `sms`, `both`                                       |
| Subject     | string?  | Email subject line (null for SMS-only)                        |
| Body        | string   | Message body with merge tags like `{{customerName}}`         |
| IsDefault   | bool     | System-provided templates, cannot be deleted                 |
| CreatedAt   | DateTime |                                                              |
| UpdatedAt   | DateTime |                                                              |

### OutreachItem

| Field         | Type      | Notes                                                      |
|---------------|-----------|-------------------------------------------------------------|
| Id            | int       | PK                                                         |
| TenantId      | int       | FK to Tenants                                               |
| CustomerId    | int       | FK to Customers                                             |
| StCustomerId  | long      | ServiceTitan customer ID                                    |
| Type          | string    | `pm_reminder`, `post_service`, `win_back`, `seasonal`       |
| Channel       | string    | `email` or `sms`                                            |
| Status        | string    | `pending`, `sent`, `dismissed`                              |
| Subject       | string?   | Rendered email subject                                      |
| Body          | string    | Rendered message body with customer data filled in          |
| ScheduledFor  | DateTime? | Optional, for seasonal campaigns                           |
| SentAt        | DateTime? |                                                             |
| DismissedAt   | DateTime? |                                                             |
| CreatedAt     | DateTime  |                                                             |

### OutreachSettings

| Field                     | Type | Notes                              |
|---------------------------|------|------------------------------------|
| Id                        | int  | PK                                 |
| TenantId                  | int  | FK to Tenants, unique              |
| WinBackThresholdMonths    | int  | Default 12                         |
| PostServiceDelayHours     | int  | Default 48                         |
| PmReminderDaysBeforeDue   | int  | Default 30                         |

---

## Outreach Generation Logic

Runs after each ServiceTitan sync via `OutreachService.GenerateOutreachItemsAsync(tenantId)`.

**Deduplication rule (all types)**: Before creating any item, check if a pending or sent item of the same `type` + `customerId` exists within the last 30 days. If so, skip.

### PM Reminders

- Query `PmCustomers` where status is `Overdue` or `ComingDue`.
- Generate one `OutreachItem` per qualifying customer.
- Available merge tags: `{{customerName}}`, `{{lastPmDate}}`, `{{daysOverdue}}`.

### Post-Service Follow-ups

- Query `Jobs` completed within the last `PostServiceDelayHours`.
- Generate one item per completed job.
- Available merge tags: `{{customerName}}`, `{{jobType}}`, `{{technicianName}}`, `{{completionDate}}`.

### Win-Back

- Query `Customers` whose most recent completed job is older than `WinBackThresholdMonths`.
- Rank by lifetime spend (sum of invoice totals) descending, then by total job count.
- Generate one item per qualifying customer.
- Available merge tags: `{{customerName}}`, `{{lastServiceDate}}`, `{{monthsSinceService}}`, `{{lifetimeSpend}}`.

### Seasonal Campaigns

- **User-triggered**, not auto-generated.
- User picks a template, selects a customer segment (all customers, PM history customers, etc.) and optional date range.
- System bulk-generates draft `OutreachItem` records with status `pending` for review.

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

Table of pending outreach items.

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
- **Bulk actions**: Select multiple rows, "Send Selected", "Dismiss Selected".

### Message Editor (modal)

- Template selector dropdown (defaults to auto-selected template for the type).
- Subject line field (email only).
- Body textarea with rendered merge tags shown as actual customer values.
- Channel toggle (email / SMS).
- Live preview of the final message.
- Send button.

### Sent History Tab

- Table: customer, type, channel, message preview, sent date.
- Filterable by type and date range.

---

## Template Management

Accessible from a "Manage Templates" link within the Outreach Hub.

### Default Templates

- 1 email + 1 SMS template per outreach type = 8 total defaults.
- Marked `IsDefault = true`. Cannot be deleted, but can be duplicated and edited.

### Template Editor

- List view grouped by type.
- Create/edit form: name, type, channel, subject (email), body.
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
- API key stored in tenant configuration.

### SMS — Twilio

- Account SID, Auth Token, and "from" phone number stored in tenant configuration.
- SMS templates should target under 160 characters.

### Configuration

A settings section within the Outreach Hub (or under app Settings) where the tenant enters:
- Resend API key + from email address
- Twilio Account SID, Auth Token, from phone number
- Outreach thresholds (win-back months, post-service delay, PM reminder days)

Sending is disabled until credentials are configured. The UI shows a setup prompt if missing.

---

## Backend Architecture

### OutreachService

- `GenerateOutreachItemsAsync(tenantId)` — runs generation logic for all types, respects deduplication.
- `SendOutreachItemAsync(itemId, channel)` — renders final message, sends via Resend or Twilio, updates status to `sent`.
- `DismissOutreachItemAsync(itemId)` — marks as `dismissed`.
- `BulkSendAsync(itemIds)` — sends multiple items.
- `RenderTemplate(template, customerData)` — replaces merge tags with actual values.

### OutreachController — `/api/outreach`

| Method | Route                        | Description                        |
|--------|------------------------------|------------------------------------|
| GET    | `/api/outreach`              | List items (filter by type, status) |
| GET    | `/api/outreach/stats`        | Pending/sent counts by type        |
| PUT    | `/api/outreach/{id}`         | Edit draft (subject, body, channel) |
| POST   | `/api/outreach/{id}/send`    | Send single item                   |
| POST   | `/api/outreach/bulk-send`    | Send selected items                |
| POST   | `/api/outreach/{id}/dismiss` | Dismiss item                       |
| GET    | `/api/outreach/templates`    | List templates                     |
| POST   | `/api/outreach/templates`    | Create template                    |
| PUT    | `/api/outreach/templates/{id}` | Edit template                    |
| DELETE | `/api/outreach/templates/{id}` | Delete (non-default only)        |
| POST   | `/api/outreach/campaigns`    | Generate seasonal campaign items   |
| GET    | `/api/outreach/settings`     | Get thresholds and credentials     |
| PUT    | `/api/outreach/settings`     | Update thresholds and credentials  |

---

## Navigation & Integration

### Sidebar

- Add "Outreach" to Main nav, between "Customers" and "Accounts Payable".
- Icon: `icon-[tabler--send]`
- Badge showing total pending count across all types.

### Routes

- `/app/outreach` — Outreach Hub page.
- No sub-routes; tabs and modals handle everything within the page.

### Sync Integration

- After existing ServiceTitan sync completes, call `OutreachService.GenerateOutreachItemsAsync(tenantId)`.
- Keeps outreach suggestions fresh without a separate background job.

---

## Scope Summary

| Component          | Count | Details                                        |
|--------------------|-------|------------------------------------------------|
| New models         | 3     | OutreachTemplate, OutreachItem, OutreachSettings |
| New service        | 1     | OutreachService                                |
| New controller     | 1     | OutreachController (14 endpoints)              |
| New page           | 1     | Outreach Hub (5 tabs, editor, template mgmt)   |
| New integrations   | 2     | Resend (email), Twilio (SMS)                   |
| Sync modification  | 1     | Hook into existing sync to generate items      |
| Nav change         | 1     | Add Outreach to sidebar                        |
| Existing page changes | 0  | Self-contained (Approach A)                    |

---

## Out of Scope (Future)

- Embedded outreach shortcuts on PM Tracker, Customer Detail, etc. (Approach B follow-up)
- Automated sending (no human review)
- Analytics/conversion tracking (did the customer book after outreach?)
- A/B testing of templates
- Review request integration (Google/Yelp)
