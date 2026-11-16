using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLMS.Contracts;
using GLMS.Web.Data;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GLMS.Tests;

public sealed class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task ProtectedResourcesRejectAnonymousRequests()
    {
        using var factory = new GlmsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateClientReturnsCreatedAndCanBeReadBack()
    {
        using var factory = new GlmsApiFactory();
        using var client = factory.CreateClient();
        await SeedVerifiedUserAsync(factory);
        await AuthenticateAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/clients", new CreateClientRequest
        {
            Name = "Northstar Freight",
            ContactDetails = "ops@northstar.example",
            Region = "Southern Africa"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Northstar Freight", created.Name);

        var readResponse = await client.GetAsync($"/api/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var readBack = await readResponse.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);
        Assert.Equal(created, readBack);
    }

    [Fact]
    public async Task ContractFilteringReturnsOnlyMatchingActiveContracts()
    {
        using var factory = new GlmsApiFactory();
        using var client = factory.CreateClient();
        await SeedVerifiedUserAsync(factory);
        await AuthenticateAsync(client);
        var expectedId = await SeedContractsAsync(factory);

        var today = DateTime.UtcNow.Date;
        var fromDate = Uri.EscapeDataString(today.AddDays(-1).ToString("yyyy-MM-dd"));
        var toDate = Uri.EscapeDataString(today.AddDays(1).ToString("yyyy-MM-dd"));
        var response = await client.GetAsync($"/api/contracts?status=Active&fromDate={fromDate}&toDate={toDate}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contracts = await response.Content.ReadFromJsonAsync<IReadOnlyList<ContractResponse>>(JsonOptions);
        var contract = Assert.Single(contracts!);
        Assert.Equal(expectedId, contract.Id);
        Assert.Equal(ContractStatus.Active, contract.Status);
    }

    [Fact]
    public async Task ReversedContractDateRangeReturnsBadRequest()
    {
        using var factory = new GlmsApiFactory();
        using var client = factory.CreateClient();
        await SeedVerifiedUserAsync(factory);
        await AuthenticateAsync(client);

        var response = await client.GetAsync("/api/contracts?fromDate=2026-12-31&toDate=2026-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task SeedVerifiedUserAsync(GlmsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "integration@example.test",
            Email = "integration@example.test",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, "P@ssw0rd-Integration42");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginAccountRequest
        {
            Email = "integration@example.test",
            Password = "P@ssw0rd-Integration42"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<AccountTokenResponse>(JsonOptions);
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    private static async Task<int> SeedContractsAsync(GlmsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
        var client = new Client
        {
            Name = "Filtering client",
            ContactDetails = "filter@example.test",
            Region = "Global"
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var active = new Contract
        {
            Reference = "ACTIVE-1",
            ServiceLevel = "Priority",
            StartDate = today.AddDays(-5),
            EndDate = today.AddDays(5),
            Status = ContractStatus.Active,
            ClientId = client.Id,
            AgreementFileName = "active.pdf"
        };
        var draft = new Contract
        {
            Reference = "DRAFT-1",
            ServiceLevel = "Standard",
            StartDate = today.AddDays(-5),
            EndDate = today.AddDays(5),
            Status = ContractStatus.Draft,
            ClientId = client.Id,
            AgreementFileName = "draft.pdf"
        };
        db.Contracts.AddRange(active, draft);
        await db.SaveChangesAsync();
        return active.Id;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
