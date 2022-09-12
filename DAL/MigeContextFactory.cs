using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace mige_collector.DAL
{
    public class MigeContextFactory : IDesignTimeDbContextFactory<MigeContext>
    {
        public MigeContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MigeContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=mige;Trusted_Connection=True;");

            return new MigeContext(optionsBuilder.Options);
        }
    }
}
