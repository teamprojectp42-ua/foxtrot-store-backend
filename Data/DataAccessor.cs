using foxtrot_store_backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataAccessor(DataContext dataContext)
{
    private readonly DataContext _dataContext = dataContext;

    public Guid GetDbIdentity()
    {
        return _dataContext.Database
            .SqlQuery<Guid>($"SELECT NEWID()")
            .AsEnumerable()
            .First();
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        return await _dataContext.Products
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        dataContext.Products.Add(product);

        await _dataContext.SaveChangesAsync();

        return product;
    }
}