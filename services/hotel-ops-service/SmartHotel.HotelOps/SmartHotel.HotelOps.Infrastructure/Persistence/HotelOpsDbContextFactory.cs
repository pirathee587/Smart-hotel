using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartHotel.HotelOps.Infrastructure.Persistence;

public class HotelOpsDbContextFactory : IDesignTimeDbContextFactory<HotelOpsDbContext>
{
    public HotelOpsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HotelOpsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=smarthotel_hotelops_db;Username=postgres;Password=postgres;");
        return new HotelOpsDbContext(optionsBuilder.Options);
    }
}
