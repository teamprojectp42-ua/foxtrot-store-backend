namespace foxtrot_store_backend.Data
{
    public class DataContext(
        DbContextOptions<DataContext> options
        ) : DbContext(options)
    {
    }
}
