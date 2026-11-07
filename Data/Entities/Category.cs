namespace foxtrot_store_backend.Data.Entities;

public class Category
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public string? ImageUrl { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> ChildCategories { get; set; }
        = new List<Category>();

    public ICollection<ProductCategory> ProductCategories { get; set; }
        = new List<ProductCategory>();

    public ICollection<CategoryTranslation> Translations { get; set; }
        = new List<CategoryTranslation>();
}
