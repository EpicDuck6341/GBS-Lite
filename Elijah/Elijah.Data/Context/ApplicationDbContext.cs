using Elijah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Data
{
    public class ApplicationDbContext : DbContext
    {
        
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        #region Core
        public DbSet<ConfiguredReport> ConfiguredReports { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceFilter> DeviceFilters { get; set; }
        public DbSet<DeviceTemplate> DeviceTemplates { get; set; }
        public DbSet<Option> Options { get; set; }
        public DbSet<ReportTemplate> ReportTemplates { get; set; }
        #endregion

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ConfiguredReport>().ToTable("core_ConfiguredReports");
            builder.Entity<Device>().ToTable("core_Device");
            builder.Entity<DeviceFilter>().ToTable("core_DeviceFilter");
            builder.Entity<DeviceTemplate>().ToTable("core_DeviceTemplate");
            builder.Entity<Option>().ToTable("core_Option");
            builder.Entity<ReportTemplate>().ToTable("core_ReportTemplate");
        }
    }
}