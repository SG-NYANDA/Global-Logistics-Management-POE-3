using GLMS.Contracts;
using GLMS.Web.Models;

namespace GLMS.Api.Services;

public interface IGlmsDataService
{
    Task<IReadOnlyList<ClientResponse>> GetClientsAsync(CancellationToken cancellationToken);
    Task<ClientResponse?> GetClientAsync(int id, CancellationToken cancellationToken);
    Task<ClientResponse> CreateClientAsync(CreateClientRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractResponse>> GetContractsAsync(ContractStatus? status, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken cancellationToken);
    Task<ContractResponse?> GetContractAsync(int id, CancellationToken cancellationToken);
    Task<ContractResponse> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken);
    Task<ContractResponse?> UpdateContractStatusAsync(int id, ContractStatus status, CancellationToken cancellationToken);
    Task<byte[]?> GetAgreementAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceRequestResponse>> GetServiceRequestsAsync(CancellationToken cancellationToken);
    Task<ServiceRequestResponse?> GetServiceRequestAsync(int id, CancellationToken cancellationToken);
    Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken);
    Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken);
    Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);
}
