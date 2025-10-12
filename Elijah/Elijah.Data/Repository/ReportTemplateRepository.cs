using GenericRepository.Model;
using GenericRepository.Repository;
using Microsoft.AspNetCore.Http;

namespace Elijah.Data.Repository;

public interface IReportTemplateRepository : IRepository<ApplicationDbContext>;

public class ReportTemplateRepository(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    HistorySettings? historySettings
)
    : Repository<ApplicationDbContext>(dbContext, httpContextAccessor, historySettings),
        IReportTemplateRepository
{
}