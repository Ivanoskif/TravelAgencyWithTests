using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelAgency.Domain.Models;
using TravelAgency.Service.Interface;

namespace TravelAgency.Web.Controllers
{
    [Authorize(Roles = "Admin,Agent")]
    public class DestinationsController : Controller
    {
        private readonly IDestinationService _destinations;

        public DestinationsController(IDestinationService destinations)
        {
            _destinations = destinations;
        }

        // POST: Destinations/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Import()
        {
            var count = await _destinations.ImportCountriesAsync();
            TempData["Message"] = $"{count} destinations imported from API.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Destinations
        // filter country/city (query-string ?country=...&city=...)
        [AllowAnonymous]
        public IActionResult Index(string? country, string? city)
        {
            var list = _destinations.Find(country, city).ToList();
            ViewBag.Country = country;
            ViewBag.City = city;
            return View(list);
        }

        // GET: Destinations/Details/5
        [AllowAnonymous]
        public IActionResult Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var destination = _destinations.Get(id.Value);
            if (destination == null)
            {
                return NotFound();
            }

            return View(destination);
        }

        // GET: Destinations/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Destinations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("CountryName,City,Latitude,Longitude,IsoCode,DefaultCurrency,Id")] Destination destination)
        {
            if (ModelState.IsValid)
            {
                if (destination.Id == Guid.Empty)
                {
                    destination.Id = Guid.NewGuid();
                }
                _destinations.Create(destination);
                TempData["Success"] = "Destination created.";
                return RedirectToAction(nameof(Index));
            }
            return View(destination);
        }

        // GET: Destinations/Edit/5
        public IActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var destination = _destinations.Get(id.Value);
            if (destination == null)
            {
                return NotFound();
            }
            return View(destination);
        }

        // POST: Destinations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Guid id, [Bind("CountryName,City,Latitude,Longitude,IsoCode,DefaultCurrency,Id")] Destination destination)
        {
            if (id != destination.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _destinations.Update(destination);
                    TempData["Success"] = "Destination updated.";
                }
                catch
                {
                    if (_destinations.Get(destination.Id) == null)
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(destination);
        }


        // GET: Destinations/Delete/5
        public IActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var destination = _destinations.Get(id.Value);
            if (destination == null)
            {
                return NotFound();
            }

            return View(destination);
        }

        // POST: Destinations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(Guid id)
        {
            var destination = _destinations.Get(id);
            if (destination != null)
            {
                _destinations.Delete(destination);
                TempData["Success"] = "Destination deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

         // GET: Destinations/CountrySnapshot/{id}
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CountrySnapshot(Guid id, CancellationToken ct)
        {
            var dest = _destinations.Get(id);
            if (dest == null)
            {
                return NotFound();
            }

            var dto = await _destinations.GetCountrySnapshotAsync(id, ct);
            if (dto == null)
            {
                return NotFound();
            }

            return View("CountrySnapshot", dto);
        }
    }
}
