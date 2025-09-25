using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Data;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Interface;
using TravelAgency.Web.Infrastructure;
using TravelAgency.Web.Models;

namespace TravelAgency.Web.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly IRepository<Package> _packRepo;
        private readonly IPackageService _packageService;
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepository<Customer> _customerRepo;

        public CartController(
            IRepository<Package> packRepo,
            IPackageService packageService,
            IBookingService bookingService,
            UserManager<ApplicationUser> userManager,
            IRepository<Customer> customerRepo)
        {
            _packRepo = packRepo;
            _packageService = packageService;
            _bookingService = bookingService;
            _userManager = userManager;
            _customerRepo = customerRepo;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            var items = HttpContext.Session.GetObject<List<CartItem>>(SessionKeys.Cart) ?? new();
            ViewBag.Total = items.Sum(i => i.Subtotal);
            return View(items);
        }

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Add(Guid packageId, int people = 1)
        {
            var pkg = _packRepo.Get(x => x, x => x.Id == packageId);
            if (pkg == null) return NotFound();

            var cart = HttpContext.Session.GetObject<List<CartItem>>(SessionKeys.Cart) ?? new();
            var existing = cart.FirstOrDefault(c => c.PackageId == packageId);
            if (existing == null)
            {
                cart.Add(new CartItem
                {
                    PackageId = pkg.Id,
                    Title = pkg.Title,
                    PeopleCount = Math.Max(1, people),
                    UnitPrice = pkg.BasePrice
                });
            }
            else
            {
                existing.PeopleCount += Math.Max(1, people);
            }

            HttpContext.Session.SetObject(SessionKeys.Cart, cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(Guid packageId)
        {
            var cart = HttpContext.Session.GetObject<List<CartItem>>(SessionKeys.Cart) ?? new();
            cart.RemoveAll(c => c.PackageId == packageId);
            HttpContext.Session.SetObject(SessionKeys.Cart, cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CancellationToken ct)
        {
            var cart = HttpContext.Session.GetObject<List<CartItem>>(SessionKeys.Cart) ?? new();
            if (cart.Count == 0)
            {
                TempData["Err"] = "Cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var customer = _customerRepo.Get(c => c, c => c.Email == user.Email);
            if (customer == null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = user.Email ?? "N/A",
                    LastName = "",
                    Email = user.Email ?? ""
                };
                _customerRepo.Insert(customer);
            }
            var customerId = customer.Id;

            foreach (var item in cart)
            {
                var remaining = await _packageService.GetRemainingSeatsAsync(item.PackageId, ct);
                if (item.PeopleCount > remaining)
                {
                    TempData["Err"] = $"Not enough seats for \"{item.Title}\". Remaining: {remaining}.";
                    return RedirectToAction(nameof(Index));
                }
            }


            foreach (var item in cart)
            {
                var (ok, error) = await _bookingService.CreateBookingAsync(
                    customerId, item.PackageId, item.PeopleCount, ct);

                if (!ok)
                {
                    TempData["Err"] = $"Failed to book \"{item.Title}\": {error}";
                    return RedirectToAction(nameof(Index));
                }
            }

            HttpContext.Session.Remove(SessionKeys.Cart);
            TempData["Ok"] = "Booking(s) completed. Thank you!";
            return RedirectToAction(nameof(Index));
        }
    }
}
