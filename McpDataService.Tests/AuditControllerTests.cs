using McpDataService.Controllers;
using McpDataService.Data;
using McpDataService.Models.Audit;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpDataService.Tests;

public class AuditControllerTests
{
    [Fact]
    public void GetRequests_WithAndFilters_ReturnsExpectedRows()
    {
        using var auditDb = CreateAuditContext();
        SeedAuditRequests(auditDb);
        var controller = new AuditController(auditDb);

        var actionResult = controller.GetRequests(
            fromDate: null,
            toDate: null,
            endpoint: "/db",
            userType: "ApiClient",
            dbType: null,
            isMcpRequest: null,
            filterOperator: "AND",
            page: 1,
            pageSize: 50);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);

        Assert.Equal("AND", payload.GetProperty("FilterOperator").GetString());
        Assert.Equal(1, payload.GetProperty("Total").GetInt32());
        Assert.Single(payload.GetProperty("Requests").EnumerateArray());
    }

    [Fact]
    public void GetRequests_WithOrFilters_ReturnsUnionOfMatches()
    {
        using var auditDb = CreateAuditContext();
        SeedAuditRequests(auditDb);
        var controller = new AuditController(auditDb);

        var actionResult = controller.GetRequests(
            fromDate: null,
            toDate: null,
            endpoint: "/db",
            userType: "AdminUi",
            dbType: null,
            isMcpRequest: null,
            filterOperator: "OR",
            page: 1,
            pageSize: 50);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);

        Assert.Equal("OR", payload.GetProperty("FilterOperator").GetString());
        Assert.Equal(2, payload.GetProperty("Total").GetInt32());
    }

    [Fact]
    public void GetRequest_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var auditDb = CreateAuditContext();
        var controller = new AuditController(auditDb);

        var actionResult = controller.GetRequest(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(actionResult);
    }

    [Fact]
    public void GetRequest_ReturnsRequestDetail_WhenIdExists()
    {
        using var auditDb = CreateAuditContext();
        var requestId = SeedAuditRequests(auditDb).First();
        var controller = new AuditController(auditDb);

        var actionResult = controller.GetRequest(requestId);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        var parameters = payload.GetProperty("Parameters");

        Assert.Equal(requestId, payload.GetProperty("Id").GetGuid());
        Assert.Equal("AUDIT_TABLE", parameters.GetProperty("table").GetString());
    }

    private static AuditDbContext CreateAuditContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuditDbContext(options);
    }

    private static IReadOnlyList<Guid> SeedAuditRequests(AuditDbContext auditDb)
    {
        var now = DateTime.UtcNow;
        var auditRequests = new[]
        {
            new AuditRequest
            {
                Timestamp = now.AddMinutes(-10),
                Endpoint = "/db/sqlite/tables",
                HttpMethod = "GET",
                ResponseStatus = 200,
                ExecutionTimeMs = 4,
                DbType = "SQLite",
                UserType = "ApiClient",
                UserIdentifier = "client-a",
                IsMcpRequest = false,
                Parameters =
                {
                    new AuditRequestParameter { Key = "table", Value = "AUDIT_TABLE" }
                }
            },
            new AuditRequest
            {
                Timestamp = now.AddMinutes(-5),
                Endpoint = "/mcp",
                HttpMethod = "POST",
                ResponseStatus = 200,
                ExecutionTimeMs = 8,
                DbType = "Oracle",
                UserType = "AdminUi",
                UserIdentifier = "admin",
                IsMcpRequest = true,
                Parameters =
                {
                    new AuditRequestParameter { Key = "table", Value = "PROC_TABLE" }
                }
            },
            new AuditRequest
            {
                Timestamp = now.AddMinutes(-1),
                Endpoint = "/api/config/users/upsert",
                HttpMethod = "POST",
                ResponseStatus = 200,
                ExecutionTimeMs = 3,
                DbType = "None",
                UserType = "UserUi",
                UserIdentifier = "user",
                IsMcpRequest = false
            }
        };

        auditDb.Requests.AddRange(auditRequests);
        auditDb.SaveChanges();
        return auditRequests.Select(request => request.Id).ToList();
    }
}
