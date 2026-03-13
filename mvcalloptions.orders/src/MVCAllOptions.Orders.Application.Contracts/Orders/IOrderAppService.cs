using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MVCAllOptions.Orders;

public interface IOrderAppService : IApplicationService
{
    Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<OrderDto> CreateAsync(OrderCreationDto input);
}
