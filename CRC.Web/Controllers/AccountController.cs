using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using CRC.Web.Data;

namespace CRC.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;

        public AccountController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // DTO (request model) for registration
        public class RegisterUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public int UserType { get; set; } = 1; // 1 = Administrator
        }

        // POST: /Account/RegisterUser (called via JS)
        [HttpPost]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { success = false, message = "Please fill in all required fields." });
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@User_Name", model.Name),
                    new SqlParameter("@Username", model.Username),
                    new SqlParameter("@User_Email", model.Email),
                    new SqlParameter("@Password", model.Password),
                    new SqlParameter("@User_Type", model.UserType)
                };

                await _db.ExecuteNonQueryAsync("spUsers_Register", parameters);

                return Ok(new { success = true, message = "User registered successfully." });
            }
            catch (SqlException ex)
            {
                // This will include "Username already exists." from RAISERROR
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            // If already authenticated, go straight to Dashboard
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        // DTO for login
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequest model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                ViewData["LoginError"] = "Please enter username and password.";
                return View();
            }

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@Username", model.Username),
            new SqlParameter("@Password", model.Password)
        };

                var dt = await _db.ExecuteDataTableAsync("spUsers_ValidateLogin", parameters);

                if (dt.Rows.Count == 0)
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                var row = dt.Rows[0];

                var userId = row["User_ID"].ToString() ?? string.Empty;
                var userName = row["User_Name"].ToString() ?? string.Empty;
                var username = row["Username"].ToString() ?? string.Empty;
                var userEmail = row["User_Email"].ToString() ?? string.Empty;
                var userType = row["User_Type"].ToString() ?? "1";

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("FullName", userName),
            new Claim("UserEmail", userEmail),
            new Claim("UserType", userType)
        };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // after successful login, go to Dashboard
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception)
            {
                ViewData["LoginError"] = "An unexpected error occurred.";
                return View();
            }
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}