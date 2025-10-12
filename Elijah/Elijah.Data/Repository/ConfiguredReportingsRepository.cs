using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;


namespace Elijah.Data.Repository;

public interface IConfiguredReportingsRepository : IRepository<ApplicationDbContext>;


public class ConfiguredReportingsRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IConfiguredReportingsRepository
{
}