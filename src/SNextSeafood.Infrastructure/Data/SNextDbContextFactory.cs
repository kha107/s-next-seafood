using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SNextSeafood.Infrastructure.Data;

public class SNextDbContextFactory : IDesignTimeDbContextFactory<SNextDbContext>
{
    public SNextDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SNextDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SNextSeafoodDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        return new SNextDbContext(optionsBuilder.Options);
    }
}
