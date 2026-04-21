using McpDataService.Controllers;
using McpDataService.Services;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Tests;

public class ConfigApiControllerTests
{
    [Fact]
    public void GenerateApiClientKey_ReturnsOk_AndPersistsClient()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var actionResult = controller.GenerateApiClientKey("AlmavivaClient");

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = TestHelpers.ToJsonElement(okResult.Value);

        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Single(appSettingsContext.ApiClientAuthService.GetClients());
    }

    [Fact]
    public void GenerateApiClientKey_ReturnsConflict_WhenClientAlreadyExists()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);
        controller.GenerateApiClientKey("DuplicatedClient");

        var actionResult = controller.GenerateApiClientKey("DuplicatedClient");

        Assert.IsType<ConflictObjectResult>(actionResult);
    }

    [Fact]
    public void SaveCurrentDatabase_UpdatesSettings_WhenTrayServiceIsMissing()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var actionResult = controller.SaveCurrentDatabase("Oracle");

        var contentResult = Assert.IsType<ContentResult>(actionResult);
        Assert.Equal("Oracle", appSettingsContext.AppSettingsService.GetCurrentDatabase());
        Assert.Contains("Oracle", contentResult.Content);
    }

    [Fact]
    public void UpsertUiUser_AllowsEditingFromSelectedRow_WithoutChangingPassword()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var createResult = controller.UpsertUiUser("mario", "PasswordSicura1!", AdminUiAuthService.UserRole);
        Assert.IsType<ContentResult>(createResult);
        var originalUser = Assert.Single(appSettingsContext.AdminUiAuthService.GetUsers());

        var updateResult = controller.UpsertUiUser(
            username: "mario.dev",
            password: null,
            role: AdminUiAuthService.ProgrammerRole,
            originalUsername: "mario");

        Assert.IsType<ContentResult>(updateResult);

        var updatedUser = Assert.Single(appSettingsContext.AdminUiAuthService.GetUsers());
        Assert.Equal("mario.dev", updatedUser.Username);
        Assert.Equal(AdminUiAuthService.ProgrammerRole, updatedUser.Role);
        Assert.Equal(originalUser.PasswordHash, updatedUser.PasswordHash);
    }

    [Fact]
    public void DeleteUiUser_ReturnsBadRequest_WhenUserDoesNotExist()
    {
        using var appSettingsContext = new AppSettingsTestContext();
        var controller = CreateController(appSettingsContext);

        var actionResult = controller.DeleteUiUser("utente-mancante");

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    private static ConfigApiController CreateController(AppSettingsTestContext appSettingsContext)
    {
        return new ConfigApiController(
            appSettingsContext.AppSettingsService,
            appSettingsContext.ApiClientAuthService,
            appSettingsContext.AdminUiAuthService,
            trayIconService: null);
    }
}
