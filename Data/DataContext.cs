using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataContext(DbContextOptions<DataContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpecification> ProductSpecifications
        => Set<ProductSpecification>();
    
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionProduct> PromotionProducts
        => Set<PromotionProduct>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

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

            entity.Property(product => product.Price)
                .HasPrecision(18, 2);
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

            entity.Property(promotion => promotion.DiscountType)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(promotion => promotion.DiscountValue)
                .HasPrecision(18, 2);

            entity.Property(promotion => promotion.CardImageUrl)
                .HasMaxLength(2048);
        });
        modelBuilder.Entity<PromotionProduct>(entity =>
        {
            entity.HasKey(pp => new
            {
                pp.PromotionId,
                pp.ProductId
            });

            entity.HasIndex(pp => pp.ProductId);

            entity.HasOne(pp => pp.Promotion)
                .WithMany(promotion => promotion.PromotionProducts)
                .HasForeignKey(pp => pp.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pp => pp.Product)
                .WithMany(product => product.PromotionProducts)
                .HasForeignKey(pp => pp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(cart => cart.Id);

            entity.Property(cart => cart.UserId)
                .HasMaxLength(255)
                .IsRequired();

            // A user can have only one cart.
            entity.HasIndex(cart => cart.UserId)
                .IsUnique();

            // Deleting a user also deletes their cart.
            entity.HasOne(cart => cart.User)
                .WithOne()
                .HasForeignKey<Cart>(cart => cart.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Quantity)
                .IsRequired();

            // Prevent duplicate products inside one cart.
            entity.HasIndex(item => new
            {
                item.CartId,
                item.ProductId
            })
            .IsUnique();

            // Useful for finding carts containing a product.
            entity.HasIndex(item => item.ProductId);

            // Quantity must always be positive.
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_CartItems_Quantity_Positive",
                    "`Quantity` > 0");
            });

            // Deleting a cart also deletes its items.
            entity.HasOne(item => item.Cart)
                .WithMany(cart => cart.Items)
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // A product cannot be physically deleted while
            // it is referenced by a cart item.
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);

            entity.Property(order => order.UserId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(order => order.CustomerName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(order => order.PhoneNumber)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(order => order.City)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(order => order.DeliveryAddress)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(order => order.Comment)
                .HasMaxLength(1000);

            entity.Property(order => order.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(order => order.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(order => new
            {
                order.UserId,
                order.CreatedAt
            });

            entity.HasIndex(order => order.Status);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Orders_TotalAmount_NonNegative",
                    "`TotalAmount` >= 0");
            });

            // Keep order history when a user account is deleted.
            entity.HasOne(order => order.User)
                .WithMany()
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting an order also deletes its items.
            entity.HasMany(order => order.Items)
                .WithOne(item => item.Order)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.ProductName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(item => item.ProductSku)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(item => item.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(item => item.Quantity)
                .IsRequired();

            entity.Property(item => item.TotalPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            // A product can appear only once in an order.
            entity.HasIndex(item => new
            {
                item.OrderId,
                item.ProductId
            })
            .IsUnique();

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderItems_Quantity_Positive",
                    "`Quantity` > 0");

                table.HasCheckConstraint(
                    "CK_OrderItems_UnitPrice_NonNegative",
                    "`UnitPrice` >= 0");

                table.HasCheckConstraint(
                    "CK_OrderItems_TotalPrice_NonNegative",
                    "`TotalPrice` >= 0");
            });

            // Prevent physical deletion of products referenced by orders.
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }
}
