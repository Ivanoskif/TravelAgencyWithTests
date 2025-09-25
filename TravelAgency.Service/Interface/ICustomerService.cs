using System;
using System.Collections.Generic;
using TravelAgency.Domain.Models;

namespace TravelAgency.Service.Interface
{
    public interface ICustomerService
    {
        
        Customer Create(Customer c);
        Customer Update(Customer c);
        Customer Delete(Customer c);
        Customer? Get(Guid id);
        IEnumerable<Customer> All();

        
        Customer? GetByEmail(string email);
        IEnumerable<Customer> Search(string? nameOrEmail);
    }
}
