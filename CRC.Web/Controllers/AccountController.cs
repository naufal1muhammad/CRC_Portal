using CRC.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace CRC.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        public AccountController(DatabaseHelper db)
        {
            _db = db;
        }

        // -------------------------
        // SUPERUSER ONLY: Register
        // -------------------------

        // GET: /Account/Register
        [Authorize(Policy = "SuperUserOnly")]
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

            // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF
            public int UserType { get; set; } = 3;
        }

        // POST: /Account/RegisterUser (called via JS)
        [Authorize(Policy = "SuperUserOnly")]
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
                var username = model.Username.Trim();
                var passwordHash = _hasher.HashPassword(username, model.Password);

                var parameters = new[]
                {
    new SqlParameter("@User_Name", model.Name.Trim()),
    new SqlParameter("@Username", username),
    new SqlParameter("@User_Email", model.Email.Trim()),
    new SqlParameter("@PasswordHash", passwordHash),
    new SqlParameter("@User_Type", model.UserType)
};

                await _db.ExecuteNonQueryAsync("spUsers_Register", parameters);

                return Ok(new { success = true, message = "User registered successfully." });
            }
            catch (SqlException ex)
            {
                // includes RAISERROR from sproc
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // -------------------------
        // ANONYMOUS: Login
        // -------------------------

        // GET: /Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        private IActionResult RedirectToLanding(ClaimsPrincipal principal)
        {
            if (principal.IsInRole("SUPERUSER"))
                return RedirectToAction("Index", "Dashboard");

            if (principal.IsInRole("ADMIN"))
                return RedirectToAction("Index", "Appointment");

            if (principal.IsInRole("STAFF"))
                return RedirectToAction("AccessDenied", "Account"); // later: Staff landing

            return RedirectToAction("AccessDenied", "Account");
        }

        // DTO for login
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // POST: /Account/Login
        [AllowAnonymous]
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
                var usernameInput = model.Username.Trim();

                var dt = await _db.ExecuteDataTableAsync(
                    "spUsers_ValidateLogin",
                    new[] { new SqlParameter("@Username", usernameInput) }
                );

                if (dt.Rows.Count == 0)
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                var row = dt.Rows[0];

                var userId = row["User_ID"]?.ToString() ?? string.Empty;
                var userName = row["User_Name"]?.ToString() ?? string.Empty;
                var username = row["Username"]?.ToString() ?? string.Empty;
                var userEmail = row["User_Email"]?.ToString() ?? string.Empty;
                var userType = row["User_Type"]?.ToString() ?? "3";

                var storedHash = row["PasswordHash"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(storedHash))
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                var verify = _hasher.VerifyHashedPassword(username, storedHash, model.Password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                // -----------------------------
                // Claims + Sign-in (same as yours)
                // -----------------------------
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("FullName", userName),
            new Claim("UserEmail", userEmail),
            new Claim("UserType", userType)
        };

                var ut = userType.Trim();

                if (ut == "1") // SUPERUSER
                {
                    claims.Add(new Claim(ClaimTypes.Role, "SUPERUSER"));
                }
                else if (ut == "2") // ADMIN
                {
                    claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, "STAFF"));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = false }
                );

                return RedirectToLanding(principal);
            }
            catch (Exception ex)
            {
                ViewData["LoginError"] = ex.Message;
                return View();
            }
        }

        // -------------------------
        // Access Denied page
        // -------------------------
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // -------------------------
        // Logout
        // -------------------------
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}