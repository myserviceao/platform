using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Models;

namespace MyServiceAO.Services;

public static class DemoSeeder
{
    public const string DemoEmail = "demo@myserviceao.com";
    public const string DemoPassword = "demo123";
    public const string DemoTenantSlug = "demo-hvac";
    private static readonly Random Rng = new(42);

    public static async Task<User> EnsureDemoTenantAndUserAsync(AppDbContext db)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == DemoTenantSlug);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                Slug = DemoTenantSlug,
                Name = "Freedom Air Heating & Cooling",
                Theme = "black"
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var user = await db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == DemoEmail);
        if (user == null)
        {
            user = new User
            {
                TenantId = tenant.Id,
                Email = DemoEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
                FirstName = "Demo",
                LastName = "User",
                Role = "owner",
                Title = "Operations Manager",
                Theme = "black"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            user.Tenant = tenant;
        }

        return user;
    }

    public static async Task ResetDemoDataAsync(AppDbContext db, int tenantId)
    {
        // Clean all existing demo data
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""AppointmentTechnicians"" WHERE ""AppointmentId"" IN (SELECT ""Id"" FROM ""Appointments"" WHERE ""TenantId"" = {0});
            DELETE FROM ""Appointments"" WHERE ""TenantId"" = {0};
            DELETE FROM ""PurchaseOrderItems"" WHERE ""PurchaseOrderId"" IN (SELECT ""Id"" FROM ""PurchaseOrders"" WHERE ""TenantId"" = {0});
            DELETE FROM ""PurchaseOrders"" WHERE ""TenantId"" = {0};
            DELETE FROM ""ApBills"" WHERE ""TenantId"" = {0};
            DELETE FROM ""Invoices"" WHERE ""TenantId"" = {0};
            DELETE FROM ""Jobs"" WHERE ""TenantId"" = {0};
            DELETE FROM ""HoldReasons"" WHERE ""TenantId"" = {0};
            DELETE FROM ""PmCustomers"" WHERE ""TenantId"" = {0};
            DELETE FROM ""CustomerLocations"" WHERE ""TenantId"" = {0};
            DELETE FROM ""Customers"" WHERE ""TenantId"" = {0};
            DELETE FROM ""Vendors"" WHERE ""TenantId"" = {0};
        ", tenantId);
        db.ChangeTracker.Clear();

        var now = DateTime.UtcNow;
        var today = now.Date;

        // ── Customers ─────────────────────────────────────────
        var customerData = new (string Name, string Address)[]
        {
            ("HCA Houston Healthcare", "1 Medical Center Blvd, Houston TX 77030"),
            ("JLL Commercial Properties", "31706 Sanders St, Magnolia TX 77355"),
            ("Wolf Creek Real Estate", "4200 Wolf Creek Pass, Conroe TX 77304"),
            ("Silver Dollar Spirits", "118 W Main St, Tomball TX 77375"),
            ("Sheraton Brookhollow", "3000 N Loop W, Houston TX 77092"),
            ("CBRE Group", "4970 S Memorial Dr, Greenville TX 75402"),
            ("First Baptist Church", "4300 Bellaire Blvd, Houston TX 77401"),
            ("Valley View Elementary", "2901 Valley View Ln, Farmers Branch TX 75234"),
            ("Riverside Restaurant", "850 Commerce St, Dallas TX 75202"),
            ("Oakwood Senior Living", "1200 Oakwood Dr, Denton TX 76201"),
            ("CrossFit Iron Will", "500 Industrial Blvd, Lewisville TX 75057"),
            ("Garcia Auto Body", "3300 S Buckner Blvd, Dallas TX 75227"),
            ("Martinez Dental", "710 W University Dr, Denton TX 76201"),
            ("Thompson Estate", "8200 Preston Rd, Dallas TX 75225"),
            ("Premier Property Mgmt", "2100 McKinney Ave, Dallas TX 75201"),
        };

        var customers = new List<Customer>();
        for (int i = 0; i < customerData.Length; i++)
        {
            var c = new Customer
            {
                TenantId = tenantId,
                StCustomerId = 90000000 + i,
                LocationName = customerData[i].Name,
                UpdatedAt = now
            };
            db.Customers.Add(c);
            customers.Add(c);

            db.CustomerLocations.Add(new CustomerLocation
            {
                TenantId = tenantId,
                StLocationId = 90000000 + i,
                
                StCustomerId = c.StCustomerId,
                LocationName = customerData[i].Name,
                Street = customerData[i].Address.Split(',')[0],
                City = customerData[i].Address.Contains(',') ? customerData[i].Address.Split(',')[1].Trim().Split(' ')[0] : "Houston",
                State = "TX"
            });
        }
        await db.SaveChangesAsync();

        // ── Vendors ───────────────────────────────────────────
        var vendorNames = new[] { "United Refrigeration", "Johnstone Supply", "Ferguson HVAC", "Carrier Enterprise", "SupplyHouse.com", "Lennox Industries", "Goodman Distribution", "Century AC Supply", "R.E. Michel" };
        var vendors = new List<Vendor>();
        foreach (var vn in vendorNames)
        {
            var v = new Vendor { TenantId = tenantId, Name = vn, StVendorId = 90000000 + vendors.Count };
            db.Vendors.Add(v);
            vendors.Add(v);
        }
        await db.SaveChangesAsync();

        // ── Jobs ──────────────────────────────────────────────
        var jobTypes = new[] { "AC Service", "Heating Repair", "AC Install", "Maintenance", "Refrigeration", "Duct Work", "Thermostat Install" };
        var statuses = new[] { "Completed", "Completed", "Completed", "Completed", "InProgress", "Scheduled", "Hold", "Completed", "Completed", "Completed" };
        var holdReasons = new[] { "Needs Quote", "Need to return", "Order Parts", "Waiting for materials", "Waiting for Customer Approval" };

        var jobs = new List<Job>();
        int jobNum = 2001;
        for (int i = 0; i < 80; i++)
        {
            var status = statuses[i % statuses.Length];
            var custIdx = i % customers.Count;
            var daysAgo = Rng.Next(1, 120);
            var amount = status == "Completed" ? (decimal)(Rng.Next(200, 15000) + Rng.NextDouble() * 100) : 0;

            var j = new Job
            {
                TenantId = tenantId,
                StJobId = 90000000 + i,
                StCustomerId = customers[custIdx].StCustomerId,
                CustomerName = customers[custIdx].Name,
                JobNumber = (jobNum++).ToString(),
                Status = status,
                JobTypeName = jobTypes[i % jobTypes.Length],
                TotalAmount = Math.Round(amount, 2),
                CreatedOn = now.AddDays(-daysAgo),
                HoldReasonName = status == "Hold" ? holdReasons[i % holdReasons.Length] : null
            };
            db.Jobs.Add(j);
            jobs.Add(j);
        }
        await db.SaveChangesAsync();

        // ── Hold Reasons ──────────────────────────────────────
        foreach (var hr in holdReasons)
        {
            db.HoldReasons.Add(new HoldReason
            {
                TenantId = tenantId,
                StHoldReasonId = 90000000 + Array.IndexOf(holdReasons, hr),
                Name = hr,
                Active = true
            });
        }
        await db.SaveChangesAsync();

        // ── Invoices (AR) ─────────────────────────────────────
        var completedJobs = jobs.Where(j => j.Status == "Completed" && j.TotalAmount > 0).ToList();
        foreach (var cj in completedJobs)
        {
            var daysOld = Rng.Next(1, 100);
            var paidPct = daysOld > 60 ? 0.3 : daysOld > 30 ? 0.6 : 0.85;
            var balance = Rng.NextDouble() < paidPct ? 0 : cj.TotalAmount;

            db.Invoices.Add(new Invoice
            {
                TenantId = tenantId,
                StInvoiceId = 90000000 + completedJobs.IndexOf(cj),
                StCustomerId = cj.StCustomerId,
                CustomerName = cj.CustomerName,
                TotalAmount = cj.TotalAmount,
                BalanceRemaining = Math.Round(balance, 2),
                InvoiceDate = now.AddDays(-daysOld)
            });
        }
        await db.SaveChangesAsync();

        // ── AP Bills ──────────────────────────────────────────
        for (int i = 0; i < 12; i++)
        {
            var vendor = vendors[i % vendors.Count];
            var daysUntilDue = Rng.Next(-15, 45);
            db.ApBills.Add(new ApBill
            {
                TenantId = tenantId,
                StApBillId = 90000000 + i,
                VendorId = vendor.Id,
                InvoiceNumber = $"VND-{1000 + i}",
                Amount = Math.Round((decimal)(Rng.Next(200, 8000) + Rng.NextDouble() * 100), 2),
                DueDate = today.AddDays(daysUntilDue),
                IsPaid = false,
                ReferenceNumber = $"REF-{Rng.Next(10000, 99999)}",
                Source = i % 3 == 0 ? "Purchasing" : "Standalone",
                Status = daysUntilDue < 0 ? "Overdue" : "Unreconciled"
            });
        }
        await db.SaveChangesAsync();

        // ── Purchase Orders ───────────────────────────────────
        var poStatuses = new[] { "Sent", "Sent", "Pending", "Received", "Sent", "Pending" };
        var skuNames = new[] { "Compressor Scroll 3-Ton", "Capacitor 45/5 MFD", "Contactor 2-Pole 40A", "TXV Valve R-410A", "Blower Motor 1/2 HP", "Refrigerant R-410A 25lb", "Filter Drier 3/8", "Thermostat Honeywell T6" };
        for (int i = 0; i < 15; i++)
        {
            var vendor = vendors[i % vendors.Count];
            var linkedJob = i < 8 ? jobs[i * 3] : null;
            var po = new PurchaseOrder
            {
                TenantId = tenantId,
                StPurchaseOrderId = 90000000 + i,
                Number = $"{linkedJob?.JobNumber ?? "SHOP"}-{(i + 1):D3}",
                Status = poStatuses[i % poStatuses.Length],
                VendorName = vendor.Name,
                StVendorId = vendor.StVendorId,
                StJobId = linkedJob?.StJobId,
                JobNumber = linkedJob?.JobNumber,
                Total = 0,
                Date = now.AddDays(-Rng.Next(1, 30)),
                RequiredOn = now.AddDays(Rng.Next(-5, 14)),
                SentOn = now.AddDays(-Rng.Next(1, 10))
            };

            var itemCount = Rng.Next(1, 4);
            decimal poTotal = 0;
            for (int k = 0; k < itemCount; k++)
            {
                var qty = Rng.Next(1, 5);
                var cost = Math.Round((decimal)(Rng.Next(20, 500) + Rng.NextDouble() * 50), 2);
                var total = qty * cost;
                poTotal += total;
                po.Items.Add(new PurchaseOrderItem
                {
                    StItemId = 90000000 + i * 10 + k,
                    SkuName = skuNames[(i + k) % skuNames.Length],
                    SkuCode = $"SKU-{1000 + (i + k) % skuNames.Length}",
                    Quantity = qty,
                    QuantityReceived = po.Status == "Received" ? qty : 0,
                    Cost = cost,
                    Total = total,
                    Status = po.Status == "Received" ? "Received" : "Pending"
                });
            }
            po.Total = poTotal;
            db.PurchaseOrders.Add(po);
        }
        await db.SaveChangesAsync();

        // ── PM Customers ──────────────────────────────────────
        var pmStatuses = new[] { "Current", "Current", "Overdue", "Current", "Overdue" };
        for (int i = 0; i < 10; i++)
        {
            db.PmCustomers.Add(new PmCustomer
            {
                TenantId = tenantId,
                StCustomerId = customers[i].StCustomerId,
                CustomerName = customers[i].Name,
                PmStatus = pmStatuses[i % pmStatuses.Length],
                LastPmDate = pmStatuses[i % pmStatuses.Length] == "Overdue" ? now.AddDays(-Rng.Next(120, 300)) : now.AddDays(-Rng.Next(10, 90)),
            });
        }
        await db.SaveChangesAsync();

        // ── Appointments (schedule) ───────────────────────────
        var techNames = new[] { "Mike Rodriguez", "Tony Estrada", "David Patchin", "Juan Fernandez", "Brandon" };
        TimeZoneInfo centralZone;
        try { centralZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        catch { centralZone = TimeZoneInfo.Utc; }
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(now, centralZone).Date;

        for (int day = 0; day < 3; day++)
        {
            var apptCount = day == 0 ? 9 : day == 1 ? 4 : 2;
            for (int a = 0; a < apptCount; a++)
            {
                var techIdx = a % techNames.Length;
                var jobIdx = (day * 10 + a) % jobs.Count;
                var hour = 7 + a + (a > 3 ? 1 : 0);
                var startLocal = todayLocal.AddDays(day).AddHours(hour);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, centralZone);

                var appt = new Appointment
                {
                    TenantId = tenantId,
                    StAppointmentId = 90000000 + day * 100 + a,
                    StJobId = jobs[jobIdx].StJobId,
                    JobNumber = jobs[jobIdx].JobNumber,
                    CustomerName = jobs[jobIdx].CustomerName,
                    LocationName = customerData[jobIdx % customerData.Length].Address.Split(',')[0],
                    Status = day == 0 && a < 2 ? "Working" : "Scheduled",
                    Start = startUtc
                };
                db.Appointments.Add(appt);
                await db.SaveChangesAsync();

                db.AppointmentTechnicians.Add(new AppointmentTechnician
                {
                    AppointmentId = appt.Id,
                    TechnicianName = techNames[techIdx]
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
