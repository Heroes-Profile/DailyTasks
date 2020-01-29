using System;
using System.IO;
using System.Threading.Tasks;
using DailyTasks.Models;
using HeroesProfileDb.HeroesProfile;
using HeroesProfileDb.HeroesProfileBrawl;
using HeroesProfileDb.HeroesProfileCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyTasks
{
    class Program
    {  
        //We need this in ConsoleApp.cs since we can't DI into a static class
        public static ServiceProvider ServiceProviderProvider;
        private static async Task Main(string[] args)
        {
            // Create service collection and configure our services
            var services = ConfigureServices();
            // Generate a provider
            var serviceProvider = services.BuildServiceProvider();
            ServiceProviderProvider = serviceProvider;
   
            // Kick off our actual code
            await ConsoleApp.Run();
        }
        
        private static IServiceCollection ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();
            
            
            // Set up the objects we need to get to configuration settings
            var config = LoadConfiguration();
            var dbSettings = config.GetSection("DbSettings").Get<DbSettings>();
            var threadingSettings = config.GetSection("ThreadingSettings").Get<ThreadingSettings>();

            // Add the config to our DI container for later use
            services.AddSingleton(config);
            services.AddSingleton(dbSettings);
            services.AddSingleton(threadingSettings);

            // EF Db config
            services.AddDbContext<HeroesProfileContext>(options => options.UseMySql(ConnectionStringBuilder.BuildConnectionString(dbSettings)));
            dbSettings.database = "heroesprofile_brawl";
            services.AddDbContext<HeroesProfileBrawlContext>(options => options.UseMySql(ConnectionStringBuilder.BuildConnectionString(dbSettings)));
            dbSettings.database = "heroesprofile_cache";
            services.AddDbContext<HeroesProfileCacheContext>(options => options.UseMySql(ConnectionStringBuilder.BuildConnectionString(dbSettings)));
            dbSettings.database = "heroesprofile";

            services.AddScoped<CalculateLeaderBoardsService>();
            services.AddScoped<CalculateChangeService>();

            // IMPORTANT! Register our application entry point
            services.AddTransient<ConsoleApp>();
            return services;
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, true)
                    .AddEnvironmentVariables();
            return  builder.Build();
        }
    }
}