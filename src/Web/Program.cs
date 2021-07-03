using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using Elastic.CommonSchema.Serilog;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Microsoft.eShopWeb.Web
{
  
    public class Program
    {
        static EventHubProducerClient producerClient;
        
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args)
                        .Build();
            producerClient = new EventHubProducerClient("Endpoint=sb://tedelklab.servicebus.windows.net/;SharedAccessKeyName=seriallog;SharedAccessKey=qCdxzMctIju6spKhYJhrUuAKXVlChmQou022tvD6n94=", "seriallogtoeh");


            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                
                Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
               .Enrich.FromLogContext()
               .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
               .WriteTo.File(new EcsTextFormatter(), "C:/logs/myapp.txt", rollingInterval: RollingInterval.Day)
               .WriteTo.AzureEventHub(producerClient, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
               .CreateLogger();

                try
                {
                    var catalogContext = services.GetRequiredService<CatalogContext>();
                    await CatalogContextSeed.SeedAsync(catalogContext, loggerFactory);

                    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                    await AppIdentityDbContextSeed.SeedAsync(userManager, roleManager);

                    Log.Information("Starting web host");
                }
                catch (Exception ex)
                {

                    Log.Fatal(ex, "Host terminated unexpectedly");
                    //var logger = loggerFactory.CreateLogger<Program>();
                    //logger.LogError(ex, "An error occurred seeding the DB.");


                }
                finally
                {
                    Log.CloseAndFlush();
                }
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders(); //去掉預設新增的日誌提供程式
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
