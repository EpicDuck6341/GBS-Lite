using System.Linq;
using System.Threading.Tasks;
using Elijah.Data.Repository;
using Elijah.Domain;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;

namespace Elijah.Logic.Concrete;


public class Service(/*IDevicesRepository repository,*/ BatchSettings settings)
    : IService
{
 
    public async Task FunctionName()
    {
    }
}