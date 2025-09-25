using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Repository.Data;

namespace TravelAgency.IntegrationTests.Infrastructure
{
    public sealed class SqliteInMemoryDb : IAsyncDisposable
    {
        public AppDbContext Context { get; }
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;

        public SqliteInMemoryDb()
        {
            _conn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            _conn.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_conn)
                .Options;

            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }
}
