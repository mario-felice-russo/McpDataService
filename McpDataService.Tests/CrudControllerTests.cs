using McpDataService.Controllers;
using McpDataService.Services;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Tests;

public class CrudControllerTests
{
    [Fact]
    public async Task Read_ReturnsBadRequest_WhenConnectionStringIsMissing()
    {
        var controller = CreateController();

        var actionResult = await controller.Read("Oracle", "TEST_TABLE", where: null);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task Create_ReturnsSimulatedPayload_ForValidDbType()
    {
        var controller = CreateController();

        var actionResult = await controller.Create("SQLite", "TEST_TABLE", new { Name = "Mario" });

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        Assert.Equal("Create operation simulated", payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Update_ReturnsSimulatedPayload_ForValidDbType()
    {
        var controller = CreateController();

        var actionResult = await controller.Update("SQLite", "TEST_TABLE", "1", new { Name = "Aggiornato" });

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        Assert.Equal("Update operation simulated", payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Delete_ReturnsSuccess_ForValidDbType()
    {
        var controller = CreateController();

        var actionResult = await controller.Delete("SQLite", "TEST_TABLE", "1");

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);
        Assert.True(payload.GetProperty("success").GetBoolean());
    }

    private static CrudController CreateController()
    {
        var configuration = TestHelpers.BuildConfiguration(
            ("ConnectionStrings:SQLite", "Data Source=dummy.db"));

        return new CrudController(new CrudService(), configuration);
    }
}
