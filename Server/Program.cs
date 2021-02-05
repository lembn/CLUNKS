using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;

namespace Server
{
    //TODO: write summaries 
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                             .MinimumLevel.Information()
                             .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                             .Enrich.FromLogContext()
                             .WriteTo.File(String.Concat(Directory.GetCurrentDirectory(), @"\access"))
                             .CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
                services.AddHostedService<Worker>())
            .UseSerilog();
    }
}
