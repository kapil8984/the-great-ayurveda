@echo off
set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__DefaultConnection=Server=(localdb)\mssqllocaldb;Database=AyurvedaDb_FinalPassProd;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
set Demo__OpenAccess=true
set SeedData__Enabled=true
set Admin__InitialPassword=AdminSecure@2026!
set RateLimit__TrustForwardedFor=false
set Site__BaseUrl=http://127.0.0.1:5099
set DOTNET_gcServer=0
set DOTNET_GCHeapHardLimit=0xC800000
cd /d "%~dp0..\publish\Web" && dotnet Ayurveda.Web.dll --urls http://127.0.0.1:5099
