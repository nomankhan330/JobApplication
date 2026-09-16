# Code Review Points - JobApplication (C&F Management System)

> Yeh file review points ko chunk-wise save karne ke liye hai. Har point ki status track hone ke liye checkbox lagaye gaye hain.
> Naye session mein is file ka path provide karein aur keh dein ke agla chunk fix karna hai.

## Project Info
- **Stack:** ASP.NET Core MVC (.NET 8), EF Core + Dapper + ADO.NET, SQL Server, DinkToPdf, ClosedXML
- **Projects:** `JobApplication` (MVC web), `BusinessLogic` (class library: Models/Services/Interfaces/Helper)
- **Auth:** Session-based (custom `SessionHelper`, `BaseController` guard)
- **Build:** `JobApplication.sln` (VS 2022)

---

## CHUNK 1: CRITICAL - Security Issues

- [ ] **1.1 Hardcoded Encryption Keys**
  - Files: `BusinessLogic\Helper\EncryptionHelper.cs:9-12`, `BusinessLogic\Helper\Enc.cs:12-19`
  - `EncryptionKey = "12345678901234567890123456789012"`, `EncryptionIV = "1234567890123456"`, `PasswordKey`, `EncryptionKey`, `initVector`, `passwordPhrase` source code mein hardcoded hain.
  - Fix: Keys ko `appsettings.json` ya User Secrets / Environment Variables mein move karein.
  - 🔴 STATUS: NOT STARTED

- [ ] **1.2 Exposed Production DB Credentials**
  - File: `JobApplication\appsettings.json:11`
  - Production connection string (IP `95.217.204.85`, `foregoxc_user`, password `X?17eir50`) comment mein exposed hai.
  - Fix: Turant hatao. Credentials rotate karo (password change karo). `appsettings.Production.json` ya env vars use karo.
  - 🔴 STATUS: NOT STARTED

- [ ] **1.3 Weak Password Encryption (Reversible AES)**
  - File: `BusinessLogic\Services\Account.cs:96` (login), `Account.cs:200` (save user)
  - Passwords AES (`EncryptionHelper.Encrypt`) se encrypt ho rahe hain jo reversible hai. DB compromise hone par saare passwords decrypt ho sakte hain.
  - Fix: BCrypt / PBKDF2 / Argon2 one-way hashing use karo. Existing encrypted passwords ke liye migration plan banayein.
  - 🔴 STATUS: NOT STARTED

- [ ] **1.4 MD5 Hashing (Obsolete)**
  - File: `BusinessLogic\Helper\Enc.cs:141,158-170`
  - `MD5CryptoServiceProvider` cryptographic use ke liye unsafe hai (collision attacks).
  - Fix: In methods ko hatao ya SHA-256+ se replace karo (agar koi legacy dependency nahi hai).
  - 🔴 STATUS: NOT STARTED

- [ ] **1.5 SQL Injection Risk (Raw SQL Queries)**
  - Files: `BusinessLogic\Services\Account.cs:58-60` (dead `sql1`), `Account.cs:63-92`, `DatabaseObject.cs` (poori class mein raw SQL)
  - File: `Account.cs:58-60` - `sql1` dead string hai par agar kabhi use hogi without parameterized query toh risk hai.
  - `DatabaseObject.cs` mein queries parameterized hain (theek hai), lekin saari jagah ensure karein ke kabhi bhi user input direct concatenate na ho.
  - Fix: Sirf parameterized queries / stored procedures use karein. Dead `sql1` hatao.
  - 🔴 STATUS: NOT STARTED

---

## CHUNK 2: HIGH - Security & Code Quality

- [ ] **2.1 DB Credentials Stored in Session**
  - File: `BusinessLogic\Interfaces\ISessionHelper.cs:24-25`, `BusinessLogic\Services\SessionHelper.cs:161-197`
  - `Dbpassword`, `Dbuserid`, `Datasource`, `Databasename`, `ExpiryDate` session mein stored hain — data leak ka risk.
  - Fix: Na rakhein jab tak zaroori na ho. Agar multi-tenant connection dynamic hai toh encrypted + short-lived rakhein.
  - 🔴 STATUS: NOT STARTED

- [ ] **2.2 Dead Code Cleanup**
  - Files:
    - `BusinessLogic\Services\Account.cs:334-475` - `SaveUserBk()`
    - `BusinessLogic\Services\Account.cs:563-620` - `GetUserBk()`
    - `BusinessLogic\Services\Account.cs:48-54` - commented login code
    - `BusinessLogic\Helper\Enc.cs` - purane MD5 functions
    - `DatabaseObject.cs` - poori class mein commented `_logs.Write()` lines (84, 94, 145, 153, 203, 212, 267, 277, 331, 341, 395, 402, 450, 457, 487, 493, 535, 542, 575, 582, 631, 638)
    - `BusinessLogic\Class1.cs` - empty template file
  - Fix: Saara unused code hatao.
  - 🔴 STATUS: NOT STARTED

- [ ] **2.3 Redundant Session Checks**
  - Files: `JobApplication\Controllers\BaseController.cs:17-47`, `JobApplication\Controllers\AccountController.cs:33-36, 138-144, 159-166`
  - `BaseController.OnActionExecuting` session check karta hai, phir bhi `AccountController` mein manual `if (_session.LoginId == 0)` checks hain — redundant.
  - Fix: BaseController ka check hi kaafi hai. Duplicate checks hatao (Action jinhe khud handle karne hain unko chhor kar).
  - 🔴 STATUS: NOT STARTED

- [ ] **2.4 Logout Poori Session Clear Nahi Karta**
  - File: `BusinessLogic\Services\SessionHelper.cs:375-386`
  - `Logout()` sirf 7 keys remove karta hai, lekin `COST_CENTERS`, `COST_CENTERIDS`, `TOKEN`, `Dbpassword`, `Dbuserid`, `ExpiryDate`, `Datasource`, `Databasename`, `RIGHTSLIST`, `MENULIST`, `AccountName` waghera clear nahi hote.
  - Fix: `_session.Clear()` use karo ya saari keys remove karo.
  - 🔴 STATUS: NOT STARTED

- [ ] **2.5 AppDbContext Double Registration**
  - File: `JobApplication\Program.cs:38-43`
  - Line 38 `AddScoped<AppDbContext>()` + Line 39 `AddDbContext<AppDbContext>()` — do baar registration.
  - Fix: Line 38 hatao, sirf `AddDbContext` rakho.
  - 🔴 STATUS: NOT STARTED

---

## CHUNK 3: MEDIUM - Robustness & Consistency

- [ ] **3.1 Redundant Connection Close Checks**
  - File: `BusinessLogic\Services\DatabaseObject.cs:38-41, 112-115, 173-176, 231-234, 296-299`
  - Har method mein `if (SqlCon.State == ConnectionState.Open) await SqlCon.CloseAsync();` — naya `SqlConnection` hamesha closed hota hai, yeh redundant hai.
  - Fix: Remove karo.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.2 Input Validation Missing**
  - Files: Saare controllers (`JobController.cs`, `AccountController.cs`, `InvoiceController.cs`, etc.)
  - `ModelState.IsValid` check kahi nahi hai. User input directly SQL parameters/code mein jata hai.
  - Fix: Models pe DataAnnotations (Required, MaxLength, etc.), controllers mein `ModelState.IsValid` check.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.3 Transaction Disposal**
  - Files: `BusinessLogic\Services\Account.cs:175, 760`, `SalesInvoiceService.cs:65`, etc.
  - `using (var transaction = _context.Database.BeginTransaction())` kuch jagah (theek hai), lekin kuch jagah `using var transaction = await ...BeginTransactionAsync()` — disposal consistent honi chahiye.
  - Fix: Har jagah `using var` + `async` pattern use karo aur `IAsyncDisposable` ensure karo.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.4 Inconsistent JSON Serialization**
  - Files: Saare controllers
  - Two approaches: `Content(JsonConvert.SerializeObject(result), "application/json")` (Newtonsoft) vs `return Json(result)` (System.Text.Json).
  - Fix: Ek approach select karo entire project mein. Property casing ke liye bhi plan banao.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.5 2GB Request Body Limit (DoS Risk)**
  - File: `JobApplication\Program.cs:45-53`
  - `MaxRequestBodySize = int.MaxValue` (Kestrel + IIS).
  - Fix: Reasonable limit (file uploads ke liye ~50-100MB). Uploads ke liye streaming approach bhi consider karo.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.6 CSRF Protection Missing**
  - Files: Saare POST actions aur AJAX calls
  - Session-based auth hai, lekin `[ValidateAntiForgeryToken]` kisi POST pe nahi hai. jQuery AJAX bina CSRF token ke ja raha hai.
  - Fix: Forms mein `@Html.AntiForgeryToken()`, controllers pe `[ValidateAntiForgeryToken]`, AJAX mein token header mein bhejo.
  - 🔴 STATUS: NOT STARTED

- [ ] **3.7 Unused Using Directives**
  - Files: `BusinessLogic\Services\Account.cs:1, 14, 16-21`, `DatabaseObject.cs:2, 10, 12-13`, `SessionHelper.cs:2, 10, 13`, `SalesInvoiceService.cs:1, 14, 27-28`, etc.
  - Fix: Unused using statements hatao.
  - 🔴 STATUS: NOT STARTED

---

## CHUNK 4: LOW - Maintainability (Optional/Refactor)

- [ ] **4.1 Magic Numbers**
  - Files: Poora codebase
  - `UserType` (1,2,3,4), `LoginType` (1,2,3), `CompanyId = 1` (`Account.cs:198`) hardcoded hain.
  - Fix: Enums ya constants banayein (e.g., `UserType.Admin`, `UserType.Authorized`, etc.).
  - 🔴 STATUS: NOT STARTED

- [ ] **4.2 Inconsistent Error Responses**
  - Files: Saare services
  - Kuch jagah `errorCode + errorMessage`, kuch jagah `errorCode + message`, kuch jagah `errorCode + data`. Response shape alag hai.
  - Fix: Common `ApiResponse<T>` class banayein.
  - 🔴 STATUS: NOT STARTED

- [ ] **4.3 Huge Service Files**
  - Files:
    - `BusinessLogic\Services\SalesInvoiceService.cs` (~2662 lines)
    - `BusinessLogic\Services\JobImportMasterService.cs` (~2200 lines)
    - `BusinessLogic\Services\PurchaseInvoiceService.cs` (~1490 lines)
  - Fix: PDF generation, Excel export, validations ko separate classes mein todhein.
  - 🔴 STATUS: NOT STARTED

- [ ] **4.4 No Proper Logging Framework**
  - File: `BusinessLogic\Services\Logs.cs` (custom `ILogs`)
  - Custom logging hai. Production ke liye structured logging behtar hai.
  - Fix: Serilog/NLog integrate karo (optional, agar zaroorat ho).
  - 🔴 STATUS: NOT STARTED

---

## Build / Verification Commands
```powershell
# Build solution
dotnet build "JobApplication.sln"

# Run web project (dev)
dotnet run --project "JobApplication\JobApplication.csproj"
```

## Notes for next session
- Har fix ke baad `dotnet build` chala kar verify karein.
- Ek chunk complete hone ke baad STATUS ko `[x]` aur `✅ COMPLETED` mark karein.
- Har chunk alag session mein kar sakte hain.