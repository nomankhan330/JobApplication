using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using BusinessLogic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using BusinessLogic.Helper;
using Microsoft.AspNetCore.Http;

namespace JobApplication.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IAccount _account;
        private readonly ISessionHelper _session;

        public AccountController(IAccount account, ISessionHelper session) : base(session)
        {
            _account = account;
            _session = session;
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

            //var ids = model.CostCenterIds;

            // Handle Photo Upload
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
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

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult CostCenter()
        {
            return View();
        }

        public ContentResult logout()
        {
            _session.Logout();
            return Content(JsonConvert.SerializeObject("000"), "application/json");
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

            if (PhotoFile != null && PhotoFile.Length > 0)
            {
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
    }
}
