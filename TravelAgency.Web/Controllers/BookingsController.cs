using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Data;
using TravelAgency.Service.Interface;

namespace TravelAgency.Web.Controllers
{
    
    public class BookingsController : Controller
    {
        private readonly IBookingService _bookings;
        private readonly IPackageService _packages;
        private readonly ICustomerService _customers;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookingsController(
            IBookingService bookings,
            IPackageService packages,
            ICustomerService customers,
            UserManager<ApplicationUser> userManager
            )
        {
            _bookings = bookings;
            _packages = packages;
            _customers = customers;
            _userManager = userManager;
        }


        // GET: Bookings
        [AllowAnonymous]
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Index()
        {
            var data = _bookings.All()
                .OrderByDescending(b => b.CreatedAtUtc)
                .ToList();
            return View(data);
        }

        // GET: /Bookings/UserBookings
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> UserBookings(CancellationToken ct)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();
                email = await _userManager.GetEmailAsync(user);
            }

            if (string.IsNullOrWhiteSpace(email))
                return Forbid();

            email = email.Trim().ToLowerInvariant();

            var items = await _bookings.GetByCustomerEmailAsync(email, ct);
            return View(items); 
        }


        // GET: Bookings/Details/5
        [AllowAnonymous]
        public IActionResult Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = _bookings.Get(id.Value);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // GET: Bookings/Create
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Create()
        {
            PopulateSelectLists();
            return View();
        }

        // POST: Bookings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Create([Bind("PackageId,CustomerId,PeopleCount,TotalBasePrice,CreatedAtUtc,Status,Id")] Booking booking)
        {
            if (ModelState.IsValid)
            {
                if (booking.Id == Guid.Empty)
                {
                    booking.Id = Guid.NewGuid();
                }
                _bookings.Create(booking);
                TempData["Success"] = "Booking created.";
                return RedirectToAction(nameof(Index));
            }

            PopulateSelectLists(booking.CustomerId, booking.PackageId);
            return View(booking);
        }

        // GET: Bookings/Edit/5
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = _bookings.Get(id.Value);
            if (booking == null)
            {
                return NotFound();
            }

            PopulateSelectLists(booking.CustomerId, booking.PackageId);
            return View(booking);
        }

        // POST: Bookings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Edit(Guid id, [Bind("PackageId,CustomerId,PeopleCount,TotalBasePrice,CreatedAtUtc,Status,Id")] Booking booking)
        {
            if (id != booking.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _bookings.Update(booking);
                    TempData["Success"] = "Booking updated.";
                }
                catch
                {
                    if (_bookings.Get(booking.Id) == null)
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateSelectLists(booking.CustomerId, booking.PackageId);
            return View(booking);
        }

        // GET: Bookings/Delete/5
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = _bookings.Get(id.Value);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // POST: Bookings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Agent")]
        public IActionResult DeleteConfirmed(Guid id)
        {
            var booking = _bookings.Get(id);
            if (booking != null)
            {
                _bookings.Delete(booking);
                TempData["Success"] = "Booking deleted.";
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Bookings/ConvertTotal?id={bookingId}&to=MKD
        [AllowAnonymous]
        public async Task<IActionResult> ConvertTotal(Guid id, string to, CancellationToken ct)
        {
            var quote = await _bookings.ConvertTotalAsync(id, to, ct);
            if (quote == null)
            {
                return NotFound();
            }

            return View("PriceQuote", quote);
        }

        private void PopulateSelectLists(Guid? customerId = null, Guid? packageId = null)
        {
            var customerList = _customers.All()
                                         .OrderBy(c => c.Email)
                                         .Select(c => new { c.Id, c.Email })
                                         .ToList();
            var packageList = _packages.All()
                                       .OrderBy(p => p.Title)
                                       .Select(p => new { p.Id, p.Title })
                                       .ToList();

            ViewData["CustomerId"] = new SelectList(customerList, "Id", "Email", customerId);
            ViewData["PackageId"] = new SelectList(packageList, "Id", "Title", packageId);
        }
    }
}
