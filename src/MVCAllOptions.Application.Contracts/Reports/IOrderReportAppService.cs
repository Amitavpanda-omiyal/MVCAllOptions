using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MVCAllOptions.Reports;

public interface IOrderReportAppService : IApplicationService
{
    Task<ListResultDto<OrderReportDto>> GetLatestOrdersAsync();
}
