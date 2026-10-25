using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataContext(DbContextOptions<DataContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpecification> ProductSpecifications
        => Set<ProductSpecification>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

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

            // Изображение категории
            entity.Property(category => category.ImageUrl)
                .HasMaxLength(2048);

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

            entity.HasIndex(pc => pc.CategoryId);

            entity.HasOne(pc => pc.Product)
                .WithMany(product => product.ProductCategories)
                .HasForeignKey(pc => pc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Category)
                .WithMany(category => category.ProductCategories)
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.Property(image => image.ImageUrl)
                .HasMaxLength(2048)
                .IsRequired();

            entity.HasIndex(image => new
            {
                image.ProductId,
                image.SortOrder
            });

            entity.HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductSpecification>(entity =>
        {
            entity.Property(specification => specification.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(specification => specification.Value)
                .HasMaxLength(1000)
                .IsRequired();

            entity.HasIndex(specification => new
            {
                specification.ProductId,
                specification.SortOrder
            });

            entity.HasOne(specification => specification.Product)
                .WithMany(product => product.Specifications)
                .HasForeignKey(specification => specification.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(specification => new
            {
                specification.ProductId,
                specification.Name
            }).IsUnique();
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.Slug)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(p => p.Slug)
                .IsUnique();

            entity.Property(p => p.Description)
                .HasMaxLength(2000);

            entity.Property(p => p.BannerImageUrl)
                .HasMaxLength(2048);

            entity.Property(p => p.ImageUrl)
                .HasMaxLength(2048);
        });
    }
}
