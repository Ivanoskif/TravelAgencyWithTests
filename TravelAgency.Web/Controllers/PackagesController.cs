using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TravelAgency.Domain.Models;
using TravelAgency.Service.Interface;

namespace TravelAgency.Web.Controllers
{
    [Authorize(Roles = "Admin,Agent")]
    public class PackagesController : Controller
    {
        private readonly IPackageService _packages;
        private readonly IDestinationService _destinations;
        private readonly ILogger<PackagesController> _logger;

        public PackagesController(IPackageService packages, IDestinationService destinations, ILogger<PackagesController> logger)
        {
            _packages = packages;
            _destinations = destinations;
            _logger = logger;
        }


        // GET /Holidays
        [AllowAnonymous]
        public async Task<IActionResult> Holidays(Guid id, CancellationToken ct)
        {
            var list = await _packages.GetHolidaysAsync(id, ct);
            ViewBag.PackageId = id;
            return View("Holidays", list);
        }


        // GET: Packages/WeatherWindow/{id}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> WeatherWindow(Guid id, CancellationToken ct)
        {
            Console.WriteLine($"[WeatherWindow] id={id}");

            var dto = await _packages.GetWeatherWindowAsync(id, ct);

            if (dto == null)
            {
                Console.WriteLine("[WeatherWindow] dto == null → NotFound");
                return NotFound("No weather data. Check: package exists, Destination has Latitude/Longitude, dates are valid.");
            }

            return View(dto);
        }


        // GET: Packages/ConvertPrice/{id}?to=MKD
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConvertPrice(Guid id, string to = "EUR", CancellationToken ct = default)
        {
            var quote = await _packages.GetPriceQuoteAsync(id, to, ct);
            if (quote == null) return NotFound("No price quote available.");
            ViewBag.PackageId = id;
            return View(quote);
        }


        // GET: Packages
        // filters: ?destinationId=&from=yyyy-MM-dd&to=yyyy-MM-dd
        [AllowAnonymous]
        public IActionResult Index(Guid? destinationId, DateTime? from, DateTime? to)
        {
            var list = _packages.All();

            if (destinationId.HasValue)
            {
                list = list.Where(p => p.DestinationId == destinationId.Value);
            }

            if (from.HasValue && to.HasValue)
            {
                var df = DateOnly.FromDateTime(from.Value);
                var dt = DateOnly.FromDateTime(to.Value);
                list = list.Where(p => p.StartDate <= dt && p.EndDate >= df);
            }

            var destList = _destinations.All()
                                        .OrderBy(d => d.City)
                                        .Select(d => new { d.Id, Name = $"{d.City}, {d.CountryName}" })
                                        .ToList();
            ViewData["DestinationFilter"] = new SelectList(destList, "Id", "Name", destinationId);

            return View(list.OrderBy(p => p.StartDate).ToList());
        }

        // GET: Packages/Details/5
        [AllowAnonymous]
        public IActionResult Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var package = _packages.Get(id.Value);
            if (package == null)
            {
                return NotFound();
            }

            return View(package);
        }

        // GET: Packages/Create
        public IActionResult Create()
        {
            PopulateDestinations();
            return View();
        }

        // POST: Packages/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("DestinationId,Title,Description,BasePrice,StartDate,EndDate,AvailableSeats,Id")] Package package)
        {
            ModelState.Remove(nameof(Package.Destination));
            if (ModelState.IsValid)
            {
                if (package.Id == Guid.Empty)
                {
                    package.Id = Guid.NewGuid();
                }

                if (package.StartDate > package.EndDate)
                {
                    ModelState.AddModelError(nameof(Package.EndDate), "End date must be after start date.");
                    PopulateDestinations(package.DestinationId);
                    return View(package);
                }

                _packages.Create(package);
                TempData["Success"] = "Package created.";
                return RedirectToAction(nameof(Index));
            }

            PopulateDestinations(package.DestinationId);
            return View(package);
        }


        // GET: Packages/Edit/5
        public IActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var package = _packages.Get(id.Value);
            if (package == null)
            {
                return NotFound();
            }

            PopulateDestinations(package.DestinationId);
            return View(package);
        }

        // POST: Packages/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Guid id, [Bind("DestinationId,Title,Description,BasePrice,StartDate,EndDate,AvailableSeats,Id")] Package package)
        {
            if (id != package.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (package.StartDate > package.EndDate)
                {
                    ModelState.AddModelError(nameof(Package.EndDate), "End date must be after start date.");
                    PopulateDestinations(package.DestinationId);
                    return View(package);
                }

                try
                {
                    _packages.Update(package);
                    TempData["Success"] = "Package updated.";
                }
                catch
                {
                    if (_packages.Get(package.Id) == null)
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateDestinations(package.DestinationId);
            return View(package);
        }

        // GET: Packages/Delete/5
        public IActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var package = _packages.Get(id.Value);
            if (package == null)
            {
                return NotFound();
            }

            return View(package);
        }

        // POST: Packages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(Guid id)
        {
            var package = _packages.Get(id);
            if (package != null)
            {
                _packages.Delete(package);
                TempData["Success"] = "Package deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PackageExists(Guid id)
        {
            return _packages.Get(id) != null;
        }



        // GET: Packages/RemainingSeats/{id}
        [AllowAnonymous]
        public IActionResult RemainingSeats(Guid id)
        {
            var remain = _packages.RemainingSeats(id);
            ViewBag.PackageId = id;
            ViewBag.Remaining = remain;
            return View("RemainingSeats");
        }

        private void PopulateDestinations(Guid? selectedId = null)
        {
            var list = _destinations.All()
                                    .OrderBy(d => d.City)
                                    .Select(d => new
                                    {
                                        d.Id,
                                        Name = $"{d.City}, {d.CountryName}"
                                    })
                                    .ToList();
            ViewData["DestinationId"] = new SelectList(list, "Id", "Name", selectedId);
        }
    }
}
