using GLMS.Contracts;
using GLMS.Web.Data;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Api.Services;

public sealed class GlmsDataService(
    GlmsDbContext db,
    AgreementFileStore agreementFileStore,
    IExchangeRateProvider exchangeRateProvider) : IGlmsDataService
{
    public async Task<IReadOnlyList<ClientResponse>> GetClientsAsync(CancellationToken cancellationToken)
    {
        return await db.Clients.AsNoTracking()
            .OrderBy(client => client.Name)
            .Select(client => new ClientResponse(client.Id, client.Name, client.ContactDetails, client.Region, client.Contracts.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientResponse?> GetClientAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Clients.AsNoTracking()
            .Where(client => client.Id == id)
            .Select(client => new ClientResponse(client.Id, client.Name, client.ContactDetails, client.Region, client.Contracts.Count))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientResponse> CreateClientAsync(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var client = new Client
        {
            Name = request.Name.Trim(),
            ContactDetails = request.ContactDetails.Trim(),
            Region = request.Region.Trim()
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return new ClientResponse(client.Id, client.Name, client.ContactDetails, client.Region, 0);
    }

    public async Task<IReadOnlyList<ContractResponse>> GetContractsAsync(
        ContractStatus? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var query = db.Contracts.AsNoTracking().Include(contract => contract.Client).AsQueryable();

        if (status == ContractStatus.Expired)
        {
            query = query.Where(contract => contract.Status == ContractStatus.Expired
                || (contract.Status == ContractStatus.Active && contract.EndDate < today));
        }
        else if (status == ContractStatus.Active)
        {
            query = query.Where(contract => contract.Status == ContractStatus.Active
                && contract.StartDate <= today && contract.EndDate >= today);
        }
        else if (status.HasValue)
        {
            query = query.Where(contract => contract.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(contract => contract.EndDate >= fromDate.Value.UtcDateTime.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(contract => contract.StartDate <= toDate.Value.UtcDateTime.Date);
        }

        var contracts = await query.OrderByDescending(contract => contract.StartDate).ToListAsync(cancellationToken);
        return contracts.Select(ToResponse).ToArray();
    }

    public async Task<ContractResponse?> GetContractAsync(int id, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.AsNoTracking().Include(item => item.Client)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return contract is null ? null : ToResponse(contract);
    }

    public async Task<ContractResponse> CreateContractAsync(
        CreateContractRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndDate.UtcDateTime.Date < request.StartDate.UtcDateTime.Date)
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid contract dates", "End date must be on or after the start date.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid contract status", "Select a valid contract status.");
        }

        if (!await db.Clients.AnyAsync(client => client.Id == request.ClientId, cancellationToken))
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid client", "The selected client does not exist.");
        }

        if (await db.Contracts.AnyAsync(contract => contract.Reference == request.Reference.Trim(), cancellationToken))
        {
            throw new ApiOperationException(StatusCodes.Status409Conflict, "Duplicate contract reference", "That contract reference is already in use.");
        }

        string agreementFileName;
        try
        {
            agreementFileName = await agreementFileStore.SaveAsync(request.Agreement, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid agreement file", exception.Message);
        }

        var contract = new Contract
        {
            Reference = request.Reference.Trim(),
            ServiceLevel = request.ServiceLevel.Trim(),
            StartDate = request.StartDate.UtcDateTime.Date,
            EndDate = request.EndDate.UtcDateTime.Date,
            Status = request.Status,
            ClientId = request.ClientId,
            AgreementFileName = agreementFileName
        };
        db.Contracts.Add(contract);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            agreementFileStore.Delete(agreementFileName);
            throw new ApiOperationException(StatusCodes.Status409Conflict, "Contract could not be saved", "Check that the contract reference is unique and the selected client is valid.");
        }

        await db.Entry(contract).Reference(item => item.Client).LoadAsync(cancellationToken);
        return ToResponse(contract);
    }

    public async Task<ContractResponse?> UpdateContractStatusAsync(int id, ContractStatus status, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid contract status", "Select a valid contract status.");
        }

        var contract = await db.Contracts.Include(item => item.Client)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        contract.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(contract);
    }

    public async Task<byte[]?> GetAgreementAsync(int id, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        var path = agreementFileStore.FindPath(contract.AgreementFileName);
        return path is null ? null : await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRequestResponse>> GetServiceRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = await db.ServiceRequests.AsNoTracking()
            .Include(request => request.Contract)
            .ThenInclude(contract => contract!.Client)
            .OrderByDescending(request => request.CreatedUtc)
            .ToListAsync(cancellationToken);
        return requests.Select(ToResponse).ToArray();
    }

    public async Task<ServiceRequestResponse?> GetServiceRequestAsync(int id, CancellationToken cancellationToken)
    {
        var request = await db.ServiceRequests.AsNoTracking()
            .Include(item => item.Contract).ThenInclude(contract => contract!.Client)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return request is null ? null : ToResponse(request);
    }

    public async Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, cancellationToken);
        if (contract is null || !ContractWorkflow.CanCreateServiceRequest(contract, DateTime.UtcNow))
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid contract", "Choose a contract that is active and within its date range.");
        }

        var rate = await exchangeRateProvider.GetUsdToZarRateAsync(cancellationToken);
        var serviceRequest = new ServiceRequest
        {
            ContractId = contract.Id,
            Description = request.Description.Trim(),
            UsdCost = request.UsdCost,
            ExchangeRate = rate,
            LocalCost = CurrencyConverter.ConvertUsdToZar(request.UsdCost, rate),
            CreatedUtc = DateTime.UtcNow,
            Status = ServiceRequestStatus.Submitted
        };

        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(serviceRequest).Reference(item => item.Contract).LoadAsync(cancellationToken);
        await db.Entry(serviceRequest.Contract!).Reference(item => item.Client).LoadAsync(cancellationToken);
        return ToResponse(serviceRequest);
    }

    public Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken) =>
        exchangeRateProvider.GetUsdToZarRateAsync(cancellationToken);

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var clientCount = await db.Clients.CountAsync(cancellationToken);
        var activeContracts = await db.Contracts.CountAsync(contract => contract.Status == ContractStatus.Active
            && contract.StartDate <= today && contract.EndDate >= today, cancellationToken);
        var openRequests = await db.ServiceRequests.CountAsync(request => request.Status == ServiceRequestStatus.Submitted
            || request.Status == ServiceRequestStatus.InProgress, cancellationToken);
        var recentEntities = await db.ServiceRequests.AsNoTracking()
            .Include(request => request.Contract).ThenInclude(contract => contract!.Client)
            .OrderByDescending(request => request.CreatedUtc).Take(6).ToListAsync(cancellationToken);
        var recent = recentEntities.Select(ToResponse).ToArray();
        return new DashboardResponse(clientCount, activeContracts, openRequests, recent);
    }

    private static ContractResponse ToResponse(Contract contract) => new(
        contract.Id,
        contract.Reference,
        contract.ServiceLevel,
        new DateTimeOffset(DateTime.SpecifyKind(contract.StartDate, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(contract.EndDate, DateTimeKind.Utc)),
        contract.Status,
        contract.AgreementFileName,
        contract.ClientId,
        contract.Client?.Name ?? string.Empty);

    private static ServiceRequestResponse ToResponse(ServiceRequest request) => new(
        request.Id,
        request.Description,
        request.UsdCost,
        request.ExchangeRate,
        request.LocalCost,
        request.Status,
        new DateTimeOffset(DateTime.SpecifyKind(request.CreatedUtc, DateTimeKind.Utc)),
        request.ContractId,
        new ServiceRequestContractResponse(
            request.ContractId,
            request.Contract?.Reference ?? string.Empty,
            request.Contract?.Client?.Name ?? string.Empty));
}
