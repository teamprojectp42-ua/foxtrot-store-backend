using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data
{
    public class DataContext(
        DbContextOptions<DataContext> options
        ) : DbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(product => product.Sku)
                    .IsUnique();

                entity.HasIndex(product => product.Slug)
                    .IsUnique();
            });
        }
    }
}
