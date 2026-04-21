namespace McpDataService.Services;

public class DbIntrospectionService
{
    public async Task<List<string>> GetTablesAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        return new List<string> { "SampleTable1", "SampleTable2" };
    }

    public async Task<List<string>> GetColumnsAsync(string dbType, string connectionString, string tableName)
    {
        await Task.CompletedTask;
        return new List<string> { "Id", "Name", "CreatedAt" };
    }

    public async Task<Dictionary<string, string>> GetSchemaAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        return new Dictionary<string, string>
        {
            { "Tables", "SampleTable1,SampleTable2" },
            { "Views", "V_SAMPLE_AUDIT,V_SAMPLE_REPORT" },
            { "StoredProcedures", "sp_sample_sync,sp_sample_cleanup" },
            { "Triggers", "TRG_SAMPLE_TABLE_BI,TRG_SAMPLE_TABLE_AU" },
            { "Indexes", "IX_SAMPLE_TABLE_ID,IX_SAMPLE_TABLE_CREATED_AT" },
            { "Packages", string.Equals(dbType, "Oracle", StringComparison.OrdinalIgnoreCase) ? "PKG_SAMPLE_IMPORT,PKG_SAMPLE_REPORT" : string.Empty },
            { "Schemas", "dbo,app,audit" }
        };
    }

    public async Task<List<string>> GetViewsAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        return new List<string> { "V_SAMPLE_AUDIT", "V_SAMPLE_REPORT" };
    }

    public async Task<List<string>> GetStoredProceduresAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        return new List<string> { "sp_sample_sync", "sp_sample_cleanup" };
    }

    public async Task<List<string>> GetTriggersAsync(string dbType, string connectionString, string? table = null)
    {
        await Task.CompletedTask;
        var baseName = string.IsNullOrWhiteSpace(table) ? "SAMPLE_TABLE" : table;
        return new List<string> { $"TRG_{baseName}_BI", $"TRG_{baseName}_AU" };
    }

    public async Task<List<string>> GetIndexesAsync(string dbType, string connectionString, string? table = null)
    {
        await Task.CompletedTask;
        var baseName = string.IsNullOrWhiteSpace(table) ? "SAMPLE_TABLE" : table;
        return new List<string> { $"IX_{baseName}_ID", $"IX_{baseName}_CREATED_AT" };
    }

    public async Task<List<string>> GetPackagesAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        if (!string.Equals(dbType, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        return new List<string> { "PKG_SAMPLE_IMPORT", "PKG_SAMPLE_REPORT" };
    }

    public async Task<List<string>> GetSchemasAsync(string dbType, string connectionString)
    {
        await Task.CompletedTask;
        return new List<string> { "dbo", "app", "audit" };
    }
}

public class CrudService
{
    public async Task<object?> Read(string dbType, string connectionString, string table, string? where)
    {
        await Task.CompletedTask;
        return new { message = "Read operation simulated", table, where };
    }

    public async Task<object> Create(string dbType, string connectionString, string table, object data)
    {
        await Task.CompletedTask;
        return new { message = "Create operation simulated", table, data };
    }

    public async Task<object> Update(string dbType, string connectionString, string table, string id, object data)
    {
        await Task.CompletedTask;
        return new { message = "Update operation simulated", table, id, data };
    }

    public async Task<bool> Delete(string dbType, string connectionString, string table, string id)
    {
        await Task.CompletedTask;
        return true;
    }
}
