using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;

namespace Elijah.Data.Repository;

public interface IDeviceTemplateRepository : IRepository<ApplicationDbContext>;


public class DeviceTemplateRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IDeviceTemplateRepository
{
}