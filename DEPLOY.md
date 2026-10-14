# Production Deployment Guide: RunASP.NET (Free Windows Hosting)

## 1. Overview
This guide provides the complete, exact production deployment procedure for **The Great Ayurveda** unified ASP.NET Core web application to RunASP.NET free Windows/IIS hosting.

---

## 2. Prerequisites
- A registered RunASP.NET hosting account.
- A free MS SQL Database provisioned from the RunASP.NET control panel.
- .NET 10 SDK installed on the build machine.

---

## 3. Publish Command
From the root of the repository, execute:

```powershell
dotnet publish src/Ayurveda.Web/Ayurveda.Web.csproj -c Release -r win-x64 --no-self-contained -o publish/Web
```

---

## 4. Which Folder to Upload
Upload the **contents** of the local publish directory:

- **Local Publish Folder**: `./publish/Web`
- **Destination Folder on RunASP.NET**: `/site/wwwroot` (or the root directory of your site via RunASP.NET File Manager, FTP/FTPS, or WebDeploy)

> **Note**: Do not upload the enclosing `Web` folder itself; upload all files and subfolders inside `./publish/Web` (`Ayurveda.Web.dll`, `web.config`, `appsettings.json`, `wwwroot/`, etc.) directly into `/site/wwwroot`.

---

## 5. RunASP.NET Environment Variables
Configure the following environment variables in the RunASP.NET Control Panel (under Site Configuration / Environment Variables) or within `web.config` under the `<aspNetCore><environmentVariables>` section:

| Environment Variable | Recommended Value / Format | Purpose |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runs the application in ASP.NET Core Production mode |
| `ConnectionStrings__DefaultConnection` | `Server=mssqlXX.runasp.net;Database=<your_db_name>;User Id=<your_db_user>;Pwd=<your_db_password>;TrustServerCertificate=True;MultipleActiveResultSets=true;` | Connection string to the RunASP.NET MS SQL database |
| `Site__BaseUrl` | `http://<your-subdomain>.runasp.net` | Canonical base URL used for sitemap, robots, and metadata tags (**Important**: keep `http://` until HTTPS certificate is activated, then switch to `https://`) |
| `SeedData__Enabled` | `true` *(first run only)* / `false` *(after initial seeding)* | Controls database auto-seeding |
| `Admin__InitialPassword` | `<your-strong-initial-admin-credential>` | Initial password for `admin@thegreatayurveda.com` on first seed |
| `DemoMode__SkipOtp` | `false` | Enforces OTP verification logic for sensitive flows |
| `DemoMode__ShowCredentialChips` | `false` | Disables demo helper credential chips on login screens |
| `Auth__RequireEmailVerification` | `false` | Allows immediate access upon customer/doctor signup |
| `Smtp__Host` | *(Optional: leave blank or set to your SMTP relay, e.g., `smtp-relay.brevo.com`)* | SMTP host for email dispatch |
| `Smtp__Port` | `587` | SMTP port |
| `Smtp__Username` | *(Optional SMTP username)* | SMTP username |
| `Smtp__Password` | *(Optional SMTP credentials)* | SMTP outbound credential |
| `Smtp__FromEmail` | `care@thegreatayurveda.com` | Outbound sender email |

> **Credential Notice**: Never store plain-text credentials in source control. Always substitute `<placeholder>` values with your actual configuration directly in the host control panel.

---

## 6. First-Run Steps & Database Seeding Workflow

1. **Create the MS SQL Database**:
   - In RunASP.NET Control Panel, navigate to **Databases** -> **MS SQL**.
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
