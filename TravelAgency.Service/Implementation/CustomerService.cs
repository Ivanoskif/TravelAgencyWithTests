using System;
using System.Collections.Generic;
using System.Linq;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Interface;

namespace TravelAgency.Service.Implementation
{
    public class CustomerService : ICustomerService
    {
        private readonly IRepository<Customer> _repo;

        public CustomerService(IRepository<Customer> repo)
        {
            _repo = repo;
        }

        public Customer Create(Customer c)
        {
            return _repo.Insert(c);
        }

        public Customer Update(Customer c)
        {
            return _repo.Update(c);
        }

        public Customer Delete(Customer c)
        {
            return _repo.Delete(c);
        }

        public Customer? Get(Guid id)
        {
            return _repo.Get(x => x, x => x.Id == id);
        }

        public IEnumerable<Customer> All()
        {
            return _repo.GetAll(x => x);
        }

        public Customer? GetByEmail(string email)
        {
            return _repo.Get(x => x, x => x.Email == email);
        }

        public IEnumerable<Customer> Search(string? nameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(nameOrEmail))
            {
                return All();
            }

            var term = nameOrEmail.Trim().ToLowerInvariant();
            return _repo.GetAll(x => x,
                predicate: x =>
                    x.Email.ToLower().Contains(term) ||
                    x.FirstName.ToLower().Contains(term) ||
                    x.LastName.ToLower().Contains(term));
        }
    }
}
