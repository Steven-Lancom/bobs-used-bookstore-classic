using Microsoft.EntityFrameworkCore;

namespace Bookstore.Data
{
    /// <summary>
    /// Replaces the EF6 DropCreateDatabaseIfModelChanges initializer.
    /// Call EnsureCreatedAsync from the host project's startup to create the database
    /// and apply HasData seed data defined in ApplicationDbContext.OnModelCreating.
    /// </summary>
    public static class BookstoreDbInitializer
    {
        public static async Task EnsureCreatedAsync(ApplicationDbContext context)
        {
            await context.Database.EnsureCreatedAsync();
        }
    }
}
