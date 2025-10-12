using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;


namespace Elijah.Data.Repository;

public interface IDeviceFiltersRepository : IRepository<ApplicationDbContext>;


public class DeviceFiltersRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IDeviceFiltersRepository
{
}