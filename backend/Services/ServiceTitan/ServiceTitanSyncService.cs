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
            // Force fresh token to avoid 401 errors from stale cache
            var token = await _oauth.ForceRefreshAsync(tenantId);
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


            // 3b. Sync Job Hold Reasons + Tag Mappings
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
                        var existingHr = await _db.HoldReasons
                            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.StHoldReasonId == stId);
                        if (existingHr == null)
                            _db.HoldReasons.Add(new HoldReason { TenantId = tenantId, StHoldReasonId = stId, Name = name, Active = active, UpdatedAt = DateTime.UtcNow });
                        else { existingHr.Name = name; existingHr.Active = active; existingHr.UpdatedAt = DateTime.UtcNow; }
                        holdReasonsSynced++;
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Sync] Failed to sync hold reasons"); }
            _logger.LogInformation("[Sync] holdReasonsSynced={Count}", holdReasonsSynced);

            // 3c. Sync ST Tag Types and auto-map to hold reasons by name
            try
            {
                var tagRaw = await _client.GetTagTypesAsync(token, stTenantId);
                var tagDoc = JsonDocument.Parse(tagRaw);
                if (tagDoc.RootElement.TryGetProperty("data", out var tagArr))
                {
                    var holdReasons = await _db.HoldReasons
                        .Where(h => h.TenantId == tenantId && h.Active)
                        .ToListAsync();

                    foreach (var tag in tagArr.EnumerateArray())
                    {
                        var tagId = tag.GetProperty("id").GetInt64();
                        var tagName = tag.TryGetProperty("name", out var tnProp) ? tnProp.GetString() ?? "" : "";

                        // Auto-map: if tag name matches a hold reason name, link them
                        var matchedReason = holdReasons.FirstOrDefault(h =>
                            h.StTagTypeId == null &&
                            h.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                        if (matchedReason != null)
                        {
                            matchedReason.StTagTypeId = tagId;
                            _logger.LogInformation("[Sync] Auto-mapped tag '{Tag}' (id={TagId}) to hold reason '{Reason}'", tagName, tagId, matchedReason.Name);
                        }

                        // Also check if any hold reason already has this tag mapped
                        var alreadyMapped = holdReasons.FirstOrDefault(h => h.StTagTypeId == tagId);
                        if (alreadyMapped != null && alreadyMapped.Name != tagName)
                        {
                            // Tag still exists, keep mapping
                        }
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Sync] Failed to sync tag types"); }

            // Build tag-to-hold-reason lookup for job matching
            var tagToHoldReason = await _db.HoldReasons
                .Where(h => h.TenantId == tenantId && h.Active && h.StTagTypeId != null)
                .ToDictionaryAsync(h => h.StTagTypeId!.Value, h => h.Name);

            _logger.LogInformation("[Sync] Tag-to-hold-reason mappings: {Count}", tagToHoldReason.Count);

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

                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jtProp) && jtProp.ValueKind == JsonValueKind.Number)
                        jobTypeMap.TryGetValue(jtProp.GetInt64(), out jobTypeName);

                    // Parse tagTypeIds
                    string? tagTypeIdsStr = null;
                    if (job.TryGetProperty("tagTypeIds", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                    {
                        var tagIds = new List<string>();
                        foreach (var t in tagsProp.EnumerateArray())
                            if (t.ValueKind == JsonValueKind.Number) tagIds.Add(t.GetInt64().ToString());
                        tagTypeIdsStr = tagIds.Count > 0 ? string.Join(",", tagIds) : null;
                    }

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
                            TotalAmount = totalAmount,
                            CreatedOn = createdOn,
                            UpdatedAt = DateTime.UtcNow
                        ,
                            HoldReasonName = status == "Hold" && tagToHoldReason.Count > 0
                                ? (job.TryGetProperty("tagTypeIds", out var ntProp) && ntProp.ValueKind == JsonValueKind.Array
                                    ? ntProp.EnumerateArray()
                                        .Select(t => t.GetInt64())
                                        .Where(t => tagToHoldReason.ContainsKey(t))
                                        .Select(t => tagToHoldReason[t])
                                        .FirstOrDefault()
                                    : null)
                                : null
                        });
                    }
                    else
                    {
                        existingJob.StCustomerId = customerId;
                        existingJob.CustomerName = custName;
                        existingJob.JobNumber = jobNumber;
                        existingJob.Status = status;
                        existingJob.JobTypeName = jobTypeName;
                        existingJob.TotalAmount = totalAmount;
                        existingJob.TagTypeIds = tagTypeIdsStr;
                        existingJob.UpdatedAt = DateTime.UtcNow;

                        // Auto-assign hold reason from tags
                        if (status == "Hold" && tagToHoldReason.Count > 0)
                        {
                            if (job.TryGetProperty("tagTypeIds", out var tagsProp2) && tagsProp2.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var tagEl in tagsProp2.EnumerateArray())
                                {
                                    var tagId = tagEl.GetInt64();
                                    if (tagToHoldReason.TryGetValue(tagId, out var holdName))
                                    {
                                        existingJob.HoldReasonName = holdName;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (status != "Hold")
                        {
                            existingJob.HoldReasonName = null; // clear if no longer on hold
                        }
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



            // 4d. Resolve hold reasons from ST Reporting API (Appointments report)
            // The Reporting API returns ApptHoldReasons for each job
            var holdJobsToResolve = await _db.Jobs
                .Where(j => j.TenantId == tenantId && j.Status == "Hold" && j.HoldReasonName == null)
                .ToListAsync();

            if (holdJobsToResolve.Count > 0)
            {
                _logger.LogInformation("[Sync] Resolving hold reasons from Reporting API for {Count} jobs", holdJobsToResolve.Count);
                int reportResolved = 0;

                try
                {
                    // Call Appointments report (id=3448) with DateType=2
                    var reportRaw = await _client.GetReportDataAsync(token, stTenantId, "operations", 3448,
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            parameters = new[]
                            {
                                new { name = "DateType", value = "2" },
                                new { name = "From", value = "2025-01-01" },
                                new { name = "To", value = DateTime.UtcNow.AddMonths(6).ToString("yyyy-MM-dd") }
                            }
                        }));

                    var reportDoc = JsonDocument.Parse(reportRaw);
                    var reportRoot = reportDoc.RootElement;

                    // Build a map of jobNumber -> holdReason from report data
                    var holdReasonMap = new Dictionary<string, string>();
                    if (reportRoot.TryGetProperty("data", out var reportData))
                    {
                        foreach (var row in reportData.EnumerateArray())
                        {
                            if (row.GetArrayLength() < 19) continue;
                            var jobNumber = row[0].GetString() ?? "";
                            var holdsOnHold = row[17].ValueKind == JsonValueKind.Number ? row[17].GetInt32() : 0;
                            var holdReason = row[18].ValueKind == JsonValueKind.String ? row[18].GetString() : null;

                            if (holdsOnHold > 0 && !string.IsNullOrWhiteSpace(holdReason))
                                holdReasonMap[jobNumber] = holdReason;
                        }
                    }

                    // Also build jobNumber -> locationAddress map
                    var locationMap = new Dictionary<string, string>();
                    if (reportRoot.TryGetProperty("data", out var reportData2))
                    {
                        foreach (var row in reportData2.EnumerateArray())
                        {
                            if (row.GetArrayLength() < 15) continue;
                            var jobNumber = row[0].GetString() ?? "";
                            var location = row[14].ValueKind == JsonValueKind.String ? row[14].GetString() : null;
                            if (!string.IsNullOrWhiteSpace(location))
                                locationMap[jobNumber] = location!;
                        }
                    }

                    // Update appointments with location addresses
                    var allAppts = await _db.Appointments
                        .Where(a => a.TenantId == tenantId)
                        .ToListAsync();
                    int locUpdated = 0;
                    foreach (var appt in allAppts)
                    {
                        if (locationMap.TryGetValue(appt.JobNumber, out var locAddr) && appt.LocationName != locAddr)
                        {
                            appt.LocationName = locAddr;
                            locUpdated++;
                        }
                    }
                    if (locUpdated > 0)
                    {
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("[Sync] Updated {Count} appointments with location addresses from report", locUpdated);
                    }

                    _logger.LogInformation("[Sync] Report returned {Count} jobs with hold reasons", holdReasonMap.Count);

                    // Match to unresolved hold jobs
                    foreach (var hj in holdJobsToResolve)
                    {
                        if (holdReasonMap.TryGetValue(hj.JobNumber, out var reason))
                        {
                            hj.HoldReasonName = reason;
                            reportResolved++;
                            _logger.LogInformation("[Sync] Job #{JobNum}: {Reason} (from report)", hj.JobNumber, reason);
                        }
                    }

                    // Also update ALL hold jobs (even previously resolved) if report has newer data
                    var allHoldJobs = await _db.Jobs
                        .Where(j => j.TenantId == tenantId && j.Status == "Hold")
                        .ToListAsync();
                    foreach (var hj in allHoldJobs)
                    {
                        if (holdReasonMap.TryGetValue(hj.JobNumber, out var reason) && hj.HoldReasonName != reason)
                        {
                            hj.HoldReasonName = reason;
                            _logger.LogInformation("[Sync] Job #{JobNum}: updated to {Reason}", hj.JobNumber, reason);
                        }
                    }

                    if (reportResolved > 0 || _db.ChangeTracker.HasChanges())
                        await _db.SaveChangesAsync();

                    _logger.LogInformation("[Sync] Resolved {Resolved}/{Total} hold reasons from report", reportResolved, holdJobsToResolve.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Sync] Reporting API failed, falling back to job history");

                    // Fallback: try job history for each unresolved hold job
                    foreach (var hj in holdJobsToResolve)
                    {
                        try
                        {
                            var histRaw = await _client.GetJobHistoryAsync(token, stTenantId, hj.StJobId);
                            var histDoc = JsonDocument.Parse(histRaw);
                            if (histDoc.RootElement.TryGetProperty("history", out var histArr))
                            {
                                string? holdReason = null;
                                DateTime latest = DateTime.MinValue;
                                foreach (var evt in histArr.EnumerateArray())
                                {
                                    var eventType = evt.TryGetProperty("eventType", out var etProp) ? etProp.GetString() : "";
                                    if (eventType != "Job Hold") continue;
                                    var memo = evt.TryGetProperty("memo", out var mProp) ? mProp.GetString() : null;
                                    if (string.IsNullOrWhiteSpace(memo)) continue;
                                    var date = evt.TryGetProperty("date", out var dProp) && dProp.ValueKind == JsonValueKind.String
                                        ? DateTime.Parse(dProp.GetString()!) : DateTime.MinValue;
                                    if (date > latest) { latest = date; holdReason = memo; }
                                }
                                if (holdReason != null)
                                {
                                    hj.HoldReasonName = holdReason;
                                    _logger.LogInformation("[Sync] Job #{JobNum}: {Reason} (from history fallback)", hj.JobNumber, holdReason);
                                }
                            }
                        }
                        catch { }
                    }
                    await _db.SaveChangesAsync();
                }
            }

            // Also clear hold reason for jobs no longer on hold
            var noLongerHold = await _db.Jobs
                .Where(j => j.TenantId == tenantId && j.Status != "Hold" && j.HoldReasonName != null)
                .ToListAsync();
            if (noLongerHold.Count > 0)
            {
                foreach (var j in noLongerHold) j.HoldReasonName = null;
                await _db.SaveChangesAsync();
                _logger.LogInformation("[Sync] Cleared hold reason for {Count} jobs no longer on hold", noLongerHold.Count);
            }


            // 4e. Sync Vendors from ST Inventory API
            try
            {
                var vendorsRaw = await _client.GetVendorsAsync(token, stTenantId);
                var vendorsDoc = JsonDocument.Parse(vendorsRaw);
                if (vendorsDoc.RootElement.TryGetProperty("data", out var vendorArr))
                {
                    int vendorSynced = 0;
                    foreach (var v in vendorArr.EnumerateArray())
                    {
                        var stVendorId = v.TryGetProperty("id", out var vid) ? vid.GetInt64() : 0;
                        if (stVendorId == 0) continue;
                        var name = v.TryGetProperty("name", out var vn) ? vn.GetString() ?? "" : "";

                        var existing = await _db.Vendors.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StVendorId == stVendorId);
                        if (existing == null)
                        {
                            existing = new Vendor { TenantId = tenantId, StVendorId = stVendorId, Name = name };
                            _db.Vendors.Add(existing);
                        }
                        else
                        {
                            existing.Name = name;
                        }
                        vendorSynced++;
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("[Sync] Synced {Count} vendors", vendorSynced);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Sync] Vendor sync failed"); }

            // 4f. Sync Purchase Orders from ST Inventory API
            try
            {
                var poRaw = await _client.GetPurchaseOrdersAsync(token, stTenantId);
                var poDoc = JsonDocument.Parse(poRaw);
                if (poDoc.RootElement.TryGetProperty("data", out var poArr))
                {
                    int poSynced = 0;
                    foreach (var po in poArr.EnumerateArray())
                    {
                        var stPoId = po.TryGetProperty("id", out var pid) ? pid.GetInt64() : 0;
                        if (stPoId == 0) continue;

                        var poNumber = po.TryGetProperty("number", out var pn) ? pn.GetString() ?? "" : "";
                        var status = po.TryGetProperty("status", out var ps) ? ps.GetString() ?? "" : "";
                        var vendorId = po.TryGetProperty("vendorId", out var pv) ? pv.GetInt64() : 0;
                        var jobId = po.TryGetProperty("jobId", out var pj) && pj.ValueKind == JsonValueKind.Number ? (long?)pj.GetInt64() : null;
                        var total = po.TryGetProperty("total", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetDecimal() : 0;
                        var tax = po.TryGetProperty("tax", out var ptx) && ptx.ValueKind == JsonValueKind.Number ? ptx.GetDecimal() : 0;
                        var shipping = po.TryGetProperty("shipping", out var psh) && psh.ValueKind == JsonValueKind.Number ? psh.GetDecimal() : 0;
                        var summary = po.TryGetProperty("summary", out var psm) ? psm.GetString() : null;
                        var date = po.TryGetProperty("date", out var pd) && pd.ValueKind == JsonValueKind.String ? DateTime.Parse(pd.GetString()!) : DateTime.UtcNow;
                        var requiredOn = po.TryGetProperty("requiredOn", out var pr) && pr.ValueKind == JsonValueKind.String ? (DateTime?)DateTime.Parse(pr.GetString()!) : null;
                        var sentOn = po.TryGetProperty("sentOn", out var pso) && pso.ValueKind == JsonValueKind.String ? (DateTime?)DateTime.Parse(pso.GetString()!) : null;
                        var receivedOn = po.TryGetProperty("receivedOn", out var pro) && pro.ValueKind == JsonValueKind.String ? (DateTime?)DateTime.Parse(pro.GetString()!) : null;

                        // Look up vendor name
                        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.TenantId == tenantId && v.StVendorId == vendorId);
                        var vendorName = vendor?.Name ?? "";

                        // Look up job number
                        var job = jobId.HasValue ? await _db.Jobs.FirstOrDefaultAsync(j => j.TenantId == tenantId && j.StJobId == jobId.Value) : null;

                        var existing = await _db.PurchaseOrders
                            .Include(p => p.Items)
                            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.StPurchaseOrderId == stPoId);

                        if (existing == null)
                        {
                            existing = new PurchaseOrder
                            {
                                TenantId = tenantId, StPurchaseOrderId = stPoId, Number = poNumber,
                                Status = status, VendorName = vendorName, StVendorId = vendorId,
                                StJobId = jobId, JobNumber = job?.JobNumber,
                                Total = total, Tax = tax, Shipping = shipping, Summary = summary,
                                Date = date, RequiredOn = requiredOn, SentOn = sentOn, ReceivedOn = receivedOn
                            };
                            _db.PurchaseOrders.Add(existing);
                        }
                        else
                        {
                            existing.Number = poNumber; existing.Status = status;
                            existing.VendorName = vendorName; existing.StVendorId = vendorId;
                            existing.StJobId = jobId; existing.JobNumber = job?.JobNumber;
                            existing.Total = total; existing.Tax = tax; existing.Shipping = shipping;
                            existing.Summary = summary; existing.Date = date;
                            existing.RequiredOn = requiredOn; existing.SentOn = sentOn; existing.ReceivedOn = receivedOn;
                            _db.PurchaseOrderItems.RemoveRange(existing.Items);
                        }

                        // Parse PO items
                        if (po.TryGetProperty("items", out var itemsArr))
                        {
                            foreach (var item in itemsArr.EnumerateArray())
                            {
                                existing.Items.Add(new PurchaseOrderItem
                                {
                                    StItemId = item.TryGetProperty("id", out var iid) ? iid.GetInt64() : 0,
                                    SkuName = item.TryGetProperty("skuName", out var isn) ? isn.GetString() ?? "" : "",
                                    SkuCode = item.TryGetProperty("skuCode", out var isc) ? isc.GetString() ?? "" : "",
                                    Description = item.TryGetProperty("description", out var isd) ? isd.GetString() : null,
                                    Quantity = item.TryGetProperty("quantity", out var iq) && iq.ValueKind == JsonValueKind.Number ? iq.GetDecimal() : 0,
                                    QuantityReceived = item.TryGetProperty("quantityReceived", out var iqr) && iqr.ValueKind == JsonValueKind.Number ? iqr.GetDecimal() : 0,
                                    Cost = item.TryGetProperty("cost", out var ic) && ic.ValueKind == JsonValueKind.Number ? ic.GetDecimal() : 0,
                                    Total = item.TryGetProperty("total", out var it) && it.ValueKind == JsonValueKind.Number ? it.GetDecimal() : 0,
                                    Status = item.TryGetProperty("status", out var ist) ? ist.GetString() ?? "" : ""
                                });
                            }
                        }
                        poSynced++;
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("[Sync] Synced {Count} purchase orders", poSynced);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Sync] PO sync failed"); }

            // 4g. Sync AP Bills from ST Accounting API
            try
            {
                var billsRaw = await _client.GetApBillsAsync(token, stTenantId);
                var billsDoc = JsonDocument.Parse(billsRaw);
                if (billsDoc.RootElement.TryGetProperty("data", out var billArr))
                {
                    int billSynced = 0;
                    foreach (var b in billArr.EnumerateArray())
                    {
                        var stBillId = b.TryGetProperty("id", out var bid) ? bid.GetInt64() : 0;
                        if (stBillId == 0) continue;

                        var billAmount = b.TryGetProperty("billAmount", out var ba) ? decimal.TryParse(ba.GetString(), out var bam) ? bam : 0 : 0;
                        var status = b.TryGetProperty("status", out var bs) ? bs.GetString() : null;
                        var source = b.TryGetProperty("source", out var bsrc) ? bsrc.GetString() : null;
                        var refNum = b.TryGetProperty("referenceNumber", out var brn) ? brn.GetString() : null;
                        var summary = b.TryGetProperty("summary", out var bsm) ? bsm.GetString() : null;
                        var dueDate = b.TryGetProperty("dueDate", out var bdd) && bdd.ValueKind == JsonValueKind.String ? DateTime.Parse(bdd.GetString()!) : DateTime.UtcNow.AddDays(30);
                        var billDate = b.TryGetProperty("billDate", out var bbd) && bbd.ValueKind == JsonValueKind.String ? (DateTime?)DateTime.Parse(bbd.GetString()!) : null;
                        var poId = b.TryGetProperty("purchaseOrderId", out var bpo) && bpo.ValueKind == JsonValueKind.Number ? (long?)bpo.GetInt64() : null;
                        var doNotPay = b.TryGetProperty("doNotPay", out var bdnp) && bdnp.ValueKind == JsonValueKind.True;
                        var canceled = b.TryGetProperty("dateCanceled", out var bdc) && bdc.ValueKind == JsonValueKind.String;

                        // Get vendor name
                        var vendorName = "";
                        if (b.TryGetProperty("vendor", out var bv) && bv.ValueKind == JsonValueKind.Object)
                            vendorName = bv.TryGetProperty("name", out var bvn) ? bvn.GetString() ?? "" : "";

                        // Find or create vendor
                        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Name == vendorName);
                        if (vendor == null && !string.IsNullOrEmpty(vendorName))
                        {
                            vendor = new Vendor { TenantId = tenantId, Name = vendorName };
                            _db.Vendors.Add(vendor);
                            await _db.SaveChangesAsync();
                        }

                        var existing = await _db.ApBills.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StApBillId == stBillId);
                        if (existing == null)
                        {
                            existing = new ApBill
                            {
                                TenantId = tenantId, StApBillId = stBillId, VendorId = vendor?.Id,
                                InvoiceNumber = refNum ?? "", Amount = billAmount, DueDate = dueDate,
                                IsPaid = canceled || status == "Reconciled", StPurchaseOrderId = poId,
                                Status = status, Source = source, ReferenceNumber = refNum,
                                Summary = summary, BillDate = billDate
                            };
                            _db.ApBills.Add(existing);
                        }
                        else
                        {
                            existing.VendorId = vendor?.Id ?? existing.VendorId;
                            existing.Amount = billAmount; existing.DueDate = dueDate;
                            existing.IsPaid = canceled || status == "Reconciled";
                            existing.StPurchaseOrderId = poId; existing.Status = status;
                            existing.Source = source; existing.ReferenceNumber = refNum;
                            existing.Summary = summary; existing.BillDate = billDate;
                        }
                        billSynced++;
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("[Sync] Synced {Count} AP bills", billSynced);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[Sync] AP Bills sync failed"); }

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
                    if (inv.TryGetProperty("date", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
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
                            LocationName = matchedJob?.CustomerName ?? "",
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
                        existing.LocationName = matchedJob?.CustomerName ?? existing.LocationName;
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

    public async Task<string> GetJobHistoryRawAsync(int tenantId, long stJobId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobHistoryAsync(token, tenant.StTenantId, stJobId);
    }

    public async Task<string> GetJobRawAsync(int tenantId, long stJobId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobAsync(token, tenant.StTenantId, stJobId);
    }

    public async Task<string> GetRawJobExportAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobsExportAsync(token, tenant.StTenantId);
    }

    public async Task<string> GetHoldJobsRawAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetJobsByStatusAsync(token, tenant.StTenantId, "Hold");
    }

    public async Task<string> GetRawApptExportAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetAppointmentsExportAsync(token, tenant.StTenantId);
    }

    public async Task<string> GetAppointmentRawAsync(int tenantId, long appointmentId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetAppointmentAsync(token, tenant.StTenantId, appointmentId);
    }

    public async Task<string> GetTagTypesRawAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetTagTypesAsync(token, tenant.StTenantId);
    }

    public async Task<string> GetHoldAppointmentsRawAsync(int tenantId)
    {
        var token = await _oauth.GetValidTokenAsync(tenantId);
        if (token == null) return "{}";
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return "{}";
        return await _client.GetAppointmentsByStatusAsync(token, tenant.StTenantId, "Hold");
    }

    public async Task<string?> GetTokenAsync(int tenantId)
    {
        return await _oauth.GetValidTokenAsync(tenantId);
    }

}

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? Error { get; set; }
}
