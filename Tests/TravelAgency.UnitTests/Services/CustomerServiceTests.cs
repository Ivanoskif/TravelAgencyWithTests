using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Implementation;
using Xunit;

namespace TravelAgency.UnitTests.Services
{
    public class CustomerServiceTests
    {
        private readonly IRepository<Customer> _repo;
        private readonly CustomerService _sut;

        public CustomerServiceTests()
        {
            _repo = Substitute.For<IRepository<Customer>>();
            _sut = new CustomerService(_repo);
        }

        [Fact]
        public void Create_ShouldInsertAndReturnEntity()
        {
            var c = new Customer { Id = Guid.NewGuid(), FirstName = "Ana", LastName = "Markovska", Email = "ana@test.mk" };
            _repo.Insert(c).Returns(c);

            var result = _sut.Create(c);

            result.Should().BeSameAs(c);
            _repo.Received(1).Insert(c);
        }

        [Fact]
        public void Update_ShouldCallRepoUpdate()
        {
            var c = new Customer { Id = Guid.NewGuid(), FirstName = "Ivan", LastName = "I.", Email = "ivan@test.mk" };
            _repo.Update(c).Returns(c);

            var result = _sut.Update(c);

            result.Should().BeSameAs(c);
            _repo.Received(1).Update(c);
        }

        [Fact]
        public void Delete_ShouldCallRepoDelete()
        {
            var c = new Customer { Id = Guid.NewGuid(), FirstName = "Maja", LastName = "M.", Email = "maja@test.mk" };
            _repo.Delete(c).Returns(c);

            var result = _sut.Delete(c);

            result.Should().BeSameAs(c);
            _repo.Received(1).Delete(c);
        }

        [Fact]
        public void Get_ShouldFetchById()
        {
            var id = Guid.NewGuid();
            var found = new Customer { Id = id, FirstName = "Boris", LastName = "B.", Email = "boris@test.mk" };

            _repo.Get<Customer>(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                null, null
            ).Returns(found);

            var result = _sut.Get(id);

            result.Should().Be(found);
            _repo.Received(1).Get(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                null, null
            );
        }

        [Fact]
        public void All_ShouldReturnAllCustomers()
        {
            var list = new List<Customer>
            {
                new() { Id = Guid.NewGuid(), FirstName = "Ana",  LastName = "A", Email = "ana@test.mk" },
                new() { Id = Guid.NewGuid(), FirstName = "Marko",LastName = "M", Email = "marko@test.mk" }
            };

            _repo.GetAll(Arg.Any<Expression<Func<Customer, Customer>>>(), null, null, null)
                 .Returns(list);

            var result = _sut.All().ToList();

            result.Should().HaveCount(2);
            result.Select(x => x.Email).Should().Contain(new[] { "ana@test.mk", "marko@test.mk" });
        }

        [Fact]
        public void GetByEmail_ShouldUseExactMatch()
        {
            var email = "exact@test.mk";
            var c = new Customer { Id = Guid.NewGuid(), FirstName = "Exact", LastName = "Match", Email = email };

            _repo.Get<Customer>(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                null, null
            ).Returns(c);

            var result = _sut.GetByEmail(email);

            result.Should().NotBeNull();
            result!.Email.Should().Be(email);
        }

        [Fact]
        public void GetByEmail_IsCaseSensitive_AsImplemented()
        {
            _repo.Get<Customer>(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                null, null
            ).Returns((Customer?)null);

            var result = _sut.GetByEmail("UPPER@Test.mk");

            result.Should().BeNull();
        }

        [Fact]
        public void Search_WithNullOrWhitespace_ShouldReturnAll()
        {
            var all = new List<Customer>
            {
                new() { Id = Guid.NewGuid(), FirstName = "Ana", LastName = "K", Email = "ana@test.mk" }
            };

            _repo.GetAll(Arg.Any<Expression<Func<Customer, Customer>>>(), null, null, null)
                 .Returns(all);

            var result1 = _sut.Search(null).ToList();
            var result2 = _sut.Search("").ToList();
            var result3 = _sut.Search("   ").ToList();

            result1.Should().BeEquivalentTo(all);
            result2.Should().BeEquivalentTo(all);
            result3.Should().BeEquivalentTo(all);

            _repo.Received(3).GetAll(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                null, null, null);
        }

        [Theory]
        [InlineData("ana", 1)]
        [InlineData("TEST.MK", 3)]   
        [InlineData("petrov", 1)]    
        public void Search_ShouldFilter_ByEmailOrFirstOrLastName(string term, int expectedCount)
        {
            // Податоци во меморија
            var data = new List<Customer>
            {
                new() { Id = Guid.NewGuid(), FirstName = "Ana",   LastName = "Kostovska", Email = "ana@test.mk" },
                new() { Id = Guid.NewGuid(), FirstName = "Marko", LastName = "Petrov",    Email = "marko@test.mk" },
                new() { Id = Guid.NewGuid(), FirstName = "Elena", LastName = "Georgieva", Email = "elena@TEST.MK" }
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Customer, Customer>>>(0);
                var predicate = ci.ArgAt<Expression<Func<Customer, bool>>>(1);

                var filter = predicate.Compile();
                var projected = data.Where(filter).Select(selector.Compile()).ToList();
                return projected;
            });

            var result = _sut.Search(term).ToList();

            result.Should().HaveCount(expectedCount);
        }
    }
}
