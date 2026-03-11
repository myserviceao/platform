using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services.ServiceTitan;

public class ServiceTitanSyncService
{
    private readonly AppDbContext _db;
    private readonly ServiceTitanClient _client;
    private readonly ServiceTitanOAuthService _oauth;
    private readonly ILogger<ServiceTitanSyncService> _logger;

    private static readonly string[] PmKeywords = { "maintenance", "tune up", "tune-up", "pm" };

    public ServiceTitanSyncService(AppDbContext db, ServiceTitanClient client, ServiceTitanOAuthService oauth, ILogger<ServiceTitanSyncService> logger)
    {
        _db = db; _client = client; _oauth = oauth; _logger = logger;
    }

    public async Task<SyncResult> SyncAllAsync(int tenantId)
    {
        try
        {
            var token = await _oauth.GetValidTokenAsync(tenantId);
            if (token == null)
                return new SyncResult { Success = false, Error = "Not connected to ServiceTitan" };

            var tenant = await _db.Tenants.FindAsync(tenantId);
            if (tenant?.StTenantId == null)
                return new SyncResult { Success = false, Error = "ST Tenant ID not configured" };

            var stTenantId = tenant.StTenantId;

            // 1. Load job type name map
            var jobTypeMap = await _client.GetJobTypeMapAsync(token, stTenantId);
            _logger.LogInformation("[Sync] tenantId={TenantId} jobTypes={Count}", tenantId, jobTypeMap.Count);

            // 2. Load customer name map
            var customerNameMap = await _client.GetCustomerNameMapAsync(token, stTenantId);
            _logger.LogInformation("[Sync] tenantId={TenantId} customers={Count}", tenantId, customerNameMap.Count);

            // 3. Upsert all Customers
            foreach (var (stCustomerId, customerName) in customerNameMap)
            {
                var existing = await _db.Customers
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.StCustomerId == stCustomerId);

                if (existing == null)
                {
                    _db.Customers.Add(new Customer
                    {
                        TenantId = tenantId,
                        StCustomerId = stCustomerId,
                        Name = customerName,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Name = customerName;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            await _db.SaveChangesAsync();


            // 3b. Sync Customer Contacts (phone/email)
            string? contactContinue = null;
            bool contactHasMore = true;
            int contactsSynced = 0;
            while (contactHasMore)
            {
                var raw = await _client.GetCustomerContactsExportAsync(token, stTenantId, contactContinue);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                contactHasMore = root.TryGetProperty("hasMore", out var chm) && chm.GetBoolean();
                contactContinue = root.TryGetProperty("continueFrom", out var ccf) ? ccf.GetString() : null;

                if (!root.TryGetProperty("data", out var data)) break;

                foreach (var contact in data.EnumerateArray())
                {
                    if (contact.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
                        continue;

                    long customerId = 0;
                    if (contact.TryGetProperty("customerId", out var cidProp))
                        customerId = cidProp.GetInt64();
                    if (customerId == 0) continue;

                    string contactType = "";
                    if (contact.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                        contactType = typeProp.GetString() ?? "";

                    string contactValue = "";
                    if (contact.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.String)
                        contactValue = valProp.GetString() ?? "";

                    if (string.IsNullOrEmpty(contactValue)) continue;

                    var customer = await _db.Customers
                        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.StCustomerId == customerId);

                    if (customer != null)
                    {
                        if (contactType.Equals("MobilePhone", StringComparison.OrdinalIgnoreCase)
                            || contactType.Equals("Phone", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(customer.Phone) || contactType.Equals("MobilePhone", StringComparison.OrdinalIgnoreCase))
                                customer.Phone = contactValue;
                        }
                        else if (contactType.Equals("Email", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(customer.Email))
                                customer.Email = contactValue;
                        }
                        contactsSynced++;
                    }
                }

                await _db.SaveChangesAsync();
                if (!contactHasMore || contactContinue == null) break;
            }
            _logger.LogInformation("[Sync] tenantId={TenantId} contactsSynced={Count}", tenantId, contactsSynced);


            // 3c. Sync Customer Locations (addresses for PM planning)
            string? locContinue = null;
            bool locHasMore = true;
            int locSynced = 0;
            while (locHasMore)
            {
                var raw = await _client.GetLocationsExportAsync(token, stTenantId, locContinue);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                locHasMore = root.TryGetProperty("hasMore", out var lhm) && lhm.GetBoolean();
                locContinue = root.TryGetProperty("continueFrom", out var lcf) ? lcf.GetString() : null;

                if (!root.TryGetProperty("data", out var data)) break;

                foreach (var loc in data.EnumerateArray())
                {
                    if (loc.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
                        continue;

                    var stLocId = loc.GetProperty("id").GetInt64();
                    long customerId = 0;
                    if (loc.TryGetProperty("customerId", out var cidProp))
                        customerId = cidProp.GetInt64();

                    string locName = "";
                    if (loc.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        locName = nameProp.GetString() ?? "";

                    string street = "", city = "", state = "", zip = "";
                    if (loc.TryGetProperty("address", out var addrObj) && addrObj.ValueKind == JsonValueKind.Object)
                    {
                        if (addrObj.TryGetProperty("street", out var sProp)) street = sProp.GetString() ?? "";
                        if (addrObj.TryGetProperty("city", out var cProp)) city = cProp.GetString() ?? "";
                        if (addrObj.TryGetProperty("state", out var stProp)) state = stProp.GetString() ?? "";
                        if (addrObj.TryGetProperty("zip", out var zProp)) zip = zProp.GetString() ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(street)) continue;

                    customerNameMap.TryGetValue(customerId, out var custName);

                    var existing = await _db.CustomerLocations
                        .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.StLocationId == stLocId);

                    if (existing == null)
                    {
                        _db.CustomerLocations.Add(new Models.CustomerLocation
                        {
                            TenantId = tenantId,
                            StLocationId = stLocId,
                            StCustomerId = customerId,
                            CustomerName = custName ?? locName,
                            LocationName = locName,
                            Street = street,
                            City = city,
                            State = state,
                            Zip = zip,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.StCustomerId = customerId;
                        existing.CustomerName = custName ?? existing.CustomerName;
                        existing.LocationName = locName;
                        existing.Street = street;
                        existing.City = city;
                        existing.State = state;
                        existing.Zip = zip;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }

                    locSynced++;
                }

                await _db.SaveChangesAsync();
                if (!locHasMore || locContinue == null) break;
            }
            _logger.LogInformation("[Sync] tenantId={TenantId} locationsSynced={Count}", tenantId, locSynced);


            // 3d. Sync Job Hold Reasons
            int holdReasonsSynced = 0;
            try
            {
                var holdRaw = await _client.GetJobHoldReasonsAsync(token, stTenantId);
                var holdDoc = JsonDocument.Parse(holdRaw);
                if (holdDoc.RootElement.TryGetProperty("data", out var holdArr))
                {
                    foreach (var hr in holdArr.EnumerateArray())
                    {
                        var stId = hr.GetProperty("id").GetInt64();
                        var name = hr.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                        var active = hr.TryGetProperty("active", out var aProp) && aProp.GetBoolean();

                        var existing = await _db.HoldReasons
                            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.StHoldReasonId == stId);

                        if (existing == null)
                        {
                            _db.HoldReasons.Add(new Models.HoldReason
                            {
                                TenantId = tenantId,
                                StHoldReasonId = stId,
                                Name = name,
                                Active = active,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            existing.Name = name;
                            existing.Active = active;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        holdReasonsSynced++;
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync] Failed to sync hold reasons for tenant {TenantId}", tenantId);
            }
            _logger.LogInformation("[Sync] tenantId={TenantId} holdReasonsSynced={Count}", tenantId, holdReasonsSynced);

            // Build holdReason ID -> Name map for matching during job sync
            var holdReasonIdMap = await _db.HoldReasons
                .Where(h => h.TenantId == tenantId && h.Active)
                .ToDictionaryAsync(h => h.StHoldReasonId, h => h.Name);

            // 4. Export all Jobs
            var pmDates = new Dictionary<long, DateTime>();
            int pmFound = 0;

            string? continueFrom = null;
            bool hasMore = true;
            while (hasMore)
            {
                var raw = await _client.GetJobsExportAsync(token, stTenantId, continueFrom);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                hasMore = root.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
                continueFrom = root.TryGetProperty("continueFrom", out var cf) ? cf.GetString() : null;

                if (!root.TryGetProperty("data", out var data)) break;

                foreach (var job in data.EnumerateArray())
                {
                    var stJobId = job.GetProperty("id").GetInt64();

                    long customerId = 0;
                    if (job.TryGetProperty("customerId", out var custProp))
                        customerId = custProp.GetInt64();
                    customerNameMap.TryGetValue(customerId, out var custName);
                    custName ??= "";

                    // ST export uses "jobNumber" field
                    string jobNumber = "";
                    if (job.TryGetProperty("jobNumber", out var numProp) && numProp.ValueKind == JsonValueKind.String)
                        jobNumber = numProp.GetString() ?? "";

                    // ST export uses "jobStatus" field (values: Scheduled, Dispatched, InProgress, Hold, Completed, Canceled)
                    string status = "";
                    if (job.TryGetProperty("jobStatus", out var sProp) && sProp.ValueKind == JsonValueKind.String)
                        status = sProp.GetString() ?? "";

                    long? holdReasonId = null;
                    if (job.TryGetProperty("holdReasonId", out var hrProp) && hrProp.ValueKind == JsonValueKind.Number)
                        holdReasonId = hrProp.GetInt64();

                    // Also try alternate field names
                    if (!holdReasonId.HasValue && job.TryGetProperty("jobHoldReasonId", out var jhrProp) && jhrProp.ValueKind == JsonValueKind.Number)
                        holdReasonId = jhrProp.GetInt64();

                    // Log all properties for hold jobs to debug
                    if (status == "Hold")
                    {
                        var allProps = string.Join(", ", job.EnumerateObject().Select(p => $"{p.Name}={p.Value.ValueKind}"));
                        _logger.LogInformation("[Sync] Hold job #{JobNum} props: {Props} holdReasonId={HoldId}", jobNumber, allProps, holdReasonId?.ToString() ?? "NULL");
                    }

                    string? holdReasonName = null;
                    if (holdReasonId.HasValue && holdReasonIdMap.TryGetValue(holdReasonId.Value, out var hrName))
                        holdReasonName = hrName;

                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jtProp) && jtProp.ValueKind == JsonValueKind.Number)
                        jobTypeMap.TryGetValue(jtProp.GetInt64(), out jobTypeName);

                    // ST export returns total as a decimal number
                    decimal totalAmount = 0;
                    if (job.TryGetProperty("total", out var totProp))
                    {
                        if (totProp.ValueKind == JsonValueKind.Number)
                            totalAmount = totProp.GetDecimal();
                        else if (totProp.ValueKind == JsonValueKind.String)
                            decimal.TryParse(totProp.GetString(), out totalAmount);
                    }

                    DateTime? createdOn = null;
                    if (job.TryGetProperty("createdOn", out var crProp) && crProp.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(crProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var crDate))
                            createdOn = DateTime.SpecifyKind(crDate, DateTimeKind.Utc);

                    var existingJob = await _db.Jobs
                        .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.StJobId == stJobId);

                    if (existingJob == null)
                    {
                        _db.Jobs.Add(new Job
                        {
                            TenantId = tenantId,
                            StJobId = stJobId,
                            StCustomerId = customerId,
                            CustomerName = custName,
                            JobNumber = jobNumber,
                            Status = status,
                            JobTypeName = jobTypeName,
                            HoldReasonName = holdReasonName,
                            TotalAmount = totalAmount,
                            CreatedOn = createdOn,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingJob.StCustomerId = customerId;
                        existingJob.CustomerName = custName;
                        existingJob.JobNumber = jobNumber;
                        existingJob.Status = status;
                        existingJob.JobTypeName = jobTypeName;
                        existingJob.HoldReasonName = holdReasonName;
                        existingJob.TotalAmount = totalAmount;
                        existingJob.UpdatedAt = DateTime.UtcNow;
                    }

                    if (jobTypeName != null && PmKeywords.Any(k => jobTypeName.ToLower().Contains(k)))
                    {
                        DateTime? completedOn = null;
                        if (job.TryGetProperty("completedOn", out var coProp) && coProp.ValueKind == JsonValueKind.String)
                            if (DateTime.TryParse(coProp.GetString(), null,
                                System.Globalization.DateTimeStyles.AssumeUniversal |
                                System.Globalization.DateTimeStyles.AdjustToUniversal, out var pc))
                                completedOn = DateTime.SpecifyKind(pc, DateTimeKind.Utc);

                        var pmDate = completedOn ?? createdOn;
                        if (pmDate.HasValue && customerId > 0)
                        {
                            pmFound++;
                            if (!pmDates.TryGetValue(customerId, out var existing2) || pmDate.Value > existing2)
                                pmDates[customerId] = pmDate.Value;
                        }
                    }
                }

                await _db.SaveChangesAsync();
                if (!hasMore || continueFrom == null) break;
            }

            _logger.LogInformation("[Sync] tenantId={TenantId} pmFound={PmFound} uniquePmCustomers={Count}", tenantId, pmFound, pmDates.Count);


            // The job export may not include holdReasonId, but the appointment export does
            var unresolvedHoldJobs = await _db.Jobs
                .Where(j => j.TenantId == tenantId && j.Status == "Hold" && j.HoldReasonName == null)
                .ToListAsync();

            if (unresolvedHoldJobs.Count > 0 && holdReasonIdMap.Count > 0)
            {
                _logger.LogInformation("[Sync] Resolving hold reasons for {Count} jobs via appointment export", unresolvedHoldJobs.Count);

                // Build a map of jobId -> holdReasonId from appointment export
                var jobHoldReasons = new Dictionary<long, long>();
                string? hrApptContinue = null;
                bool hrApptHasMore = true;
                while (hrApptHasMore)
                {
                    var hrApptRaw = await _client.GetAppointmentsExportAsync(token, stTenantId, hrApptContinue);
                    var hrApptDoc = JsonDocument.Parse(hrApptRaw);
                    var hrApptRoot = hrApptDoc.RootElement;
                    hrApptHasMore = hrApptRoot.TryGetProperty("hasMore", out var ahm) && ahm.GetBoolean();
                    hrApptContinue = hrApptRoot.TryGetProperty("continueFrom", out var acf) ? acf.GetString() : null;

                    if (!hrApptRoot.TryGetProperty("data", out var hrApptData)) break;

                    foreach (var appt in hrApptData.EnumerateArray())
                    {
                        var apptStatus = appt.TryGetProperty("status", out var asProp) && asProp.ValueKind == JsonValueKind.String
                            ? asProp.GetString() : "";
                        if (apptStatus != "Hold") continue;

                        long apptJobId = 0;
                        if (appt.TryGetProperty("jobId", out var jidProp) && jidProp.ValueKind == JsonValueKind.Number)
                            apptJobId = jidProp.GetInt64();

                        if (apptJobId == 0) continue;

                        // Try to get holdReasonId from the appointment
                        if (appt.TryGetProperty("holdReasonId", out var hrProp2) && hrProp2.ValueKind == JsonValueKind.Number)
                        {
                            jobHoldReasons[apptJobId] = hrProp2.GetInt64();
                        }
                    }

                    if (!hrApptHasMore || hrApptContinue == null) break;
                }

                _logger.LogInformation("[Sync] Found {Count} appointment hold reasons", jobHoldReasons.Count);

                // Now match hold reasons to jobs
                int resolved = 0;
                foreach (var hj in unresolvedHoldJobs)
                {
                    if (jobHoldReasons.TryGetValue(hj.StJobId, out var apptHoldReasonId))
                    {
                        if (holdReasonIdMap.TryGetValue(apptHoldReasonId, out var reasonName))
                        {
                            hj.HoldReasonName = reasonName;
                            resolved++;
                            _logger.LogInformation("[Sync] Job #{JobNum} hold reason: {Reason} (from appt)", hj.JobNumber, reasonName);
                        }
                    }
                }

                if (resolved > 0)
                    await _db.SaveChangesAsync();

                _logger.LogInformation("[Sync] Resolved {Resolved}/{Total} hold reasons from appointments", resolved, unresolvedHoldJobs.Count);
            }


            // 5. Upsert PmCustomers
            foreach (var (stCustomerId, lastPmDate) in pmDates)
            {
                customerNameMap.TryGetValue(stCustomerId, out var customerName);

                var daysSince = (DateTime.UtcNow - lastPmDate).Days;
                var pmStatus = daysSince >= 180 ? "Overdue"
                             : daysSince >= 120 ? "ComingDue"
                             : "Current";

                var existing = await _db.PmCustomers
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.StCustomerId == stCustomerId);

                if (existing == null)
                {
                    _db.PmCustomers.Add(new PmCustomer
                    {
                        TenantId = tenantId,
                        StCustomerId = stCustomerId,
                        CustomerName = customerName ?? $"Customer {stCustomerId}",
                        LastPmDate = lastPmDate,
                        PmStatus = pmStatus,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.CustomerName = customerName ?? existing.CustomerName;
                    existing.LastPmDate = lastPmDate;
                    existing.PmStatus = pmStatus;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            await _db.SaveChangesAsync();

            // 6. Sync Invoices (paginated export)
            string? invoiceContinueFrom = null;
            bool invoiceHasMore = true;
            int invoiceSynced = 0;

            while (invoiceHasMore)
            {
                var raw = await _client.GetInvoicesExportAsync(token, stTenantId, invoiceContinueFrom);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                invoiceHasMore = root.TryGetProperty("hasMore", out var hm2) && hm2.GetBoolean();
                invoiceContinueFrom = root.TryGetProperty("continueFrom", out var cf2) ? cf2.GetString() : null;

                if (!root.TryGetProperty("data", out var data)) break;

                foreach (var inv in data.EnumerateArray())
                {
                    var stInvoiceId = inv.GetProperty("id").GetInt64();

                    long stCustomerId = 0;
                    string custName = "";
                    if (inv.TryGetProperty("customer", out var custObj) && custObj.ValueKind == JsonValueKind.Object)
                    {
                        if (custObj.TryGetProperty("id", out var cidProp))
                            stCustomerId = cidProp.GetInt64();
                        if (custObj.TryGetProperty("name", out var cnameProp))
                            custName = cnameProp.GetString() ?? "";
                    }

                    // ST invoice export: total and balance are strings
                    decimal totalAmount = 0;
                    if (inv.TryGetProperty("total", out var totProp))
                    {
                        if (totProp.ValueKind == JsonValueKind.String)
                            decimal.TryParse(totProp.GetString(), out totalAmount);
                        else if (totProp.ValueKind == JsonValueKind.Number)
                            totalAmount = totProp.GetDecimal();
                    }

                    decimal balance = 0;
                    if (inv.TryGetProperty("balance", out var balProp))
                    {
                        if (balProp.ValueKind == JsonValueKind.String)
                            decimal.TryParse(balProp.GetString(), out balance);
                        else if (balProp.ValueKind == JsonValueKind.Number)
                            balance = balProp.GetDecimal();
                    }

                    DateTime invoiceDate = DateTime.MinValue;
                    if (inv.TryGetProperty("invoiceDate", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(dateProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate))
                            invoiceDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

                    var existing = await _db.Invoices
                        .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.StInvoiceId == stInvoiceId);

                    if (existing == null)
                    {
                        _db.Invoices.Add(new Invoice
                        {
                            TenantId = tenantId,
                            StInvoiceId = stInvoiceId,
                            StCustomerId = stCustomerId,
                            CustomerName = custName,
                            TotalAmount = totalAmount,
                            BalanceRemaining = balance,
                            InvoiceDate = invoiceDate != DateTime.MinValue ? invoiceDate : DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.StCustomerId = stCustomerId;
                        existing.CustomerName = custName;
                        existing.TotalAmount = totalAmount;
                        existing.BalanceRemaining = balance;
                        if (invoiceDate != DateTime.MinValue)
                            existing.InvoiceDate = invoiceDate;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }

                    invoiceSynced++;
                }

                await _db.SaveChangesAsync();
                if (!invoiceHasMore || invoiceContinueFrom == null) break;
            }

            _logger.LogInformation("[Sync] tenantId={TenantId} invoicesSynced={Count}", tenantId, invoiceSynced);

            // 7. Sync Appointments (3-day window)
            TimeZoneInfo centralZone;
            try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
            catch { try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
                    catch { centralZone = TimeZoneInfo.Utc; } }

            var nowCentral = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralZone);
            var windowStart = TimeZoneInfo.ConvertTimeToUtc(nowCentral.Date, centralZone);
            var windowEnd   = TimeZoneInfo.ConvertTimeToUtc(nowCentral.Date.AddDays(3), centralZone);

            var oldAppts = await _db.Appointments
                .Where(a => a.TenantId == tenantId && (a.Start < windowStart || a.Start >= windowEnd))
                .ToListAsync();
            _db.Appointments.RemoveRange(oldAppts);
            await _db.SaveChangesAsync();

            int page = 1;
            int apptSynced = 0;
            bool apptHasMore = true;

            while (apptHasMore)
            {
                var raw = await _client.GetAppointmentsAsync(token, stTenantId, windowStart, windowEnd, page);
                var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                apptHasMore = root.TryGetProperty("hasMore", out var hm3) && hm3.GetBoolean();

                if (!root.TryGetProperty("data", out var data)) break;

                var stApptIdsThisPage = new List<long>();

                foreach (var appt in data.EnumerateArray())
                {
                    var stApptId = appt.GetProperty("id").GetInt64();
                    stApptIdsThisPage.Add(stApptId);

                    long stJobId = 0;
                    if (appt.TryGetProperty("jobId", out var jidProp) && jidProp.ValueKind == JsonValueKind.Number)
                        stJobId = jidProp.GetInt64();

                    string apptStatus = "";
                    if (appt.TryGetProperty("status", out var asProp) && asProp.ValueKind == JsonValueKind.String)
                        apptStatus = asProp.GetString() ?? "";

                    DateTime apptStart = DateTime.UtcNow;
                    if (appt.TryGetProperty("start", out var startProp) && startProp.ValueKind == JsonValueKind.String)
                        if (DateTime.TryParse(startProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var ps))
                            apptStart = DateTime.SpecifyKind(ps, DateTimeKind.Utc);

                    if (apptStatus.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
                        apptStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var matchedJob = stJobId > 0
                        ? await _db.Jobs.FirstOrDefaultAsync(j => j.TenantId == tenantId && j.StJobId == stJobId)
                        : null;

                    var existing = await _db.Appointments
                        .Include(a => a.Technicians)
                        .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.StAppointmentId == stApptId);

                    if (existing == null)
                    {
                        existing = new Appointment
                        {
                            TenantId = tenantId,
                            StAppointmentId = stApptId,
                            StJobId = stJobId,
                            JobNumber = matchedJob?.JobNumber ?? "",
                            CustomerName = matchedJob?.CustomerName ?? "",
                            Status = apptStatus,
                            Start = apptStart,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.Appointments.Add(existing);
                    }
                    else
                    {
                        existing.StJobId = stJobId;
                        existing.JobNumber = matchedJob?.JobNumber ?? existing.JobNumber;
                        existing.CustomerName = matchedJob?.CustomerName ?? existing.CustomerName;
                        existing.Status = apptStatus;
                        existing.Start = apptStart;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _db.AppointmentTechnicians.RemoveRange(existing.Technicians);
                    }

                    apptSynced++;
                }

                await _db.SaveChangesAsync();

                if (stApptIdsThisPage.Count > 0)
                {
                    try
                    {
                        var assignRaw = await _client.GetAppointmentAssignmentsAsync(token, stTenantId, stApptIdsThisPage);
                        var assignDoc = JsonDocument.Parse(assignRaw);
                        if (assignDoc.RootElement.TryGetProperty("data", out var assignments))
                        {
                            var techMap = new Dictionary<long, List<string>>();
                            foreach (var a in assignments.EnumerateArray())
                            {
                                long aId = 0;
                                if (a.TryGetProperty("appointmentId", out var aIdProp) && aIdProp.ValueKind == JsonValueKind.Number)
                                    aId = aIdProp.GetInt64();

                                string techName = "";
                                if (a.TryGetProperty("technicianName", out var tnProp))
                                {
                                    if (tnProp.ValueKind == JsonValueKind.String)
                                        techName = tnProp.GetString() ?? "";
                                    else if (tnProp.ValueKind == JsonValueKind.Object)
                                    {
                                        var fn = tnProp.TryGetProperty("firstName", out var fp) ? fp.GetString() ?? "" : "";
                                        var ln = tnProp.TryGetProperty("lastName", out var lp) ? lp.GetString() ?? "" : "";
                                        techName = $"{fn} {ln}".Trim();
                                    }
                                }
                                if (string.IsNullOrEmpty(techName) && a.TryGetProperty("employee", out var empProp) && empProp.ValueKind == JsonValueKind.Object)
                                {
                                    if (empProp.TryGetProperty("name", out var empName))
                                        techName = empName.GetString() ?? "";
                                }

                                if (aId > 0 && !string.IsNullOrEmpty(techName))
                                {
                                    if (!techMap.ContainsKey(aId)) techMap[aId] = new List<string>();
                                    if (!techMap[aId].Contains(techName))
                                        techMap[aId].Add(techName);
                                }
                            }

                            foreach (var (apptStId, techs) in techMap)
                            {
                                var apptEntity = await _db.Appointments
                                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.StAppointmentId == apptStId);
                                if (apptEntity == null) continue;
                                foreach (var tech in techs)
                                {
                                    _db.AppointmentTechnicians.Add(new AppointmentTechnician
                                    {
                                        AppointmentId = apptEntity.Id,
                                        TechnicianName = tech
                                    });
                                }
                            }

                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Sync] Appointment assignment fetch failed (non-fatal)");
                    }
                }

                page++;
                if (!apptHasMore) break;
            }

            _logger.LogInformation("[Sync] tenantId={TenantId} appointmentsSynced={Count}", tenantId, apptSynced);

            // Update snapshot timestamp
            var snapshot = await _db.DashboardSnapshots.FirstOrDefaultAsync(s => s.TenantId == tenantId);
            if (snapshot == null)
            {
                snapshot = new DashboardSnapshot { TenantId = tenantId };
                _db.DashboardSnapshots.Add(snapshot);
            }
            snapshot.SnapshotTakenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new SyncResult { Success = true, SyncedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Failed for tenantId={TenantId}", tenantId);
            return new SyncResult { Success = false, Error = ex.Message };
        }
    }






    public async Task<string> GetRawApptExportAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetAppointmentsExportAsync(token, tenant.StTenantId);
    }

    public async Task<string> GetHoldJobsRawAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobsByStatusAsync(token, tenant.StTenantId, "Hold");
    }

    public async Task<string> GetAppointmentRawAsync(int tenantId, long appointmentId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetAppointmentAsync(token, tenant.StTenantId, appointmentId);
    }

    public async Task<string> GetRawJobExportAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobsExportAsync(token, tenant.StTenantId);
    }

    public async Task<string> GetJobRawAsync(int tenantId, long stJobId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobAsync(token, tenant.StTenantId, stJobId);
    }

    public async Task<string> GetJobHistoryRawAsync(int tenantId, long stJobId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobHistoryAsync(token, tenant.StTenantId, stJobId);
    }
}




public class SyncResult
{
    public bool Success { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? Error { get; set; }
}            // 4c. Resolve hold reasons from appointment export
