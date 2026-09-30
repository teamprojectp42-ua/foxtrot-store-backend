using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data
{
    public class DataContext(
        DbContextOptions<DataContext> options
        ) : DbContext(options)
    {
        public DbSet<Entities.Product> Products => Set<Entities.Product>();
    }
}
