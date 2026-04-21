using Microsoft.AspNetCore.Mvc;
using McpDataService.Services;
using System.Text.Json;

namespace McpDataService.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly DbIntrospectionService _introspectionService;
    private readonly CrudService _crudService;
    private readonly IConfiguration _configuration;
    private readonly AppSettingsService _appSettingsService;

    public McpController(
        DbIntrospectionService introspectionService,
        CrudService crudService,
        IConfiguration configuration,
        AppSettingsService appSettingsService)
    {
        _introspectionService = introspectionService;
        _crudService = crudService;
        _configuration = configuration;
        _appSettingsService = appSettingsService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] JsonElement? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { jsonrpc = "2.0", error = new { code = -32600, message = "Invalid Request" }, id = (object?)null });

            var req = request.Value;
            if (!req.TryGetProperty("jsonrpc", out var jsonrpc) || jsonrpc.GetString() != "2.0")
                return BadRequest(new { jsonrpc = "2.0", error = new { code = -32600, message = "Invalid Request" }, id = (object?)null });

            if (!req.TryGetProperty("method", out var methodElement))
                return BadRequest(new { jsonrpc = "2.0", error = new { code = -32600, message = "Missing method" }, id = (object?)null });

            var method = methodElement.GetString() ?? "";
            JsonElement? idElement = null;
            if (req.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
                idElement = idEl;
            }

            var parameters = req.TryGetProperty("params", out var parametersElement)
                ? parametersElement
                : default(JsonElement);

            var result = method switch
            {
                "db_get_tables" => await HandleGetTables(parameters),
                "db_get_columns" => await HandleGetColumns(parameters),
                "db_get_schema" => await HandleGetSchema(parameters),
                "db_get_views" => await HandleGetViews(parameters),
                "db_get_stored_procedures" => await HandleGetStoredProcedures(parameters),
                "db_get_triggers" => await HandleGetTriggers(parameters),
                "db_get_indexes" => await HandleGetIndexes(parameters),
                "db_get_packages" => await HandleGetPackages(parameters),
                "db_get_schemas" => await HandleGetSchemas(parameters),
                "db_read" => await HandleRead(parameters),
                "db_create" => await HandleCreate(parameters),
                "db_update" => await HandleUpdate(parameters),
                "db_delete" => await HandleDelete(parameters),
                _ => new { error = $"Unknown method: {method}" }
            };

            return Ok(new { jsonrpc = "2.0", result, id = idElement });
        }
        catch (Exception ex)
        {
            return Ok(new { jsonrpc = "2.0", error = new { code = -32603, message = ex.Message }, id = (object?)null });
        }
    }

    private async Task<object> HandleGetTables(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var tables = await _introspectionService.GetTablesAsync(dbType, connString ?? "");
        return new { tables };
    }

    private async Task<object> HandleGetColumns(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var connString = _configuration.GetConnectionString(dbType);
        var columns = await _introspectionService.GetColumnsAsync(dbType, connString ?? "", table);
        return new { table, columns };
    }

    private async Task<object> HandleGetSchema(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var schema = await _introspectionService.GetSchemaAsync(dbType, connString ?? "");
        return schema;
    }

    private async Task<object> HandleGetViews(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var views = await _introspectionService.GetViewsAsync(dbType, connString ?? "");
        return new { views };
    }

    private async Task<object> HandleGetStoredProcedures(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var storedProcedures = await _introspectionService.GetStoredProceduresAsync(dbType, connString ?? "");
        return new { storedProcedures };
    }

    private async Task<object> HandleGetTriggers(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var connString = _configuration.GetConnectionString(dbType);
        var triggers = await _introspectionService.GetTriggersAsync(dbType, connString ?? "", table);
        return new { table, triggers };
    }

    private async Task<object> HandleGetIndexes(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var connString = _configuration.GetConnectionString(dbType);
        var indexes = await _introspectionService.GetIndexesAsync(dbType, connString ?? "", table);
        return new { table, indexes };
    }

    private async Task<object> HandleGetPackages(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var packages = await _introspectionService.GetPackagesAsync(dbType, connString ?? "");
        return new { packages };
    }

    private async Task<object> HandleGetSchemas(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var connString = _configuration.GetConnectionString(dbType);
        var schemas = await _introspectionService.GetSchemasAsync(dbType, connString ?? "");
        return new { schemas };
    }

    private async Task<object> HandleRead(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var where = GetParameter(parameters, "where");
        var connString = _configuration.GetConnectionString(dbType);
        return await _crudService.Read(dbType, connString ?? "", table, where);
    }

    private async Task<object> HandleCreate(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var data = parameters.TryGetProperty("data", out var dataElement) ? dataElement.GetRawText() : "{}";
        var connString = _configuration.GetConnectionString(dbType);
        return await _crudService.Create(dbType, connString ?? "", table, data);
    }

    private async Task<object> HandleUpdate(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var id = GetParameter(parameters, "id");
        var data = parameters.TryGetProperty("data", out var dataElement) ? dataElement.GetRawText() : "{}";
        var connString = _configuration.GetConnectionString(dbType);
        return await _crudService.Update(dbType, connString ?? "", table, id, data);
    }

    private async Task<object> HandleDelete(JsonElement parameters)
    {
        var dbType = ResolveDbType(parameters);
        var table = GetParameter(parameters, "table");
        var id = GetParameter(parameters, "id");
        var connString = _configuration.GetConnectionString(dbType);
        var result = await _crudService.Delete(dbType, connString ?? "", table, id);
        return new { success = result };
    }

    private string ResolveDbType(JsonElement parameters)
    {
        var requestedDbType = GetParameter(parameters, "db_type");
        if (!string.IsNullOrWhiteSpace(requestedDbType))
        {
            return requestedDbType;
        }

        return _appSettingsService.GetCurrentDatabase();
    }

    private string GetParameter(JsonElement parameters, string key)
    {
        if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(key, out var value))
            return value.GetString() ?? "";
        return "";
    }
}