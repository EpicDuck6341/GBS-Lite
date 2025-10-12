using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;


namespace Elijah.Data.Repository;
public interface IOptionsRepository : IRepository<ApplicationDbContext>;

public class OptionsRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IOptionsRepository
{
}