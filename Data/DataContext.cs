
using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<Brand> Brands => Set<Brand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(product => product.Sku)
                .IsUnique();

            entity.HasIndex(product => product.Slug)
                .IsUnique();

            entity.HasOne(product => product.Brand)
                .WithMany(brand => brand.Products)
                .HasForeignKey(product => product.BrandId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.Property(brand => brand.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(brand => brand.Slug)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(brand => brand.Description)
                .HasMaxLength(1000);

            entity.Property(brand => brand.LogoUrl)
                .HasMaxLength(2048);

            entity.HasIndex(brand => brand.Slug)
                .IsUnique();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(category => category.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(category => category.Slug)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(category => category.Description)
                .HasMaxLength(1000);

            entity.HasIndex(category => category.Slug)
                .IsUnique();

            entity.HasOne(category => category.ParentCategory)
                .WithMany(category => category.ChildCategories)
                .HasForeignKey(category => category.ParentCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(pc => new { pc.ProductId, pc.CategoryId });

            entity.HasOne(pc => pc.Product)
                .WithMany(product => product.ProductCategories)
                .HasForeignKey(pc => pc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Category)
                .WithMany(category => category.ProductCategories)
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
