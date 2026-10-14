# Security Fixes & Repository Audit (Phase 0)

This document details all security vulnerabilities identified in the initial audit, the remediation implemented in `Ayurveda.API`, and the automated test suites proving the effectiveness of each fix.

---

## 1. Summary of Vulnerabilities & Fixes

### a) Broken Access Control & Missing Authorization Policies
- **Vulnerability**: Endpoints across `AdminsController`, `OrdersController.UpdateStatus`, `UsersController.DeleteUser`, `DoctorsController.RemoveDoctor`, `ProductsController` (POST/PUT/DELETE), `CategoriesController` (POST/DELETE), and `YogaController` (POST/PUT/DELETE) had no `[Authorize]` attribute, permitting any anonymous user to modify catalog items, approve doctors, alter order statuses, and delete users or doctors.
- **Fix**:
  - Enforced `[Authorize(Roles = "Admin")]` on all administrative endpoints in `AdminsController`, `OrdersController.UpdateStatus`, `UsersController.DeleteUser`, `DoctorsController.RemoveDoctor`, and catalog write endpoints in `ProductsController`, `CategoriesController`, and `YogaController`.
  - Added role-based checks ensuring only authenticated customers can place orders/manage carts, and doctors can manage their consultation slots.
- **Verification Tests**:
  - `AdminEndpoints_AnonymousUser_ReturnsUnauthorized` in `AuthorizationAndSecurityTests.cs`
  - `AdminEndpoints_CustomerUser_ReturnsForbidden` in `AuthorizationAndSecurityTests.cs`
  - `AdminEndpoints_AdminUser_ReturnsOk` in `AuthorizationAndSecurityTests.cs`
  - `DeleteUser_CustomerUser_ReturnsForbidden` in `AuthorizationAndSecurityTests.cs`
  - `UpdateOrderStatus_CustomerUser_ReturnsForbidden` in `AuthorizationAndSecurityTests.cs`

---

### b) Public Admin Registration Removed & Secure Admin Bootstrapping
- **Vulnerability**: `/api/auth/admin/signup` allowed arbitrary public registration of administrator accounts without verification or invitation.
- **Fix**:
  - Completely removed the admin registration action from `AuthController`.
  - Added a CLI seed command to `Program.cs`: `dotnet run -- seed-admin --email <email> --password <password> --name <name> --contact <contact>` which provisions an administrator with `MustChangePassword = true`.
  - Added support for environment variable bootstrap on first startup (`ADMIN_BOOTSTRAP_EMAIL`, `ADMIN_BOOTSTRAP_PASSWORD`).
- **Verification Tests**:
  - `PublicAdminSignup_EndpointRemoved_ReturnsNotFound` in `AuthorizationAndSecurityTests.cs`
  - `SeedAdmin_CreatesAccountWithForcedPasswordChange` in `AuthenticationSecurityTests.cs`

---

### c) Insecure Direct Object Reference (IDOR) Elimination
- **Vulnerability**: Request bodies for cart (`AddToCartRequest`, `UpdateCartQuantityRequest`), orders (`CreateOrderRequest`), appointments (`BookAppointmentRequest`), and user profile updates allowed passing arbitrary `userId` and `doctorId` values, enabling attackers to inspect, alter, or hijack other users' data.
- **Fix**:
  - Ignored client-provided `userId` and `doctorId` across all cart, order, profile, and appointment requests.
  - Derived identity strictly from the authenticated principal claims (`ClaimTypes.NameIdentifier`).
  - Added role ownership checks: Customers can only query their own orders/appointments/cart; Doctors can only query their own appointments; Admins can query all.
- **Verification Tests**:
  - `CartEndpoints_IgnoreBodyUserId_DerivesFromAuthenticatedPrincipal` in `IdorAndDataProtectionTests.cs`
  - `UserProfile_OtherUserProfileLookup_ReturnsForbidden` in `IdorAndDataProtectionTests.cs`
  - `OrderDetails_OtherUserOrderLookup_ReturnsForbidden` in `IdorAndDataProtectionTests.cs`

---

### d) JWT Token Configuration & Lifetime Hardening
- **Vulnerability**: Hardcoded fallback JWT key in `Program.cs` and `AuthService.cs` (`[REDACTED_LEGACY_KEY]$LongEnoughForHmacSha256`), access token valid for 7 days without fail-fast validation.
- **Fix**:
  - Moved JWT token to configuration/environment variables/user-configuration.
  - Added startup fail-fast check in `Program.cs`: throws `InvalidOperationException` if `Jwt:Key` is missing or UTF-8 byte count is `< 32` bytes (256 bits).
  - Shortened access token lifetime to 30 minutes (`Jwt:ExpiresInMinutes`).
- **Verification Tests**:
  - `JwtGeneration_KeyTooShort_ThrowsInvalidOperationException` in `AuthenticationSecurityTests.cs`

---

### e) Elimination of Default Seeding in Production
- **Vulnerability**: `DbInitializer.cs` automatically created test accounts with predictable credentials (`[REDACTED_DEV_ADMIN]`, `[REDACTED_DEV_DOCTOR]`, `[REDACTED_DEV_USER]`) whenever the database started.
- **Fix**:
  - Guarded seeding: data seeding executes **only** when `Environment.IsDevelopment()` or if explicitly opted-in via configuration `SEED_DATA=true`.
  - Production deployments run schema migrations only, with zero auto-seeded test accounts.

---

### f) Environment-Restricted Swagger & Strict CORS
- **Vulnerability**: Swagger and Swagger UI were enabled unconditionally in all environments. CORS was set to `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`.
- **Fix**:
  - Enforced `if (app.Environment.IsDevelopment())` for `UseSwagger()` and `UseSwaggerUI()`.
  - Restricted CORS to explicit origins configured under `Cors:AllowedOrigins` (e.g. `http://localhost:3000`), rejecting wildcard origins.

---

### g) Cryptographic Password Reset & Email Sender Abstraction
- **Vulnerability**: 4-digit predictable OTP (`Random.Shared.Next(1000, 9999)`), OTP stored in plaintext in the database, leaked to `Console.WriteLine`, no rate limits or brute-force attempt limits, forgot-password leaked account existence.
- **Fix**:
  - Generated cryptographically secure 6-digit OTP using `RandomNumberGenerator.GetInt32(100000, 1000000)`.
  - Stored SHA-256 hashed OTP (`OtpHashed`) in the database; plaintext OTP is never persisted.
  - Added 10-minute expiry and capped attempts at 5: upon 5 failed attempts, OTP is permanently invalidated.
  - Rate-limited reset requests to 1 per 60 seconds per email address.
  - Unified `ForgotPasswordAsync` to return generic message (`"If the account exists, a verification OTP has been dispatched to the email."`), mitigating user enumeration attacks.
  - Replaced `Console.WriteLine` with `IEmailSender` abstraction: MailKit SMTP for production, and an isolated local `outbox/` directory for development.
- **Verification Tests**:
  - `ForgotPassword_NonExistentEmail_ReturnsGenericResponseWithoutLeakingExistence` in `AuthenticationSecurityTests.cs`

---

### h) Generic Login Error Messages & Account Lockout
- **Vulnerability**: Login responses differentiated between "Invalid email id" and "Invalid password", facilitating email harvesting. No lockout existed against brute-force attacks.
- **Fix**:
  - Standardized all failed login responses to `"Invalid email or password"`.
  - Added `AccessFailedCount` and `LockoutEndUtc` fields to `User`, `Doctor`, and `Admin` models.
  - Triggered 15-minute account lockout upon 5 consecutive failed attempts.
- **Verification Tests**:
  - `Login_InvalidCredentials_ReturnsGenericErrorMessage` in `AuthenticationSecurityTests.cs`
  - `Login_FiveFailedAttempts_TriggersAccountLockout` in `AuthenticationSecurityTests.cs`

---

### i) Canonical Email Normalization & Unique Constraints
- **Vulnerability**: Email lookups and uniqueness checks were inconsistent in casing/whitespace handling; `Doctor.ContactNumber` lacked unique indexing.
- **Fix**:
  - Implemented `NormalizeEmail`: `.Trim().ToLowerInvariant()` applied across registration, login, and password resets.
  - Added unique index on `Doctor.ContactNumber` and verified unique index on `Email` in EF Core model configuration.
- **Verification Tests**:
  - `EmailNormalization_IgnoresWhitespaceAndCasing` in `AuthenticationSecurityTests.cs`

---

### j) Concurrency Token & Filtered Double-Booking Index
- **Vulnerability**: Parallel appointments could double-book the same doctor slot. Parallel order checkout could decrement inventory below zero without optimistic concurrency protection.
- **Fix**:
  - Added `DateTime StartUtc` property on `Appointment`.
  - Added a unique filtered index on `(DoctorId, StartUtc)` where `[Status] NOT IN ('Cancelled', 'Declined')`.
  - Handled `DbUpdateException` on duplicate slot insertions, returning friendly error: `"This appointment slot has just been booked by another user. Please choose an alternate slot or date."`
  - Added `RowVersion` concurrency token (`byte[] RowVersion` with `.IsRowVersion()`) on `Product`. Handled `DbUpdateConcurrencyException` safely during order placement.
- **Verification Tests**:
  - `AppointmentDoubleBooking_DuplicateSlot_ReturnsFriendlyErrorMessage` in `ConcurrencyAndBookingTests.cs`
  - `OrderCreation_ExceedsStock_FailsSafely` in `ConcurrencyAndBookingTests.cs`

---

### k) Doctor Verification & Approval Gate
- **Vulnerability**: Unapproved or rejected doctors were able to log in and appeared in public listings immediately upon sign-up.
- **Fix**:
  - In `AuthService.LoginDoctorAsync`, blocked sign-in if `DoctorDetail.Status != 1` with explicit message (`"Your account is pending administrator approval"`).
  - Filtered public directory queries (`GetAllDoctorsAsync`) to only return doctors with `DoctorDetail.Status == 1`.
- **Verification Tests**:
  - `DoctorLogin_UnapprovedDoctor_CannotLogin` in `AuthenticationSecurityTests.cs`

---

### l) RFC 7807 ProblemDetails Global Error Handling
- **Vulnerability**: `ExceptionMiddleware` leaked `exception.Message` directly in JSON responses, exposing internal architecture details and sensitive exceptions on 5xx errors.
- **Fix**:
  - Refactored `ExceptionMiddleware` to return standard RFC 7807 `ProblemDetails` with `Content-Type: application/problem+json`.
  - Sanitized 500 errors to `"Internal Server Error"` with generic detail `"An unexpected error occurred while processing your request. Please try again later."` and included `traceId`.
- **Verification Tests**:
  - `ExceptionMiddleware_Unhandled500Exception_ReturnsRfc7807ProblemDetailsWithoutRawDetails` in `FileUploadAndProblemDetailsTests.cs`

---

### m) Hardened File Upload Security
- **Vulnerability**: `FileStorageService` validated only file extension string without checking `Content-Type` header, file size limits were 10MB without magic byte inspection, and filenames used pseudo-random timestamps.
- **Fix**:
  - Validated both file extension whitelist (`.jpg`, `.jpeg`, `.png`, `.webp`) and MIME `Content-Type` header.
  - Implemented magic byte inspection (JPEG: `FF D8 FF`, PNG: `89 50 4E 47`, WEBP: `RIFF...WEBP`).
  - Reduced maximum upload size to 5MB.
  - Used cryptographically secure random names (`RandomNumberGenerator.GetBytes(16)` hex) and ensured destination path stays strictly within intended upload directory (preventing directory traversal attacks).
- **Verification Tests**:
  - `FileStorageService_InvalidExtension_ThrowsArgumentException` in `FileUploadAndProblemDetailsTests.cs`
  - `FileStorageService_ForgedExtensionWithInvalidMagicBytes_ThrowsArgumentException` in `FileUploadAndProblemDetailsTests.cs`
  - `FileStorageService_ValidJpegWithProperMagicBytes_Succeeds` in `FileUploadAndProblemDetailsTests.cs`

---

### n) Fail-Fast Database Initialization
- **Vulnerability**: `Program.cs` caught seeding/migration exceptions and swallowed them with a log message, allowing the API to launch in a corrupted or half-migrated state.
- **Fix**:
  - Configured `Program.cs` to log critical error and immediately rethrow (`throw;`) on database migration or seeding failures, stopping startup.

---

### o) Repository Cleanup & Credentials Audit
- **Cleaned Directories & Files**:
  - Removed dead Node/Express/Mongo code: `/Backend Ayurveda` deleted.
  - Removed stale build artifacts: `/Frontend/build` deleted.
  - Removed duplicate files: `Frontend/src/components/yoga/showyogadetails22.js` deleted.
  - Added comprehensive root `.gitignore` covering `bin/`, `obj/`, `.env`, `node_modules/`, `build/`, `outbox/`, `uploads/`, and LocalDB files.
- **Credentials Identified for Rotation**:
  1. `[REDACTED_LEGACY_KEY]$LongEnoughForHmacSha256` (JWT Token Key previously present in `appsettings.json`, `Program.cs`, and `AuthService.cs`) -> Must be rotated in all deployment environments.
  2. Default seed passwords (`[REDACTED_DEV_ADMIN]`, `[REDACTED_DEV_DOCTOR]`, `[REDACTED_DEV_USER]`) -> Replaced with dev-only/opt-in secure bootstrap credentials.
  3. SQL Server LocalDB connection string in `appsettings.json` -> Placeholders for production SQL Server credentials.
  4. Node `.env` OAuth placeholders in deleted `Backend Ayurveda` (`CLIENT_ID`, `CLIENT_TOKEN`) -> Documented that no credentials were carried forward.

---

## 2. Test Suite Execution Summary
- **Test Project**: `tests/Ayurveda.API.Tests`
- **Framework**: .NET 10, xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, EF Core InMemory
- **Total Tests**: 26
- **Passed**: 26
- **Failed**: 0
- **Skipped**: 0
- **Vulnerabilities**: 0 detected (`dotnet list package --vulnerable`)
