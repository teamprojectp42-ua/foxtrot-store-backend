namespace foxtrot_store_backend.Data.Entities;

public class ProductSpecification
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;

    public ICollection<ProductSpecificationTranslation> Translations
    { get; set; } = new List<ProductSpecificationTranslation>();
}