using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataAccessor(DataContext dataContext)
{
    public async Task<List<Product>> GetProductsAsync()
    {
        return await dataContext.Products
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        dataContext.Products.Add(product);

        await dataContext.SaveChangesAsync();

        return product;
    }
}