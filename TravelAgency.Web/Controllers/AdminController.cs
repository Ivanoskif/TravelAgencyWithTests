using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TravelAgency.Repository.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace TravelAgency.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Index(string? email = null)
        {
            ViewBag.Email = email ?? string.Empty;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Find(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Err"] = "Внеси email.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                TempData["Err"] = $"User with this email dose not exist: {email}";
                return RedirectToAction(nameof(Index), new { email });
            }

            var roles = await _userManager.GetRolesAsync(user);
            TempData["UserId"] = user.Id.ToString();
            TempData["Email"] = user.Email!;
            TempData["Roles"] = string.Join(", ", roles);
            return RedirectToAction(nameof(Index), new { email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string email, string role)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                TempData["Err"] = "Email and Role are required.";
                return RedirectToAction(nameof(Index), new { email });
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                TempData["Err"] = $"User with this email dose not exist: {email}";
                return RedirectToAction(nameof(Index), new { email });
            }

            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }

            var res = await _userManager.AddToRoleAsync(user, role);
            TempData[res.Succeeded ? "Ok" : "Err"] =
                res.Succeeded ? $"Role: '{role}' is set to: {email}."
                              : string.Join(" | ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index), new { email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string email, string role)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                TempData["Err"] = "Email and Role are required.";
                return RedirectToAction(nameof(Index), new { email });
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                TempData["Err"] = $"User with this email dose not exist: {email}";
                return RedirectToAction(nameof(Index), new { email });
            }

            var res = await _userManager.RemoveFromRoleAsync(user, role);
            TempData[res.Succeeded ? "Ok" : "Err"] =
                res.Succeeded ? $"Role: '{role}' was removed for: {email}."
                              : string.Join(" | ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index), new { email });
        }
    }
}
