using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelAgency.Domain.Models;
using TravelAgency.Service.Interface;

namespace TravelAgency.Web.Controllers
{
    [Authorize(Roles = "Admin,Agent")]
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customers;

        public CustomersController(ICustomerService customers)
        {
            _customers = customers;
        }

        // GET: Customers
        [AllowAnonymous]
        public IActionResult Index(string? q)
        {
            var data = string.IsNullOrWhiteSpace(q)
                ? _customers.All()
                : _customers.Search(q);

            ViewBag.Query = q;
            return View(data.ToList());
        }

        // GET: Customers/Details/5
        [AllowAnonymous]
        public IActionResult Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = _customers.Get(id.Value);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("FirstName,LastName,Email,Phone,Id")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                if (customer.Id == Guid.Empty)
                {
                    customer.Id = Guid.NewGuid();
                }

                var exists = _customers.GetByEmail(customer.Email);
                if (exists != null)
                {
                    ModelState.AddModelError(nameof(Customer.Email), "Email already exists.");
                    return View(customer);
                }

                _customers.Create(customer);
                TempData["Success"] = "Customer created.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // GET: Customers/Edit/5
        public IActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = _customers.Get(id.Value);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Guid id, [Bind("FirstName,LastName,Email,Phone,Id")] Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var other = _customers.GetByEmail(customer.Email);
                if (other != null && other.Id != customer.Id)
                {
                    ModelState.AddModelError(nameof(Customer.Email), "Email already in use by another customer.");
                    return View(customer);
                }

                _customers.Update(customer);
                TempData["Success"] = "Customer updated.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // GET: Customers/Delete/5
        public IActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = _customers.Get(id.Value);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(Guid id)
        {
            var customer = _customers.Get(id);
            if (customer != null)
            {
                _customers.Delete(customer);
                TempData["Success"] = "Customer deleted.";
            }

            return RedirectToAction(nameof(Index));
        }


        // GET: Customers/GetByEmail?email=...
        [AllowAnonymous]
        public IActionResult GetByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest();
            }

            var c = _customers.GetByEmail(email);
            if (c == null)
            {
                return NotFound();
            }

            return View("Details", c);
        }

        [AllowAnonymous]
        public IActionResult Search(string q)
        {
            var list = _customers.Search(q).ToList();
            ViewBag.Query = q;
            return View("Index", list);
        }
    }
}
