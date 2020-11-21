using System;
using Microsoft.Extensions.Configuration;
using static System.Environment;

namespace Proto.TestFixtures
{
    public static class TestConfig
    {
        static TestConfig()
            => Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", false, false)
                .AddJsonFile($"appsettings.{GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json".ToLower(), true)
                .AddEnvironmentVariables()
                .Build();

        public static IConfigurationRoot Configuration { get; set; }
    }
}