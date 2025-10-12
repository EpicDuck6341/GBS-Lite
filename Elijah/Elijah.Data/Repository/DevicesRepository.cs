using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;


namespace Elijah.Data.Repository;

public interface IDevicesRepository : IRepository<ApplicationDbContext>;


public class DevicesRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IDevicesRepository
{
}