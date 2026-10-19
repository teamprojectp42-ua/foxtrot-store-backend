namespace foxtrot_store_backend.Models.Categories;

public class CategoryTreeDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int SortOrder { get; set; }

    public List<CategoryTreeDto> Children { get; set; } = [];
}