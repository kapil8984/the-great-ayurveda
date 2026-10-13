# Production Deployment Guide: MonsterASP.NET (Free Site)

## 1. Overview
This guide provides the complete, exact production deployment procedure for **The Great Ayurveda** unified ASP.NET Core web application to MonsterASP.NET free Windows/IIS hosting.

---

## 2. Prerequisites
- A registered MonsterASP.NET hosting account.
- A free MS SQL Database provisioned from the MonsterASP control panel.
- .NET 10 SDK installed on the build machine.

---

## 3. Publish Command
From the root of the repository, execute:

```powershell
dotnet publish src/Ayurveda.Web/Ayurveda.Web.csproj -c Release -o ./publish/Web
```

---

## 4. Which Folder to Upload
Upload the **contents** of the local publish directory:

- **Local Publish Folder**: `./publish/Web`
- **Destination Folder on MonsterASP**: `/site/wwwroot` (or the root directory of your site via MonsterASP File Manager, FTP/FTPS, or WebDeploy)

> **Note**: Do not upload the enclosing `Web` folder itself; upload all files and subfolders inside `./publish/Web` (`Ayurveda.Web.dll`, `web.config`, `appsettings.json`, `wwwroot/`, etc.) directly into `/site/wwwroot`.

---

## 5. MonsterASP Environment Variables
Configure the following environment variables in the MonsterASP Control Panel (under Site Configuration / Environment Variables) or within `web.config` under the `<aspNetCore><environmentVariables>` section:

| Environment Variable | Recommended Value / Format | Purpose |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runs the application in ASP.NET Core Production mode |
| `ConnectionStrings__DefaultConnection` | `Server=mssqlXX.monsterasp.net;Database=<your_db_name>;User Id=<your_db_user>;Password=<your_db_password>;TrustServerCertificate=True;MultipleActiveResultSets=true;` | Connection string to the MonsterASP MS SQL database |
| `Site__BaseUrl` | `https://<your-subdomain>.monsterasp.net` | Canonical base URL used for `sitemap.xml`, `robots.txt`, and metadata tags |
| `SeedData__Enabled` | `true` *(first run only)* / `false` *(after initial seeding)* | Controls database auto-seeding |
| `Admin__InitialPassword` | `<your-secure-strong-admin-password>` | Initial password for `admin@thegreatayurveda.com` on first seed |
| `DemoMode__SkipOtp` | `false` | Enforces OTP verification logic for sensitive flows |
| `DemoMode__ShowCredentialChips` | `false` | Disables demo helper credential chips on login screens |
| `Auth__RequireEmailVerification` | `false` | Allows immediate access upon customer/doctor signup |
| `Smtp__Host` | *(Optional: leave blank or set to your SMTP relay, e.g., `smtp-relay.brevo.com`)* | SMTP host for email dispatch |
| `Smtp__Port` | `587` | SMTP port |
| `Smtp__Username` | *(Optional SMTP username)* | SMTP username |
| `Smtp__Password` | *(Optional SMTP password)* | SMTP password |
| `Smtp__FromEmail` | `noreply@<your-subdomain>.monsterasp.net` | Outbound sender email |

> **Security Notice**: Never store plain-text secrets in source control. Always substitute `<placeholder>` values with your actual secrets directly in the host control panel.

---

## 6. First-Run Steps & Database Seeding Workflow

1. **Create the MS SQL Database**:
   - In MonsterASP Control Panel, navigate to **Databases** -> **MS SQL**.
   - Create a new database.
   - Record the host server (`mssqlXX.monsterasp.net`), database name, username, and password.

2. **Configure Initial Environment Variables**:
   - In the MonsterASP site settings, configure all environment variables listed in Section 5.
   - Ensure `SeedData__Enabled` is set to `true`.
   - Set `Admin__InitialPassword` to your chosen strong administrator password.

3. **Upload the Published Files**:
   - Run the publish command (Section 3).
   - Connect via FTP or the MonsterASP File Manager.
   - Upload all files from `./publish/Web` into `/site/wwwroot`.

4. **Trigger Initial Migration and Seeding**:
   - Open a browser and visit your site: `https://<your-subdomain>.monsterasp.net/`.
   - The application automatically applies pending Entity Framework Core migrations and populates initial categories, products, verified doctors, hero banners, and active coupons (`HEALTH15`).
   - The administrator account `admin@thegreatayurveda.com` is created with your configured `Admin__InitialPassword`.

5. **Verify Admin Portal Access**:
   - Navigate to `https://<your-subdomain>.monsterasp.net/admin-portal/login`.
   - Log in with `admin@thegreatayurveda.com` and your initial password.
   - Confirm access to the Admin Dashboard, Catalog, Doctor Verification, and Order management.

6. **Lock Seed Data (Post-Seed Step)**:
   - Return to the MonsterASP Control Panel -> Site Configuration -> Environment Variables.
   - Change `SeedData__Enabled` to `false`.
   - Restart the website/application pool.
   - **Verification**: With `SeedData__Enabled=false`, subsequent site restarts or app pool recycles will preserve existing database state without duplicate records or password resets.

---

## 7. File Permissions & Shared Uploads
- Media uploaded through the Admin Portal (product photos, banner images, doctor avatars) is stored under `wwwroot/uploads/`.
- Ensure the IIS Application Pool user (`IIS_IUSRS` / site user) has Write and Modify permissions on `/site/wwwroot/wwwroot/uploads/`.
