# The Great Ayurveda — Execution & Operations Guide (PowerShell / VS Code)

This document provides exact, verified PowerShell commands to manage, build, run, test, and reset the **The Great Ayurveda** unified ASP.NET Core solution.

---

## 1. Project Names, Launch Profiles, & Ports

The solution is consolidated into the following core projects:
- **`src/Ayurveda.Domain/Ayurveda.Domain.csproj`** — Entities, Enums, and Value Objects.
- **`src/Ayurveda.Application/Ayurveda.Application.csproj`** — DTOs, ViewModels, and Core Interfaces.
- **`src/Ayurveda.Infrastructure/Ayurveda.Infrastructure.csproj`** — EF Core DbContext, Migrations, Seeders, and Services.
- **`src/Ayurveda.Web/Ayurveda.Web.csproj`** — Unified Web Application (Storefront, Doctor Portal, Admin Area).
- **`tests/Ayurveda.API.Tests/Ayurveda.API.Tests.csproj`** — Full Integration and Security Regression Test Suite.

### Launch Profiles (from `src/Ayurveda.Web/Properties/launchSettings.json`):
| Profile | Environment | Application URLs | Notes |
| :--- | :--- | :--- | :--- |
| **`http`** | Development | `http://localhost:5000` | Default local HTTP development profile |
| **`https`** | Development | `https://localhost:5001;http://localhost:5000` | Dual HTTPS/HTTP local development profile |

---

## 2. LocalDB Status & Existing Databases

As of the latest pre-push audit, running `sqlcmd -S "(localdb)\mssqllocaldb" -Q "SELECT name FROM sys.databases;"` shows **9 databases** present on `(localdb)\mssqllocaldb`:
1. `master` (system)
2. `tempdb` (system)
3. `model` (system)
4. `msdb` (system)
5. `JwtAuthDemoDb`
6. `AyurvedaDb` (primary development database)
7. **`AyurvedaApiDb`** — **YES, it still exists on the LocalDB instance**, preserved from legacy API runs.
8. `AyurvedaDb_ProdRehearsal`
9. `AyurvedaDb_ProdPass2`

---

## 3. Step-by-Step PowerShell Commands

### Step 1: Open the Repository Root
```powershell
cd d:\TheGreatAyurveda\The-Great-Ayurveda
```

### Step 2: Stop Any Running Processes (Free Locks and Ports)
```powershell
Get-Process -Name Ayurveda.Web,dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Step 3: Build the Solution
```powershell
dotnet build
```

### Step 4: Run the Complete Test Suite
```powershell
dotnet test tests/Ayurveda.API.Tests/Ayurveda.API.Tests.csproj
```

### Step 5: Reset the Database (Clean Slate Seed)
The real database reset script is **`reset-db.ps1`**:
```powershell
powershell -ExecutionPolicy Bypass -File .\reset-db.ps1
```
*(Drops `AyurvedaDb` safely. On the next application run, EF Core automatically applies all migrations and seeds categories, 312 products, 24 doctors, banners, and coupons).*

### Step 6: Start the Web Application
```powershell
dotnet run --project src/Ayurveda.Web/Ayurveda.Web.csproj --launch-profile http
```
*The server will start listening on `http://localhost:5000`.*

### Step 7: Open in Browser
```powershell
# Open Storefront
Start-Process "http://localhost:5000"

# Open Dedicated Administrative Portal
Start-Process "http://localhost:5000/admin-portal"
```
