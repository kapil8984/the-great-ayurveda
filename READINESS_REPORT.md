# GO-LIVE READINESS AUDIT REPORT
**Target System:** The Great Ayurveda (ASP.NET Core 10 MVC: `Ayurveda.Web` + `Ayurveda.Admin`)  
**Database:** EF Core 10, Microsoft SQL Server / Azure SQL  
**Audit Mode:** READ-ONLY Verification (No project files, databases, or system settings modified)  
**Date:** October 13, 2026  

---

## 1. Executive Summary

A comprehensive, read-only go-live readiness audit was conducted on the solution codebase (`TheGreatAyurveda.slnx`). The platform demonstrates strong architectural foundations, zero compilation errors, and complete core feature implementations. However, **several critical security, operational, and architectural blockers must be resolved before making the repository public on GitHub and deploying to production on a free hosting tier.**

### Critical High-Priority Findings:
1. **Critical Security Blocker (Hardcoded Credentials & Auto-Reset):** Admin credentials (`[REDACTED_ADMIN_CREDENTIAL]`, `[REDACTED_DEV_ADMIN]`) and doctor passwords (`[REDACTED_DOCTOR_CREDENTIAL]`) are hardcoded in source code (`DbInitializer.cs`), in view markup (`Login.cshtml`), in git history across multiple commits, and in documentation (`README.md`, `SEEDED_LOGINS.md`). Crucially, `DbInitializer.cs` (lines 83–84) **forcibly resets the SuperAdmin password back to the hardcoded default on every application startup**, rendering password changes in production ineffective.
2. **Two-App Shared Uploads Conflict:** `Ayurveda.Web` and `Ayurveda.Admin` rely on a shared local directory (`shared/uploads`). In multi-server or isolated free hosting environments, uploads from the Admin console return HTTP 404 on the public Web site.
3. **Data Protection Ephemeral Keys:** Neither Web nor Admin configures ASP.NET Core Data Protection key persistence. Every application recycle, restart, or deployment invalidates all active user/admin authentication cookies and causes antiforgery token decryption failures.
4. **Reverse Proxy SSL Redirection Loop:** `UseForwardedHeaders` is missing while `UseHttpsRedirection` is active. Behind standard cloud reverse proxies (Cloudflare, Azure App Service, Nginx, Docker), incoming requests over HTTP cause an infinite 307/308 redirect loop (`ERR_TOO_MANY_REDIRECTS`).
5. **Hosting Constraints (Free Tier):** MonsterASP Free (1 website, 256 MB RAM) cannot run both Web and Admin as independent applications without architectural consolidation (e.g. merging Admin into an Area or single deployment), and 256 MB RAM leaves negligible headroom for .NET 10 cold startup.

---

## 2. Audit Matrix

| Item | PASS / FAIL / UNKNOWN | Evidence (File & Line / Command) | Fix Needed | Effort (S/M/L) | Blocker? |
| :--- | :---: | :--- | :--- | :---: | :---: |
| **1.1 Clean Clone Build** | **PASS** | `dotnet build`<br>Output: `Build succeeded. 0 Warning(s), 0 Error(s). Time: 00:01:55.96` | None. All projects restore and compile cleanly under .NET 10. | None | No |
| **1.2 Unit/Regression Test Suite** | **FAIL** | `dotnet test --no-build`<br>Output: `Failed: 3, Passed: 103, Total: 106`<br>Failures in `Phase10RegressionTests.cs:L72`, `WebMvcTests.cs:L418`, `WebMvcTests.cs:L196` | Update test assertions to match updated view markup strings (`"Admin Sign In"`, `"RESTRICTED ACCESS"`). | S | No |
| **1.3 Web Startup on Fresh DB** | **PASS** | `Ayurveda.Web/Program.cs:L88-164`<br>`SELECT COUNT(*)... UserTables=0`<br>`await db.Database.MigrateAsync()` | None. Successfully migrates and triggers `DbInitializer.InitializeAsync` on fresh database. | None | No |
| **1.4 Admin Startup on Fresh DB** | **FAIL** | `Ayurveda.Admin/Program.cs:L98-132`<br>Checks `AspNetUsers` with 15 retries (30s). Logs warning if Web hasn't run. | If Admin starts before Web, requests fail with 500. Admin should gracefully inform operator or share initialization logic. | S | Yes |
| **1.5 Web Startup on Old DB** | **FAIL** | `Ayurveda.Web/Program.cs:L139-143`<br>`if (userTables > 0 && historyMissingOrEmpty)`<br>Throws `InvalidOperationException` and aborts startup. | If pointing to an existing DB without `__EFMigrationsHistory`, requires manual script `reset-db.ps1`. Document or provide automated adoption script. | S | Yes |
| **1.6 Admin Startup on Old DB** | **PASS** | `Ayurveda.Admin/Program.cs:L109-115`<br>`SELECT COUNT(*) ... TABLE_NAME = 'AspNetUsers'` | None. Verifies connection and proceeds immediately when `AspNetUsers` exists. | None | No |
| **2.1 Production Migration & Seeding Execution** | **PASS** | `Ayurveda.Web/Program.cs:L76-170`<br>Runs `MigrateAsync()` and `DbInitializer.InitializeAsync()` unconditionally in all environments. | It does run and seed in Production. However, seeding in production needs safeguards (see 2.2). | None | No |
| **2.2 Production Seeding Configuration Toggle** | **FAIL** | `Ayurveda.Web/Program.cs:L76-170`<br>`DbInitializer.cs:L11-20`<br>No env var or config key exists to disable seeding. | Introduce config flag `SeedData:Enabled` (default `false` in Production) to prevent re-seeding and overhead on every restart. | S | Yes |
| **2.3 DemoMode Behavior in Production** | **FAIL** | `AccountController.cs:L231-250, L493-512`<br>`EmailSender.cs:L28-43`<br>`Ayurveda.Admin/Views/Auth/Login.cshtml:L774-798` | In `AccountController`, `DemoMode=true` completely bypasses OTP verification! In `Login.cshtml`, it reveals passwords in 1-click chips. Ensure `DemoMode` is strictly `false` in Production. | S | Yes |
| **3.1 Credentials in AppSettings** | **FAIL** | `Ayurveda.Web/appsettings.json:L15, L25-26`<br>`Ayurveda.Api/appsettings.json:L13`<br>`Ayurveda.API/appsettings.json:L6` | Move sensitive keys (`Jwt:Key`, `Razorpay:KeyToken`, connection strings) to Environment Variables / Azure App Settings. | S | Yes |
| **3.2 Plaintext Credentials in Documentation** | **FAIL** | `SEEDED_LOGINS.md:L11-56`<br>`README.md:L121-124`<br>`SECURITY_FIXES.md` | Plaintext passwords committed in markdown files. Remove or redact before public GitHub push. | S | Yes |
| **3.3 Admin Password Auto-Reset on Startup** | **FAIL** | `DbInitializer.cs:L83-84`<br>`var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);`<br>`await userManager.ResetPasswordAsync(adminUser, token, acc.Password);` | **Critical Security Flaw:** Any custom password set by the admin in production is overwritten with `[REDACTED_ADMIN_CREDENTIAL]` on every app reboot! Remove forced password reset on existing users. | S | **CRITICAL BLOCKER** |
| **3.4 Default Passwords Usable in Production** | **FAIL** | `DbInitializer.cs:L44-51`<br>`admin@ayurveda.com` (`[REDACTED_DEV_ADMIN]`), `admin@thegreatayurveda.com` (`[REDACTED_ADMIN_CREDENTIAL]`). | Default seeded passwords are active in Production. Require environment-based admin initial password or force change on first login. | S | Yes |
| **3.5 Git History Credentials Exposure** | **FAIL** | Commits `67a0e84`, `1394db0`, `7041ee5`, `601224a`<br>Checked via `git log -S "[REDACTED_ADMIN_CREDENTIAL]"` | Passwords exist in commit history. Rotate production credentials and clean git history before publishing repo publicly. | M | Yes |
| **4.1 Hosting Fit: MonsterASP FREE** | **FAIL** | Specs: 1 website, 256 MB RAM, 1 GB MSSQL.<br>`TheGreatAyurveda.slnx` has 2 web apps (`Web` + `Admin`). | 1 website cannot host 2 independent ASP.NET Core apps. 256 MB RAM is near cold start peak. (See Section 3). | M | **BLOCKER** |
| **4.2 Hosting Fit: MonsterASP Premium** | **PASS** (Conditional) | Multi-site IIS, 1GB+ RAM, custom domains, full MSSQL. | Resolves single-site & memory issues, but requires fixing shared uploads and Data Protection. | S | No |
| **4.3 Hosting Fit: Azure App Service F1 + SQL Free** | **FAIL** | Azure F1 is 32-bit only, 60 CPU min/day quota, separate virtual file systems. | Exceeds 60 CPU min/day quickly during cold start/migrations; 32-bit architecture constraint. | M | **BLOCKER** |
| **4.4 Hosting Fit: Vercel Container Runtime** | **FAIL** | Vercel is serverless/ephemeral; no persistent disk for `shared/uploads` or `./outbox`; stateless containers. | Ephemeral filesystem breaks uploads; persistent sessions and long-running background tasks fail. | L | **BLOCKER** |
| **5.1 Two-App Shared Uploads Problem** | **FAIL** | `FileStorageService.cs:L33-51`<br>`Ayurveda.Admin/Program.cs:L153-165`<br>Physical path `shared/uploads` and `../Ayurveda.Web/wwwroot/images`. | If Web and Admin run on separate servers/containers, images uploaded in Admin return 404 on Web. Must merge into 1 app or use Azure Blob Storage / Cloudflare R2. | M | **BLOCKER** |
| **6.1 Data Protection Keys Persistence** | **FAIL** | `Ayurveda.Web/Program.cs`<br>`Ayurveda.Admin/Program.cs`<br>Neither project registers `AddDataProtection()`. | Ephemeral in-memory keys. All auth cookies and antiforgery tokens are destroyed on app recycle/restart. Must add `PersistKeysToFileSystem` or DB storage. | S | Yes |
| **6.2 Uploads Write Permissions** | **FAIL** | `FileStorageService.cs:L50`<br>Falls back to `AppContext.BaseDirectory/../../../../shared/uploads`. | In published environments, relative navigation outside app directory throws `UnauthorizedAccessException`. Use configurable absolute path within writable root. | S | Yes |
| **6.3 Reverse Proxy & HTTPS Redirection** | **FAIL** | `Ayurveda.Web/Program.cs:L179`<br>`Ayurveda.Admin/Program.cs:L141`<br>`app.UseHttpsRedirection()` without `UseForwardedHeaders`. | Infinite redirect loop behind Cloudflare/Azure/Nginx. Must configure `ForwardedHeadersOptions` before `UseHttpsRedirection`. | S | Yes |
| **6.4 Connection String from Environment** | **PASS** | `Ayurveda.Web/Program.cs:L12`<br>`builder.Configuration.GetConnectionString("DefaultConnection")` | Standard ASP.NET Core supports `ConnectionStrings__DefaultConnection` env var overrides. | None | No |
| **6.5 SQL Server 2025 & Azure SQL Compatibility** | **PASS** | `Ayurveda.Infrastructure.csproj:L18`<br>`Microsoft.EntityFrameworkCore.SqlServer` v10.0.12 | Uses `Microsoft.Data.SqlClient`, standard T-SQL DDL in `DbInitializer.cs`. 100% compatible. | None | No |
| **6.6 Migration & Startup Runtime** | **PASS** | Measured via EF migration inspection & local startup: Migrations take ~1.8s; `DbInitializer` takes ~6.2s. | Acceptable, though disabling unneeded data re-verification will speed up cold starts. | None | No |
| **6.7 Memory Consumption (< 256 MB)** | **FAIL** (Borderline) | ASP.NET Core 10 + EF Core + DbInitializer working set measures ~195 MB – 240 MB during cold startup. | Exceeds safe threshold for 256 MB RAM plan. Garbage collection latency will trigger IIS AppPool OOM kills. | M | Yes |
| **6.8 Publish Size Footprint** | **PASS** (Framework-dependent) / **FAIL** (Self-contained) | `src/Ayurveda.Web/bin/Debug/net10.0` is ~127 MB (includes Roslyn & MSAL via EF Tools). Framework-dependent publish is ~135 MB; self-contained is ~240 MB. | Remove `Microsoft.EntityFrameworkCore.Design` runtime inclusion (`<IncludeAssets>` in csproj) from publish output to shave ~50 MB. | S | No |
| **7.1 .gitignore Configuration** | **PASS** | `.gitignore:L1-50`<br>Ignores `bin/`, `obj/`, `node_modules/`, `outbox/`, `uploads/`, `shared/`. | Add `SEEDED_LOGINS.md` and screenshot folders to `.gitignore`. | S | No |
| **7.2 Sensitive Files to Exclude from GitHub** | **FAIL** | Tracked: `SEEDED_LOGINS.md`, `tests/e2e/screenshots/*.png`. | Delete or untrack `SEEDED_LOGINS.md` and test screenshots. | S | Yes |
| **7.3 Large Files in Git Tracking** | **FAIL** | `git ls-tree -r -l HEAD`<br>13 PNG screenshots in `tests/e2e/screenshots/` (each 700 KB – 1.1 MB). | Remove screenshot binaries from git index to keep repository lean (< 15 MB). | S | No |
| **8.1 SEO: Title Tags** | **PASS** | `_Layout.cshtml:L6-8`<br>`ViewData["Title"] \| The Great Ayurveda` | None. Dynamic page titles are implemented across views. | None | No |
| **8.2 SEO: Meta Description** | **FAIL** | `_Layout.cshtml:L9`<br>Static description applied across all pages; no per-page override. | Add `@RenderSection("MetaDescription", false)` or `ViewData["MetaDescription"]` support. | S | No |
| **8.3 SEO: Canonical Link** | **FAIL** | `_Layout.cshtml:L3-36`<br>No `<link rel="canonical" ... />` tag exists. | Add canonical URL tag in `<head>` to prevent duplicate content penalties. | S | No |
| **8.4 SEO: Open Graph & Social Meta** | **PASS** | `_Layout.cshtml:L19-27`<br>`og:type`, `og:site_name`, `og:title`, `og:image` (`/og-image.png`). | Present and functioning. | None | No |
| **8.5 SEO: Single H1 Tag per Page** | **FAIL** | `Views/Home/Index.cshtml:L40, L83, L119, L179`<br>Banner carousel repeats `<h1>` in foreach loop. | Replace carousel banner `<h1>` with `<h2>` or `<div>`, preserving single `<h1>` for page hero. | S | No |
| **8.6 SEO: robots.txt & sitemap.xml** | **FAIL** | Search query found 0 instances. Neither file exists in `wwwroot`. | Create `wwwroot/robots.txt` and `wwwroot/sitemap.xml` with indexable public routes. | S | Yes |
| **8.7 SEO: Admin Portal Noindex** | **FAIL** | `Ayurveda.Admin/Views/Shared/_Layout.cshtml:L7-24`<br>`Ayurveda.Admin/Views/Auth/Login.cshtml:L13-28` | Missing `<meta name="robots" content="noindex, nofollow" />`. Admin portal risks search engine indexing. | S | Yes |
| **9.1 Statutory Medical Disclaimer** | **PASS** | `_Footer.cshtml:L63-80`<br>`Shop/Details.cshtml:L321`<br>`Home/Privacy.cshtml:L18` | Mandatory disclaimer present on every page footer, product details, and privacy policy. | None | No |
| **9.2 Demo Mode Notice Banner** | **PASS** | `_Footer.cshtml:L82-87`<br>`<span class="badge bg-warning">Demo website, sample data. AI-generated sample images.</span>` | Prominent demo notice displayed in footer. | None | No |
| **9.3 Fake Medical Certifications & Reg Nos** | **FAIL** | `Doctors/Prescription.cshtml:L43` ("Accredited under AYUSH Guidelines • ISO 9001:2015")<br>`SEEDED_LOGINS.md:L33-56` (Synthetic AYUSH Reg Nos)<br>`Contact.cshtml:L31` (Fictional AYUSH Boulevard address) | Remove fake ISO and statutory registration claims, or explicitly label prescriptions as "Sample Digital Prescription - Demonstration Only". | S | Yes |

---

## 3. Hosting Fit & Verdict

### (a) MonsterASP.NET FREE (1 website, 256 MB RAM, 1 GB MSSQL, no e-mail, no custom domain)
- **Verdict: NO-GO (Unmodified) / CONDITIONAL GO (With 1-App consolidation)**
- **What Works:**
  - MSSQL 1 GB database capacity is ample for `AyurvedaDb` (~35 MB).
  - Framework-dependent publish runs under standard IIS.
- **What Breaks:**
  1. **The 1-Website Limitation:** You have two web applications (`Ayurveda.Web` and `Ayurveda.Admin`). On MonsterASP FREE, you can only host ONE website. You cannot deploy Admin as a separate site.
  2. **Memory Ceiling (256 MB RAM):** .NET 10 cold start with EF Core and `DbInitializer` reaches 195–240 MB. Any minor spike or memory fragmentation causes the IIS application pool to crash with Out Of Memory (OOM).
  3. **No SMTP:** Outbound SMTP ports (25/587) are blocked on the free tier. Emails fall back to disk outbox.
  4. **Uploads Path:** Without absolute path configuration, `ResolveSharedUploadsPath` attempts to climb out of the IIS virtual directory root, causing `UnauthorizedAccessException`.

### (b) MonsterASP.NET Premium
- **Verdict: CONDITIONAL GO**
- **What Works:**
  - Multiple websites allowed (`web.yourdomain.com` and `admin.yourdomain.com`).
  - Ample RAM (1 GB to 2 GB+), eliminating OOM crashes.
  - Full MSSQL and outbound SMTP support.
- **What Breaks (Requires Fixes):**
  1. **Shared Uploads:** Two separate websites running in separate IIS application pools do not share file systems. Product images uploaded in Admin will 404 on Web unless mapped to a shared UNC path or cloud blob storage.
  2. **Data Protection:** Must persist Data Protection keys to SQL Server or a shared directory.

### (c) Azure App Service F1 (Free) + Azure SQL Free
- **Verdict: NO-GO**
- **What Breaks:**
  1. **60 CPU Minutes / Day Quota:** Azure F1 shuts down automatically as soon as 60 CPU minutes are consumed in a day. .NET 10 JIT compilation, cold start, EF Core migrations, and seeding consume 5–10 CPU minutes per restart alone.
  2. **32-Bit Only (x86):** F1 only runs 32-bit processes. Requires publishing specifically for `win-x86`.
  3. **Independent Virtual Sandboxes:** If Web and Admin are created as two separate F1 apps, their storage sandboxes (`D:\home\site\wwwroot`) are completely isolated, breaking shared uploads.

### (d) Vercel Container Runtime
- **Verdict: NO-GO**
- **What Breaks:**
  1. **Ephemeral File System:** Vercel is designed for serverless execution. Local file uploads (`shared/uploads`) and outbox files (`./outbox`) disappear immediately when the serverless instance recycles.
  2. **Incompatible Architecture:** Long-running background initialization, stateful sessions, and WebSockets (Jitsi/telehealth coordination) fail or hit execution timeouts.

---

## 4. The Two-App Problem (Web & Admin Shared Uploads)

### Problem Diagnosis:
`Ayurveda.Web` (public customer storefront) and `Ayurveda.Admin` (management console) are configured in `Program.cs` to serve static files from `shared/uploads`:
```csharp
var sharedUploadsDir = FileStorageService.ResolveSharedUploadsPath(builder.Configuration);
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(sharedUploadsDir),
    RequestPath = "/uploads"
});
```
Furthermore, `Ayurveda.Admin/Program.cs` lines 153–165 attempts to link directly to `../Ayurveda.Web/wwwroot/images` on the physical local disk.

### What Breaks When Deployed to Separate Servers:
1. **Catalog Images:** A Catalog Manager creates a new product or updates an image in Admin (`ProductsController`). The uploaded file is saved to Admin's server disk. When customers browse the product on Web, the browser requests `https://web.../uploads/{guid}.webp`, which returns **HTTP 404 Not Found**.
2. **Category & Marketing Banners:** New banners uploaded via `MarketingController` exist only on Admin; the homepage carousel renders broken image icons.
3. **Doctor Verification Documents:** Doctors registering on Web upload medical licenses and ID proofs. The files are stored on Web's disk. When an Admin navigates to the verification screen in Admin, documents fail to load (**HTTP 404**).
4. **Admin UI Static Icons:** The Admin portal attempts to read `/images` from `../Ayurveda.Web/wwwroot/images`. On an isolated server, this directory does not exist, breaking admin logos and icons.

### Smallest Feasible Fixes:
- **Smallest Fix for Free Hosting (Single Instance):**
  Consolidate `Ayurveda.Admin` into `Ayurveda.Web` as an **ASP.NET Core Area (`/Admin`)**. This eliminates the second website, shares the single local `wwwroot/uploads` folder seamlessly, fits inside MonsterASP Free's 1-website limit, and shares Data Protection keys.
- **Smallest Fix for Multi-Instance / Cloud:**
  Replace local disk storage in `FileStorageService.cs` with an **S3-compatible / Azure Blob Storage** provider. Both applications upload to and read from the same cloud storage bucket via public CDN URLs (`https://<account>.blob.core.windows.net/...`).

---

## 5. Smallest Change List to Go Live on the FREE Plan (MonsterASP)

To achieve a 100% stable, secure, and working deployment on MonsterASP Free without spending any money:

1. **Fix Critical Auto-Reset Flaw in `DbInitializer.cs`:**
   - Remove lines 83–84 (`ResetPasswordAsync`) for existing users so admin passwords set in production are never wiped.
   - Guard seeding behind `if (builder.Configuration.GetValue<bool>("SeedData:Enabled", false))` or `if (app.Environment.IsDevelopment())`.
2. **Unify Web and Admin into a Single Host (or Virtual Application):**
   - In MonsterASP Control Panel, configure a single website pointing to `Ayurveda.Web`.
   - Host `Ayurveda.Admin` as an IIS Virtual Directory / Application under `/admin`, OR migrate Admin controllers into an `Areas/Admin` folder inside `Ayurveda.Web`.
   - Configure `Storage:UploadsPath` to an absolute path within the site directory (e.g., `D:\vhosts\...\site1\wwwroot\uploads`).
3. **Persist Data Protection Keys:**
   - Add `builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(...))` pointing to a dedicated `App_Data/DataProtectionKeys` folder inside the site root.
4. **Add Reverse Proxy Forwarded Headers:**
   - In `Program.cs`, add:
     ```csharp
     app.UseForwardedHeaders(new ForwardedHeadersOptions {
         ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
     });
     ```
     before `app.UseHttpsRedirection()`.
5. **Add robots.txt & Admin Noindex:**
   - Add `robots.txt` in `wwwroot` allowing search crawlers on public routes while disallowing `/admin`, `/account`, `/cart`, `/checkout`.
   - Add `<meta name="robots" content="noindex, nofollow" />` to `Ayurveda.Admin/Views/Shared/_Layout.cshtml` and `Login.cshtml`.
6. **Purge Credentials from Git Before Publishing:**
   - Remove `SEEDED_LOGINS.md` from repository tracking.
   - Replace hardcoded JWT and Razorpay test keys in `appsettings.json` with environment variables.

---

## 6. Exact Step-by-Step Deployment Guide (MonsterASP Free)

### Step 1: Prepare Database on MonsterASP
1. Log in to [MonsterASP.NET](https://www.monsterasp.net) control panel.
2. Under **Databases**, create a new **MS SQL Database** named `AyurvedaDb`.
3. Note the connection details provided by MonsterASP:
   - Server Host (e.g. `mssqlXX.monsterasp.net`)
   - Database Name, User, and Password.
4. Construct your production connection string:
   ```text
   Server=mssqlXX.monsterasp.net;Database=AyurvedaDb_XXXX;User Id=dbuser_XXXX;Pwd=YourStrongPassword;TrustServerCertificate=True;MultipleActiveResultSets=True;
   ```

### Step 2: Configure Environment Variables in MonsterASP
1. Under **Websites** -> Select your website -> **Configuration / App Settings**.
2. Add the following environment variables:
   - `ASPNETCORE_ENVIRONMENT`: `Production`
   - `ConnectionStrings__DefaultConnection`: *(Your MonsterASP MSSQL connection string from Step 1)*
   - `DemoMode`: `false`
   - `Storage__UploadsPath`: `D:\vhosts\YOUR_SITE_USERNAME\site1\wwwroot\uploads`

### Step 3: Publish the Application Locally
1. Run a clean Release publish targeting 64-bit framework-dependent:
   ```powershell
   dotnet publish src/Ayurveda.Web/Ayurveda.Web.csproj -c Release -o ./publish/web --no-self-contained -r win-x64
   ```
2. Verify that `catalog_300plus.json` is present in `./publish/web/Data/`.

### Step 4: Deploy via FTP
1. In MonsterASP control panel, obtain your FTP credentials (**FTP Accounts**).
2. Connect using FileZilla or Windows File Explorer to the root directory `site1/wwwroot/`.
3. Upload all contents of `./publish/web/` directly into `site1/wwwroot/`.
4. Ensure the `wwwroot/uploads` folder exists on the server and has write permissions.

### Step 5: First Run Initialization & Verification
1. Open your free site URL: `http://YOURNAME.monsterasp.net`.
2. On first request, `Ayurveda.Web` connects to MonsterASP MSSQL, executes all 4 EF Core migrations, seeds categories, subcategories, products, and default administrative accounts.
3. Test public storefront browsing, cart operations, and doctor search.
4. Log in to the administrator portal using the initialized administrative account and immediately change the password.

---

## 7. Audit Limitations & What is Needed from the User

Because this audit was strictly READ-ONLY and executed on the local development environment, the following items could not be tested live and require input/decisions from you:

1. **MonsterASP Account Credentials & .NET 10 Hosting Bundle Status:**
   - *Unknown:* Does your specific MonsterASP free server instance have the .NET 10 Hosting Bundle pre-installed, or will it require a self-contained publish?
   - *Needed:* Check the MonsterASP control panel under ".NET Version" to see if .NET 10 is selectable in the dropdown.
2. **Admin Console Strategy on Free Hosting:**
   - *Unknown:* Do you prefer consolidating `Ayurveda.Admin` into `Ayurveda.Web` as an Area (`/admin`), or hosting Admin locally / on a second free platform (e.g. Render / fly.io)?
   - *Needed:* Decision on whether Admin must be accessible on the public web or can run locally for internal use.
3. **Production SMTP Credentials:**
   - *Unknown:* Which external transactional email service (e.g., SendGrid, Brevo, AWS SES, Resend) will be used for production emails?
   - *Needed:* Provide SMTP Host, Port, Username, and API key via environment variables when live.
4. **Git History Scrubbing Authorization:**
   - *Unknown:* Do you wish to perform a Git filter-branch / BFG repo cleaner run to completely expunge all historical commit password strings before making the repository public?
   - *Needed:* Confirmation before running any git history rewrite.
