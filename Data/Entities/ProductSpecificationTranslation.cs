namespace foxtrot_store_backend.Data.Entities;

public sealed class ProductSpecificationTranslation
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid ProductId { get; set; }

    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public ProductSpecification Specification { get; set; } = null!;
}