using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sum.Data;
using sum.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace sum.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IDataProtector _protector;

        public class ActiveAccountDto
        {
            public int UserId { get; set; }
            public string Email { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
        }

        public AccountController(AppDbContext db, IDataProtectionProvider provider)
        {
            _db = db;
            _protector = provider.CreateProtector("sum.AccountController.ActiveAccounts");
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        private List<ActiveAccountDto> GetActiveAccounts()
        {
            var cookie = HttpContext.Request.Cookies["ActiveAccounts"];
            if (string.IsNullOrEmpty(cookie)) return new List<ActiveAccountDto>();

            try
            {
                var decrypted = _protector.Unprotect(cookie);
                return JsonSerializer.Deserialize<List<ActiveAccountDto>>(decrypted) ?? new List<ActiveAccountDto>();
            }
            catch
            {
                return new List<ActiveAccountDto>();
            }
        }

        private void SaveActiveAccounts(List<ActiveAccountDto> accounts)
        {
            var json = JsonSerializer.Serialize(accounts);
            var encrypted = _protector.Protect(json);

            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            };

            HttpContext.Response.Cookies.Append("ActiveAccounts", encrypted, options);
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Neplatný e-mail nebo heslo.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName ?? user.Email),
                new Claim("UserId", user.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // Save to active accounts list
            var activeAccounts = GetActiveAccounts();
            if (!activeAccounts.Any(a => a.UserId == user.Id))
            {
                activeAccounts.Add(new ActiveAccountDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName ?? user.Email,
                    Role = user.Role
                });
                SaveActiveAccounts(activeAccounts);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Tento e-mail je již registrovaný.");
                return View(model);
            }

            // Derive username from email (part before @)
            var username = model.Email.Split('@')[0];

            var user = new User
            {
                Username = username,
                Email = model.Email,
                FullName = null,
                PasswordHash = HashPassword(model.Password),
                Role = "Student",
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registrace proběhla úspěšně! Nyní se můžete přihlásit.";
            return RedirectToAction("Login");
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var claim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(claim, out var currentUserId))
            {
                var activeAccounts = GetActiveAccounts();
                var current = activeAccounts.FirstOrDefault(a => a.UserId == currentUserId);
                if (current != null)
                {
                    activeAccounts.Remove(current);
                    SaveActiveAccounts(activeAccounts);
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // If there are other active accounts, switch to the first one
                if (activeAccounts.Any())
                {
                    var nextAcc = activeAccounts.First();
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == nextAcc.UserId);
                    if (user != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.Email),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, user.Role),
                            new Claim("FullName", user.FullName ?? user.Email),
                            new Claim("UserId", user.Id.ToString())
                        };

                        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            principal,
                            new AuthenticationProperties
                            {
                                IsPersistent = false,
                                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                            });

                        return RedirectToAction("Index", "Home");
                    }
                }
            }
            else
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return RedirectToAction("Login");
        }

        // POST: /Account/SwitchAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchAccount(int userId)
        {
            var activeAccounts = GetActiveAccounts();
            var targetAccount = activeAccounts.FirstOrDefault(a => a.UserId == userId);
            if (targetAccount == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                activeAccounts.Remove(targetAccount);
                SaveActiveAccounts(activeAccounts);
                return RedirectToAction("Login");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName ?? user.Email),
                new Claim("UserId", user.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/LogoutAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutAll()
        {
            HttpContext.Response.Cookies.Delete("ActiveAccounts");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: /Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login");

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return RedirectToAction("Login");

            return View(user);
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // --- Password helpers ---
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}
