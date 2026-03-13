# AR Alerts — Design Spec

## Overview

A full collections workflow page that shows outstanding AR by aging bucket, lets users log contact attempts, track collection status (active, payment plan, sent to collections, written off), and auto-generates escalating outreach reminders at 15/30/60/90 day thresholds.

## Data Model

### ArContactLog

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| TenantId | int | FK to Tenants |
| CustomerId | int | FK to Customers |
| ContactType | string | `call`, `email`, `text` |
| Outcome | string | `left_voicemail`, `spoke_with`, `promised_to_pay`, `disputed`, `no_answer`, `wrong_number` |
| Notes | string? | Free-text |
| FollowUpDate | DateTime? | Optional scheduled follow-up |
| CreatedAt | DateTime | |

### ArStatus

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| TenantId | int | FK to Tenants |
| CustomerId | int | FK to Customers (unique per tenant) |
| Status | string | `active`, `payment_plan`, `sent_to_collections`, `written_off` |
| PaymentPlanAmount | decimal? | Monthly amount if on plan |
| PaymentPlanNote | string? | |
| StatusChangedAt | DateTime | |
| UpdatedAt | DateTime | |

No changes to existing Invoice or Customer models. Aging computed from Invoice.InvoiceDate and Invoice.BalanceRemaining.

---

## AR Alerts Page

Route: `/app/ar-alerts`

### Header
- Title: "AR Alerts"
- Summary cards: Total Outstanding, 15+ Days, 30+ Days, 60+ Days, 90+ Days (dollar amount + invoice count)

### Filters
- Search by customer name
- Aging bucket: All, 15+, 30+, 60+, 90+
- Status: All, Active, Payment Plan, Sent to Collections, Written Off

### Customer Table

| Column | Notes |
|--------|-------|
| Customer | Name + avatar, click to expand |
| Total Owed | Sum of outstanding balances |
| Oldest Invoice | Age in days |
| Invoices | Count of open invoices |
| Status | Badge |
| Last Contact | Date or "Never" |
| Actions | Log Contact, Send Reminder, Change Status |

### Expanded Row
- Individual invoices: #, date, amount, balance, age
- Contact history: chronological log with type, outcome, notes
- Status controls: dropdown + payment plan fields

### Log Contact Modal
- Contact type: Call / Email / Text
- Outcome: dropdown
- Notes: textarea
- Follow-up date: optional
- Save button

### Send Reminder
Opens native email/SMS client with pre-filled AR reminder (same pattern as Outreach Hub).

---

## Outreach Integration

### Auto-generated AR Reminders
During sync, after outreach generation, check invoices against thresholds:
- 15 days — gentle reminder
- 30 days — firmer
- 60 days — urgent
- 90 days — final notice

Type: `ar_reminder`. Dedup: skip if pending/sent item for same customer + threshold within 30 days. Each threshold tracked separately.

Customers with status `payment_plan`, `sent_to_collections`, or `written_off` excluded from auto-generation.

### Default Templates
8 total (4 email + 4 SMS). Merge tags: `{{customerName}}`, `{{companyName}}`, `{{totalOwed}}`, `{{oldestInvoiceAge}}`, `{{invoiceCount}}`.

### Sidebar Badge
Count of customers with invoices 15+ days overdue.

---

## Backend

### ArAlertsService
- `GetAgingSummaryAsync(tenantId)`
- `GetCustomerArDetailsAsync(tenantId, filters)`
- `LogContactAsync(tenantId, customerId, log)`
- `UpdateStatusAsync(tenantId, customerId, status, amount, note)`
- `GenerateArRemindersAsync(tenantId)` — called during sync

### ArAlertsController — `/api/ar-alerts`

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/ar-alerts/summary` | Aging bucket totals |
| GET | `/api/ar-alerts` | Customer AR list (filtered, paginated) |
| GET | `/api/ar-alerts/{customerId}` | Customer invoices + contact log |
| POST | `/api/ar-alerts/{customerId}/contact` | Log contact |
| PUT | `/api/ar-alerts/{customerId}/status` | Update status |

---

## Navigation
- AR Alerts already in sidebar nav with `icon-[tabler--alert-circle]`
- Add badge showing count of customers with 15+ day overdue invoices
