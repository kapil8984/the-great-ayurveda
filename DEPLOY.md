# Production Deployment Guide: MonsterASP (Windows / IIS Hosting)

## 1. Overview
This guide provides the complete, exact production deployment procedure for **The Great Ayurveda** unified ASP.NET Core web application to MonsterASP (or RunASP.NET) Windows/IIS hosting.

---

## 2. Prerequisites
- A registered MonsterASP hosting account with IIS ASP.NET Core hosting module (.NET 10 or in-process / out-of-process hosting).
- An MS SQL Database provisioned from the MonsterASP control panel.
- .NET 10 SDK installed on the build machine.

---

## 3. Publish Command


From the root of the repository, execute:

```powershell
./publish-live.ps1
```
Or manually:
```powershell
dotnet publish src/Ayurveda.Web/Ayurveda.Web.csproj -c Release -r win-x64 --no-self-contained -o publish/Web
```

---

## 4. Which Folder to Upload
Upload the **contents** of the local publish directory:

- **Local Publish Folder**: `./publish/Web`
- **Destination Folder on MonsterASP**: `/site/wwwroot` (via MonsterASP File Manager, FTP/FTPS, or WebDeploy)

> **Note**: Do not upload the enclosing `Web` folder itself; upload all files and subfolders inside `./publish/Web` (`Ayurveda.Web.dll`, `web.config`, `appsettings.json`, `wwwroot/`, `catalog_300plus.json`, etc.) directly into `/site/wwwroot`.

---

## 5. MonsterASP Environment Variables
Configure the following environment variables in the MonsterASP Control Panel (under Site Configuration / Environment Variables) or within `web.config` under the `<aspNetCore><environmentVariables>` section:

| Environment Variable | Recommended Value / Format | Purpose |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runs the application in ASP.NET Core Production mode |
| `ConnectionStrings__DefaultConnection` | `Server=mssqlXX.monsterasp.net;Database=<db_name>;User Id=<db_user>;Password=<db_password>;TrustServerCertificate=True;MultipleActiveResultSets=true;` | Connection string to the MonsterASP MS SQL database |
| `Site__BaseUrl` | `https://<your-subdomain>.monsterasp.net` | Canonical base URL used for sitemap, robots, and metadata tags |
| `Demo__OpenAccess` | `true` *(for public live demo)* / `false` *(for standard live)* | When `true`, enables demo accounts, sandbox login, demo doctor portal, demo chips on admin login, and simulated payments |
| `Demo__AnyCredentialLogin` | `false` | When false (default), implicit "any email/password creates account" is disabled. Only verified users or explicit "Continue as Demo Guest" can enter |
| `Demo__WarehouseLat` | `22.7196` | Indore centralized warehouse dispatch origin latitude for Leaflet routing |
| `Demo__WarehouseLng` | `75.8577` | Indore centralized warehouse dispatch origin longitude for Leaflet routing |
| `SeedData__Enabled` | `true` *(first run only)* / `false` *(after initial seeding)* | Controls database auto-seeding on application startup |
| `Admin__InitialPassword` | `<your-strong-initial-admin-credential>` | Initial password for `admin@thegreatayurveda.com` on first seed |
| `RateLimit__TrustForwardedFor` | `true` *(if behind proxy/load balancer)* / `false` | When true, uses first client IP from `X-Forwarded-For` header for IP rate limiting |
| `Auth__RequireEmailVerification` | `true` *(production)* / `false` | When true, new accounts stay unconfirmed until OTP is entered; unconfirmed accounts cannot log in |
| `Auth__OtpMode` | `Demo` *(testing/portfolio)* / `Email` *(production)* | When `Demo`, accepts ANY 6 digits for instant verification without outbound email; when `Email`, generates cryptographic 6-digit OTP and dispatches via configured email provider |
| `Demo__AutoApproveDoctors` | `false` *(production)* / `true` *(demo/testing)* | When `true`, newly registered doctor accounts are automatically verified and approved upon registration without requiring manual admin verification |
| `Ads__RotateSeconds` | `3` *(default)* | Number of seconds between rotating sponsored demo ad banners on storefront layouts (clamped 1-10s) |
| `Email__Mode` | `Smtp` *(production)* / `File` *(testing)* | Outbound email mode: `Smtp` sends over TLS; `File` writes .eml files to `App_Data/outbox` |
| `Email__Host` | `smtp.gmail.com` *(or relay host)* | Outbound SMTP server hostname |
| `Email__Port` | `587` | Outbound SMTP port (587 STARTTLS / 465 SSL) |
| `Email__User` | `<smtp_username_or_email>` | Outbound SMTP username |
| `Email__Password` | `<smtp_app_password_or_token>` | Outbound SMTP password or app secret |
| `Email__FromName` | `The Great Ayurveda Care` | Sender display name in customer email notifications |
| `Email__EnableSsl` | `true` | Enforces SSL/TLS on SMTP transport |
| `Email__DailyLimit` | `300` | Global daily throttle cap for outbound OTP verification emails |
| `Razorpay__KeyId` | `rzp_test_<id>` *(or live `rzp_live_<id>`)* | Razorpay Standard Checkout public client key |
| `Razorpay__KeySecret` | `<razorpay_secret_key>` | Razorpay merchant secret for server-side HMAC-SHA256 signature verification |

> **Credential Notice**: Never store plain-text credentials in source control. Always substitute `<placeholder>` values with your actual configuration directly in the host control panel. Never log `Email__Password` or `Razorpay__KeySecret`.

---

## 6. First-Run Steps & Database Seeding Workflow

1. **Create the MS SQL Database**:
   - In MonsterASP Control Panel, navigate to **Databases** -> **MS SQL**.
   - Create a new database.
   - Record the host server (`mssqlXX.runasp.net`), database name, username, and database credential.

2. **Configure Initial Environment Variables**:
   - In the RunASP.NET site settings, configure all environment variables listed in Section 5.
   - Ensure `SeedData__Enabled` is set to `true`.
   - Set `Admin__InitialPassword` to your chosen strong administrator password.
   - Set `Site__BaseUrl` to `http://<your-subdomain>.runasp.net` (stays `http://` until SSL/HTTPS is activated in the panel).

3. **Upload the Published Files**:
   - Run the publish command (Section 3).
   - Connect via FTP or the RunASP.NET File Manager.
   - Upload all files from `./publish/Web` into `/site/wwwroot`.

4. **Directory Creation & Permissions**:
   - Ensure the following two directories exist under `/site/wwwroot` and grant them Write and Modify permissions:
     - `wwwroot/uploads` (for media and document uploads)
     - `App_Data` (for ASP.NET DataProtection keyring files)

5. **Trigger Initial Migration and Seeding**:
   - Open a browser and visit your site: `http://<your-subdomain>.runasp.net/`.
   - The application automatically applies pending Entity Framework Core migrations and populates initial categories, products, doctors, hero banners, and active coupons (`HEALTH15`).
   - The administrator account `admin@thegreatayurveda.com` is created with your configured `Admin__InitialPassword`.

6. **Verify Admin Portal Access**:
   - Navigate to `http://<your-subdomain>.runasp.net/admin-portal/login`.
   - Log in with `admin@thegreatayurveda.com` and your initial password.
   - Confirm access to the Admin Dashboard, Catalog, Doctor Verification, and Order management.

7. **Lock Seed Data (Post-Seed Step)**:
   - Return to the RunASP.NET Control Panel -> Site Configuration -> Environment Variables.
   - Change `SeedData__Enabled` to `false`.
   - Restart the website/application pool.
   - **Verification**: With `SeedData__Enabled=false`, subsequent site restarts or app pool recycles will preserve existing database state without duplicate records or password resets.

---

## 7. File Permissions & Storage Paths
- Media uploaded through the Admin Portal (product photos, banner images, doctor avatars) is stored under `wwwroot/uploads/`.
- Key material for ASP.NET Data Protection is stored under `App_Data/`.
- Ensure the IIS Application Pool user (`IIS_IUSRS` / site pool user) has Write and Modify permissions on both `/site/wwwroot/wwwroot/uploads/` and `/site/wwwroot/App_Data/`.

---

## 8. Fallback Configuration: `appsettings.Production.json`
If your hosting plan or control panel does not provide an Environment Variables interface:
- Create an `appsettings.Production.json` file directly on the server in `/site/wwwroot` containing your connection string and settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=mssqlXX.runasp.net;Database=<your_db_name>;User Id=<your_db_user>;Pwd=<your_db_password>;TrustServerCertificate=True;MultipleActiveResultSets=true;"
  },
  "Site": {
    "BaseUrl": "http://<your-subdomain>.runasp.net"
  },
  "SeedData": {
    "Enabled": false
  },
  "Admin": {
    "InitialPassword": "<your-strong-initial-admin-credential>"
  }
}
```

> **CRITICAL**: Maintain this file only on the remote server host filesystem. NEVER commit `appsettings.Production.json` or live database credentials to the Git repository.
