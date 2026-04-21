using McpDataService.Services;
using McpDataService.Tests.TestSupport;

namespace McpDataService.Tests;

public class DatabaseCommandServiceTests
{
    [Fact]
    public async Task CrudService_CommandsReturnExpectedPayloads()
    {
        var service = new CrudService();

        var readResult = await service.Read("SQLite", "Data Source=dummy.db", "TEST_TABLE", "Id = 1");
        var createResult = await service.Create("SQLite", "Data Source=dummy.db", "TEST_TABLE", new { Name = "Mario" });
        var updateResult = await service.Update("SQLite", "Data Source=dummy.db", "TEST_TABLE", "1", new { Name = "Updated" });
        var deleteResult = await service.Delete("SQLite", "Data Source=dummy.db", "TEST_TABLE", "1");

        var readPayload = TestHelpers.ToJsonElement(readResult);
        var createPayload = TestHelpers.ToJsonElement(createResult);
        var updatePayload = TestHelpers.ToJsonElement(updateResult);

        Assert.Equal("Read operation simulated", readPayload.GetProperty("message").GetString());
        Assert.Equal("Create operation simulated", createPayload.GetProperty("message").GetString());
        Assert.Equal("Update operation simulated", updatePayload.GetProperty("message").GetString());
        Assert.True(deleteResult);
    }

    [Fact]
    public async Task DbIntrospectionService_CommandsReturnExpectedData()
    {
        var service = new DbIntrospectionService();

        var tables = await service.GetTablesAsync("SQLite", "Data Source=dummy.db");
        var columns = await service.GetColumnsAsync("SQLite", "Data Source=dummy.db", "TEST_TABLE");
        var schema = await service.GetSchemaAsync("SQLite", "Data Source=dummy.db");
        var views = await service.GetViewsAsync("SQLite", "Data Source=dummy.db");
        var storedProcedures = await service.GetStoredProceduresAsync("SQLite", "Data Source=dummy.db");
        var triggers = await service.GetTriggersAsync("SQLite", "Data Source=dummy.db", "TEST_TABLE");
        var indexes = await service.GetIndexesAsync("SQLite", "Data Source=dummy.db", "TEST_TABLE");
        var oraclePackages = await service.GetPackagesAsync("Oracle", "Data Source=dummy-oracle");
        var sqlitePackages = await service.GetPackagesAsync("SQLite", "Data Source=dummy.db");
        var schemas = await service.GetSchemasAsync("SQLite", "Data Source=dummy.db");

        Assert.Contains("SampleTable1", tables);
        Assert.Contains("Id", columns);
        Assert.True(schema.ContainsKey("Tables"));
        Assert.NotEmpty(views);
        Assert.NotEmpty(storedProcedures);
        Assert.Contains("TRG_TEST_TABLE_BI", triggers);
        Assert.Contains("IX_TEST_TABLE_ID", indexes);
        Assert.NotEmpty(oraclePackages);
        Assert.Empty(sqlitePackages);
        Assert.Contains("dbo", schemas);
    }
}
