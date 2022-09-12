using Microsoft.EntityFrameworkCore;

namespace mige_collector.DAL
{
    public class MigeContext : DbContext
    {
        public MigeContext(DbContextOptions<MigeContext> options) : base(options)
        {
            //Database.EnsureDeleted();
            Database.EnsureCreated();
            Database.Migrate();
        }

        public DbSet<Species>? Species { get; set; }
        public DbSet<SpeciesImage>? Images { get; set; }
    }
}
