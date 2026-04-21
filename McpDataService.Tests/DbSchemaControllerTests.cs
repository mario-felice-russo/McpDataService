using McpDataService.Controllers;
using McpDataService.Services;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Tests;

public class DbSchemaControllerTests
{
    [Fact]
    public async Task GetTables_ReturnsBadRequest_WhenConnectionStringIsMissing()
    {
        var controller = CreateController();

        var actionResult = await controller.GetTables("SqlServer");

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task AllSchemaEndpoints_ReturnOk_ForValidDatabase()
    {
        var controller = CreateController();

        Assert.IsType<OkObjectResult>(await controller.GetTables("SQLite"));
        Assert.IsType<OkObjectResult>(await controller.GetColumns("SQLite", "TEST_TABLE"));
        Assert.IsType<OkObjectResult>(await controller.GetSchema("SQLite"));
        Assert.IsType<OkObjectResult>(await controller.GetViews("SQLite"));
        Assert.IsType<OkObjectResult>(await controller.GetStoredProcedures("SQLite"));
        Assert.IsType<OkObjectResult>(await controller.GetTriggers("SQLite", "TEST_TABLE"));
        Assert.IsType<OkObjectResult>(await controller.GetIndexes("SQLite", "TEST_TABLE"));
        Assert.IsType<OkObjectResult>(await controller.GetPackages("Oracle"));
        Assert.IsType<OkObjectResult>(await controller.GetSchemas("SQLite"));
    }

    [Fact]
    public async Task GetPackages_ReturnsOraclePackages_ForOracleDbType()
    {
        var controller = CreateController();

        var actionResult = await controller.GetPackages("Oracle");

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        var packages = payload.GetProperty("packages").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.NotEmpty(packages);
        Assert.Contains("PKG_SAMPLE_IMPORT", packages);
    }

    private static DbSchemaController CreateController()
    {
        var configuration = TestHelpers.BuildConfiguration(
            ("ConnectionStrings:SQLite", "Data Source=dummy.db"),
            ("ConnectionStrings:Oracle", "Data Source=dummy-oracle"));

        return new DbSchemaController(new DbIntrospectionService(), configuration);
    }
}
