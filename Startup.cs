using Microsoft.Extensions.Configuration;
using mige_collector.DAL;

namespace mige_collector
{
    internal class Startup
    {
        private readonly IConfiguration configuration;
        private readonly MigeContext migeContext;

        public Startup(IConfiguration configuration, MigeContext migeContext)
        {
            this.configuration = configuration;
            this.migeContext = migeContext;
        }

        public void DoWork()
        {
            SpeciesListCollector collector = new(migeContext);
            collector.Collect();
        }
    }
}
