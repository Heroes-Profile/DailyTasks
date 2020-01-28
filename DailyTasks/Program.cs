using System;
using System.IO;
using DailyTasks.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyTasks
{
    class Program
    {  
        //We need this in ConsoleApp.cs since we can't DI into a static class
        public static ServiceProvider ServiceProviderProvider;
        private static void Main(string[] args)
        {
            // Create service collection and configure our services
            var services = ConfigureServices();
            // Generate a provider
            var serviceProvider = services.BuildServiceProvider();
            ServiceProviderProvider = serviceProvider;
   
            // Kick off our actual code
            ConsoleApp.Run();
        }
        
        private static IServiceCollection ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();
            
            
            // Set up the objects we need to get to configuration settings
            var config = LoadConfiguration();
            var dbSettings = config.GetSection("DbSettings").Get<DbSettings>();
            
            // Add the config to our DI container for later use
            services.AddSingleton(config);
            services.AddSingleton(dbSettings);
            
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