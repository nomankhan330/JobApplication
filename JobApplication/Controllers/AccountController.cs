using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using BusinessLogic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using BusinessLogic.Helper;
using Microsoft.AspNetCore.Http;
using JobApplication.Helpers;

namespace JobApplication.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IAccount _account;
        private readonly ISessionHelper _session;
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public AccountController(IAccount account, ISessionHelper session, AppDbContext context, IAuditService audit) : base(session)
        {
            _account = account;
            _session = session;
            _context = context;
            _audit = audit;
        }

        [AllowAnonymous]
        public async Task<ContentResult> SubmitLogin([FromBody] LoginRequest request)
        {
            var result = await _account.Login(request.userid, request.password);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> SaveUser(User model, IFormFile PhotoFile)
        {
            if (_session.LoginId == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            if (model == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "Invalid request. No user data received." }),
                    "application/json");
            }

            //var ids = model.CostCenterIds;

            // Handle Photo Upload
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                if (!FileUploadHelper.IsAllowed(PhotoFile, out string uploadError))
                {
                    return Content(
                        JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = uploadError }),
                        "application/json");
                }

                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/users");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(PhotoFile.FileName)}";
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoFile.CopyToAsync(stream);
                }

                model.Photo = fileName;
            }

            var result = await _account.SaveUser(model);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetUser(IList<QueryFilters> filters)
        {
            var result = await _account.GetUser(filters);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetUserById(int Id)
        {
            var result = await _account.GetUserById(Id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> DeleteUserById(int Id)
        {
            var result = await _account.DeleteUserById(Id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var result = await _account.ToggleUserStatus(id);

            return Content(JsonConvert.SerializeObject(result), "application/json");

        }

        [HttpPost]
        public IActionResult DecryptPassword(string userId, string encryptedPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(encryptedPassword))
                {
                    return Json(new { success = false, message = "User Id or encrypted password is required." });
                }

                //string plainPassword = EncryptionHelper.Encrypt(userId);
                string plainPassword = EncryptionHelper.Decrypt(encryptedPassword);

                return Json(new { success = true, password = plainPassword });
            }
            catch (Exception ex)
            {
                // Error handling
                return Json(new { success = false, message = "Decryption failed: " + ex.Message });
            }
        }

        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> VerifySecurityAnswer(string userId, string answer)
        {
            var result = await _account.VerifySecurityAnswer(userId, answer);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string userId, string newPassword)
        {
            var result = await _account.ResetPassword(userId, newPassword);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult AdminUsers()
        {
            return View();
        }

        public IActionResult CostCenter()
        {
            return View();
        }

        public async Task<ContentResult> logout()
        {
            // ========== AUDIT: capture identity before the session is cleared ==========
            int loginId = _session.LoginId;
            string userName = _session.UserName;

            _session.Logout();

            // ========== AUDIT: record logout ==========
            // UserIdOverride/UserNameOverride are used because the session is already
            // cleared by this point.
            await _audit.RecordAsync(new AuditEntryRequest
            {
                Module = "Authentication",
                Action = "Logout",
                EntityName = "Users",
                EntityId = userName,
                Description = $"User {userName} logged out.",
                OldValues = null,
                NewValues = new Dictionary<string, object?>
                {
                    { "UserId", loginId > 0 ? loginId.ToString() : null },
                    { "UserName", userName }
                },
                PageName = "/Account/logout",
                UserIdOverride = loginId > 0 ? loginId : null,
                UserNameOverride = userName
            });

            return Content(JsonConvert.SerializeObject("000"), "application/json");
        }

        public IActionResult Lock()
        {
            _session.IsLocked = true;
            return RedirectToAction("LockScreen", "Account");
        }

        public IActionResult LockScreen()
        {
            if (_session.LoginId == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Unlock(string password)
        {
            if (_session.LoginId == 0)
            {
                return Json(new { errorCode = 401, errorMessage = "Session expired" });
            }

            if (string.IsNullOrEmpty(password))
            {
                return Json(new { errorCode = 400, errorMessage = "Please enter your password" });
            }

            bool valid = await _account.VerifyPassword(password);

            if (valid)
            {
                _session.IsLocked = false;
                return Json(new { errorCode = 200, data = _session.UserType });
            }

            return Json(new { errorCode = 401, errorMessage = "Invalid password" });
        }

        public IActionResult Profile()
        {
            if (_session.LoginId == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetProfile()
        {
            var result = await _account.GetProfile();

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(User model, IFormFile? PhotoFile)
        {
            if (_session.LoginId == 0)
            {
                return Unauthorized(new
                {
                    errorCode = 401,
                    errorMessage = "Session expired"
                });
            }

            if (model == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "Invalid request. No user data received." }),
                    "application/json");
            }

            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                if (!FileUploadHelper.IsAllowed(PhotoFile, out string uploadError))
                {
                    return Content(
                        JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = uploadError }),
                        "application/json");
                }

                string folder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/uploads/users");

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(PhotoFile.FileName)}";
                string filePath = Path.Combine(folder, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await PhotoFile.CopyToAsync(stream);

                model.Photo = fileName;
            }

            var result = await _account.UpdateProfile(model);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json");
        }

        // ----------------------------------------------------------------
        // Two-Factor Authentication (TOTP)
        // ----------------------------------------------------------------

        [AllowAnonymous]
        public IActionResult TwoFactor()
        {
            if (_session.Pending2FAUserId == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            return View();
        }

        [AllowAnonymous]
        public async Task<ContentResult> VerifyTwoFactor([FromBody] TwoFactorCodeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.code))
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "Verification code is required." }),
                    "application/json");
            }

            int userId = _session.Pending2FAUserId;
            if (userId == 0)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "No pending login found. Please login again." }),
                    "application/json");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 404, errorMessage = "User not found." }),
                    "application/json");
            }

            bool valid = !string.IsNullOrWhiteSpace(user.TwoFactorSecretKey)
                && TotpHelper.ValidateTotp(user.TwoFactorSecretKey, request.code);

            if (!valid && !string.IsNullOrWhiteSpace(user.TwoFactorRecoveryCodes))
            {
                string stored = user.TwoFactorRecoveryCodes;
                valid = TotpHelper.TryConsumeRecoveryCode(ref stored, request.code);
                if (valid)
                {
                    user.TwoFactorRecoveryCodes = stored;
                    await _context.SaveChangesAsync();
                }
            }

            if (!valid)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "Invalid verification code." }),
                    "application/json");
            }

            _session.Pending2FASecret = "";

            var result = await _account.CompleteTwoFactorLogin();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [AllowAnonymous]
        public async Task<ContentResult> GenerateTwoFactorSecret()
        {
            int userId = _session.Pending2FAUserId > 0 ? _session.Pending2FAUserId : _session.LoginId;
            if (userId == 0)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "No active user found." }),
                    "application/json");
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 404, errorMessage = "User not found." }),
                    "application/json");
            }

            var secret = TotpHelper.GenerateSecret();
            string accountName = string.IsNullOrWhiteSpace(user.UserId) ? $"user{userId}" : user.UserId.Trim();

            _session.Pending2FASecret = secret;

            string uri = TotpHelper.GetProvisioningUri("C&F Management System", accountName, secret);
            string qr = "data:image/png;base64," + Convert.ToBase64String(ZatcaQrHelper.GeneratePng(uri, 10));

            return Content(
                JsonConvert.SerializeObject(new
                {
                    errorCode = 200,
                    secret = secret,
                    otpauthUri = uri,
                    qr = qr
                }),
                "application/json");
        }

        [AllowAnonymous]
        public async Task<ContentResult> EnableTwoFactor([FromBody] TwoFactorCodeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.code))
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "Verification code is required." }),
                    "application/json");
            }

            string pendingSecret = _session.Pending2FASecret;
            if (string.IsNullOrWhiteSpace(pendingSecret))
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "2FA setup session expired. Please try again." }),
                    "application/json");
            }

            if (!TotpHelper.ValidateTotp(pendingSecret, request.code))
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "Invalid verification code." }),
                    "application/json");
            }

            int userId = _session.Pending2FAUserId > 0 ? _session.Pending2FAUserId : _session.LoginId;
            if (userId == 0)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "No active user found." }),
                    "application/json");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 404, errorMessage = "User not found." }),
                    "application/json");
            }

            var recoveryCodes = TotpHelper.GenerateRecoveryCodes();

            user.TwoFactorEnabled = true;
            user.TwoFactorSecretKey = pendingSecret;
            user.TwoFactorRecoveryCodes = string.Join(",", recoveryCodes);
            await _context.SaveChangesAsync();

            _session.Pending2FASecret = "";

            return Content(
                JsonConvert.SerializeObject(new
                {
                    errorCode = 200,
                    recoveryCodes = recoveryCodes
                }),
                "application/json");
        }

        [AllowAnonymous]
        public async Task<ContentResult> DisableTwoFactor([FromBody] TwoFactorCodeRequest request)
        {
            if (_session.LoginId == 0)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "Session expired." }),
                    "application/json");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.code))
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 400, errorMessage = "Verification code is required." }),
                    "application/json");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == _session.LoginId);
            if (user == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 404, errorMessage = "User not found." }),
                    "application/json");
            }

            bool valid = !string.IsNullOrWhiteSpace(user.TwoFactorSecretKey)
                && TotpHelper.ValidateTotp(user.TwoFactorSecretKey, request.code);

            if (!valid && !string.IsNullOrWhiteSpace(user.TwoFactorRecoveryCodes))
            {
                string stored = user.TwoFactorRecoveryCodes;
                valid = TotpHelper.TryConsumeRecoveryCode(ref stored, request.code);
                if (valid)
                {
                    user.TwoFactorRecoveryCodes = stored;
                }
            }

            if (!valid)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "Invalid verification code." }),
                    "application/json");
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorSecretKey = null;
            user.TwoFactorRecoveryCodes = null;
            await _context.SaveChangesAsync();

            return Content(
                JsonConvert.SerializeObject(new { errorCode = 200, errorMessage = "Two-factor authentication disabled." }),
                "application/json");
        }

        [AllowAnonymous]
        public async Task<ContentResult> TwoFactorStatus()
        {
            int userId = _session.Pending2FAUserId > 0 ? _session.Pending2FAUserId : _session.LoginId;
            if (userId == 0)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 401, errorMessage = "No active user found." }),
                    "application/json");
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Content(
                    JsonConvert.SerializeObject(new { errorCode = 404, errorMessage = "User not found." }),
                    "application/json");
            }

            var twoFactorSetting = await _context.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == "EnableTwoFactorAuth");

            return Content(
                JsonConvert.SerializeObject(new
                {
                    errorCode = 200,
                    enabled = user.TwoFactorEnabled == true,
                    globallyEnabled = twoFactorSetting?.IsActive == true
                }),
                "application/json");
        }
    }
}
