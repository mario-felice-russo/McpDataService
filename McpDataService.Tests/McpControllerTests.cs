using System.Text.Json;
using McpDataService.Controllers;
using McpDataService.Services;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Tests;

public class McpControllerTests
{
    [Fact]
    public async Task HandleMcpRequest_ReturnsBadRequest_WhenJsonRpcIsMissing()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);
        var invalidRequest = TestHelpers.ToJsonElement(new
        {
            method = "db_get_tables",
            @params = new { db_type = "SQLite" }
        });

        var actionResult = await controller.HandleMcpRequest(invalidRequest);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task HandleMcpRequest_ReturnsUnknownMethodError_ForUnsupportedMethods()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var actionResult = await controller.HandleMcpRequest(BuildRequest("not_supported"));

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        var errorMessage = payload.GetProperty("result").GetProperty("error").GetString();

        Assert.Contains("Unknown method", errorMessage);
    }

    [Theory]
    [InlineData("db_get_tables")]
    [InlineData("db_get_columns")]
    [InlineData("db_get_schema")]
    [InlineData("db_get_views")]
    [InlineData("db_get_stored_procedures")]
    [InlineData("db_get_triggers")]
    [InlineData("db_get_indexes")]
    [InlineData("db_get_packages")]
    [InlineData("db_get_schemas")]
    [InlineData("db_read")]
    [InlineData("db_create")]
    [InlineData("db_update")]
    [InlineData("db_delete")]
    public async Task HandleMcpRequest_SupportsAllDatabaseCommands(string method)
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var actionResult = await controller.HandleMcpRequest(BuildRequest(method));

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        var result = payload.GetProperty("result");

        Assert.Equal("2.0", payload.GetProperty("jsonrpc").GetString());
        Assert.False(result.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task HandleMcpRequest_UsesCurrentDatabase_WhenDbTypeIsMissing()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        appSettingsContext.AppSettingsService.SetCurrentDatabase("Oracle");
        var controller = CreateController(appSettingsContext);

        var actionResult = await controller.HandleMcpRequest(BuildRequest("db_get_packages", includeDbType: false));

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        var packages = payload.GetProperty("result").GetProperty("packages").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.NotEmpty(packages);
        Assert.Contains("PKG_SAMPLE_REPORT", packages);
    }

    private static McpController CreateController(AppSettingsTestContext appSettingsContext)
    {
        var configuration = TestHelpers.BuildConfiguration(
            ("ConnectionStrings:SqlServer", "Server=(localdb)\\MSSQLLocalDB;Database=Dummy;"),
            ("ConnectionStrings:Oracle", "Data Source=dummy-oracle"),
            ("ConnectionStrings:SQLite", "Data Source=dummy.db"));

        return new McpController(
            new DbIntrospectionService(),
            new CrudService(),
            configuration,
            appSettingsContext.AppSettingsService);
    }

    private static JsonElement BuildRequest(string method, bool includeDbType = true)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["table"] = "TEST_TABLE",
            ["where"] = "Id = 1",
            ["id"] = "1",
            ["data"] = new Dictionary<string, object?>
            {
                ["Name"] = "Mario"
            }
        };

        if (includeDbType)
        {
            parameters["db_type"] = "SQLite";
        }

        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["id"] = 1,
            ["params"] = parameters
        };

        return TestHelpers.ToJsonElement(request);
    }
}
