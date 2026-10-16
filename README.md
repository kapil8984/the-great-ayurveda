# 🌿 The Great Ayurveda

A full-stack demonstration e-commerce and telemedicine platform for classical Ayurvedic formulations and consultation scheduling, built on **ASP.NET Core 10 (MVC)** and **Clean Architecture**.

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20Onion-success)](file:///d:/TheGreatAyurveda/The-Great-Ayurveda/src)
[![Tests](https://img.shields.io/badge/Tests-118%20Passed-brightgreen)](file:///d:/TheGreatAyurveda/The-Great-Ayurveda/tests/Ayurveda.API.Tests)

---

## 📌 Overview

**The Great Ayurveda** is a software demonstration showcasing modern web engineering practices in ASP.NET Core:
1. **Public Storefront:** Browsable catalog of 312 classical Ayurvedic formulation listings with distinct imagery, categories, pricing, herbology details, dosage notes, and session-based guest carts.
2. **Doctor Clinical Portal:** Fictional doctor profiles, tele-consultation booking calendar with time-slot reservation, simulated video/audio modes, and digital prescription workflows.
3. **Administrative Portal (`/admin-portal`):** Management console for catalog operations, category management, doctor profile approvals, order tracking, and promotional banners.

> **Demonstration Notice:** This platform is an educational software demonstration. All formulations, pricing, doctor profiles, and reviews represent sample or illustrative data. No real commercial transactions, medical advice, or physical shipping occurs.

---

## 🏛️ Architecture

The solution follows a multi-tier Clean Architecture pattern:
- **`src/Ayurveda.Domain`**: Core domain entities (Products, Categories, Orders, Doctors, Appointments, ApplicationUser) and enums.
- **`src/Ayurveda.Application`**: ViewModels, Data Transfer Objects (DTOs), and service interfaces.
- **`src/Ayurveda.Infrastructure`**: EF Core `AyurvedaDbContext`, SQL Server migrations, identity configuration, image storage, cache busting, and rolling file logging.
- **`src/Ayurveda.Web`**: The single runnable web application containing the public storefront, customer accounts, doctor portal, and the Admin area (`/admin-portal`).

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or higher
- Microsoft SQL Server 2022, SQL Server Express, or **LocalDB** (standard on Windows with Visual Studio)

### 1. Clone & Build
```bash
git clone https://github.com/kapil-Dharwal/The-Great-Ayurveda.git
cd The-Great-Ayurveda
dotnet build
```

### 2. Configure Database Connection
Configure your SQL Server connection string in `src/Ayurveda.Web/appsettings.json` or via environment variable:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

Or via environment variable:
```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=AyurvedaDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True"
```

### 3. Database Initialization & Seeding
On application startup, database migrations and initial seed data are applied automatically:
- **312 Products:** Seeded directly from `catalog_300plus.json` with matching image assets in `wwwroot/images/products`.
- **24 Fictional Doctor Profiles:** Representing various classical disciplines.
- **Administrative Roles & Users:** Configures default roles (`Admin`, `CatalogManager`, `FinanceManager`, `SupportAgent`, `DemoViewer`).

### 4. Running the Application
```bash
dotnet run --project src/Ayurveda.Web --urls "http://localhost:5000"
```
Navigate to `http://localhost:5000` in your browser.

---

## 🧪 Demonstration Mode (`Demo:OpenAccess`)

When `Demo:OpenAccess` is enabled (`true`), the application provides public evaluation features:
- **Customer Sign-In & Checkout:** Any email address can sign in or register as a sandbox customer without real OTPs or external emails. Real administrative, staff, and seeded doctor accounts are protected and cannot be accessed with arbitrary passwords.
- **Simulated Payment Sandbox:** Orders and consultation fees simulate payment with a toggleable failure scenario and clear demo labels. No external payment calls are made.
- **Doctor Portal Demo:** `/doctor/login` allows access to an approved fictional demo doctor profile (`Dr. Aarav Sharma`) with sample appointments.
- **Read-Only Admin Demo (`DemoViewer`):** Real administrative credentials remain protected. Visitors can evaluate the administrative portal using the `DemoViewer` role (`demo-admin@ayurveda.local` / `DemoAdmin@2026!`). All create, edit, and delete requests are safely rejected server-side, and customer contact information is masked.
- **Data Isolation & Cleanup:** Demo users have isolated carts and bookings. Demo accounts older than 24 hours are lazily purged on login, and total demo accounts are capped at 300.

---

## 🔬 Testing

The test suite consists of **118 automated integration and unit tests** in `tests/Ayurveda.API.Tests`:
- Core e-commerce workflows (guest cart isolation, cart merge at login, checkout)
- Doctor consultation booking and double-booking prevention
- Authentication security, lockout, and protected account isolation
- Admin role-based authorization and DemoViewer mutation prevention
- Cache busting and rate limiting policies

To execute all tests:
```bash
dotnet test
```
