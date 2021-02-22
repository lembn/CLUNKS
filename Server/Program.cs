using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Reflection;

namespace Server
{
    //TODO: write summaries 
    public class Program
    {
        public static void Main(string[] args)
        {
            UriBuilder uri = new UriBuilder(Assembly.GetEntryAssembly().Location);
            Log.Logger = new LoggerConfiguration()
                             .MinimumLevel.Information()
                             .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                             .Enrich.FromLogContext()
                             .WriteTo.File(String.Concat(Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)), @"\data\log.txt"))
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
