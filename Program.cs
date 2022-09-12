using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using mige_collector;
using mige_collector.DAL;

class Program
{
    public static IConfigurationRoot? Configuration { get; private set; }

    static void Main(string[] args)
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").Build();

        var host = CreateDefaultBuilder().Build();

        host.Services.GetRequiredService<Startup>().DoWork();
    }

    static IHostBuilder CreateDefaultBuilder()
    {
        return Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<Startup>();
            services.AddDbContext<MigeContext>(options =>
            {
                options.UseSqlServer(Configuration?.GetConnectionString("MigeContext") ?? "");
            });
        });
    }
}