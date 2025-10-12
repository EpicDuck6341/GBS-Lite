using Elijah.Data;
using Elijah.Data.Context;
using Elijah.Data.Repository;
using Elijah.Domain;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using GenericRepository.Utilities;
using LogManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elijah.Logic.Injection
{
    public class ServiceMapper
    {
        /// <summary>
        /// This method mirrors the ConfigureServices dependancy injection seen in Startup.cs of a regular web project, 
        /// to maintain the project references structure
        /// For a list on Service lifetimes, Scoped vs Transient etc. please see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2#service-lifetimes
        /// </summary>
        /// <param name="services">Service map for DI</param>
        /// <param name="configuration">Pass appsettings.json to this param using an IConfiguration obj</param>
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            //DbContext
            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());

            //Logging
            services.InitialiseFsLogging(configuration);
            //Services
            services.AddTransient<IService, Service>();
            services.AddSingleton<IZigbeeClient,ZigbeeClient>();
            //Settings
            services.AddSingleton(configuration.GetSection("BatchSettings").Get<BatchSettings>());

            //Repository
            services.AddGenericRepository<ApplicationDbContext,IDevicesRepository,DevicesRepository>();
        }
    }
}