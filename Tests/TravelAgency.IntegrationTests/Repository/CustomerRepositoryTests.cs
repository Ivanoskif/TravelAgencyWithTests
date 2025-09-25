using System;
using System.Linq;
using FluentAssertions;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Implementation;
using TravelAgency.Repository.Interface;
using TravelAgency.IntegrationTests.Infrastructure;
using Xunit;

namespace TravelAgency.IntegrationTests.Repositories
{
    public class CustomerRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryDb _db = default!;
        private IRepository<Customer> _repo = default!;

        public async Task InitializeAsync()
        {
            _db = new SqliteInMemoryDb();
            _repo = new Repository<Customer>(_db.Context);
            await Task.CompletedTask;
        }

        public async Task DisposeAsync() => await _db.DisposeAsync();

        private static Customer NewCustomer(string email = "john@test.mk") => new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = email,
            Phone = "070123456"
        };

        [Fact]
        public void Insert_Get_Update_Delete_Customer()
        {
            var c = _repo.Insert(NewCustomer());
            c.Id.Should().NotBeEmpty();

            var fromDb = _repo.Get(x => x, x => x.Id == c.Id);
            fromDb.Should().NotBeNull();
            fromDb!.Email.Should().Be("john@test.mk");

            c.LastName = "Doev";
            _repo.Update(c);

            _db.Context.Customers.Single(x => x.Id == c.Id).LastName.Should().Be("Doev");

            _repo.Delete(c);
            _db.Context.Customers.Any(x => x.Id == c.Id).Should().BeFalse();
        }

        [Fact]
        public void GetAll_FilterByEmailDomain_Projection()
        {
            _repo.Insert(NewCustomer("a@site.com"));
            _repo.Insert(NewCustomer("b@site.com"));
            _repo.Insert(NewCustomer("c@other.com"));

            var emails = _repo.GetAll(
                selector: x => x.Email,
                predicate: x => x.Email.EndsWith("@site.com"),
                orderBy: q => q.OrderBy(x => x.Email),
                include: null
            ).ToList();

            emails.Should().HaveCount(2).And.ContainInOrder("a@site.com", "b@site.com");
        }
    }
}
