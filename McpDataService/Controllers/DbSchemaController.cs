using McpDataService.Services;
using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Controllers;

[ApiController]
[Route("db")]
public class DbSchemaController : ControllerBase
{
    private readonly DbIntrospectionService _introspectionService;
    private readonly IConfiguration _configuration;

    public DbSchemaController(
        DbIntrospectionService introspectionService,
        IConfiguration configuration)
    {
        _introspectionService = introspectionService;
        _configuration = configuration;
    }

    [HttpGet("{dbType}/tables")]
    public async Task<IActionResult> GetTables(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var tables = await _introspectionService.GetTablesAsync(dbType, connString);
        return Ok(new { tables });
    }

    [HttpGet("{dbType}/tables/{table}/columns")]
    public async Task<IActionResult> GetColumns(string dbType, string table)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var columns = await _introspectionService.GetColumnsAsync(dbType, connString, table);
        return Ok(new { table, columns });
    }

    [HttpGet("{dbType}/schema")]
    public async Task<IActionResult> GetSchema(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var schema = await _introspectionService.GetSchemaAsync(dbType, connString);
        return Ok(schema);
    }

    [HttpGet("{dbType}/views")]
    public async Task<IActionResult> GetViews(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var views = await _introspectionService.GetViewsAsync(dbType, connString);
        return Ok(new { views });
    }

    [HttpGet("{dbType}/stored-procedures")]
    public async Task<IActionResult> GetStoredProcedures(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var storedProcedures = await _introspectionService.GetStoredProceduresAsync(dbType, connString);
        return Ok(new { storedProcedures });
    }

    [HttpGet("{dbType}/triggers")]
    public async Task<IActionResult> GetTriggers(string dbType, [FromQuery] string? table = null)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var triggers = await _introspectionService.GetTriggersAsync(dbType, connString, table);
        return Ok(new { table, triggers });
    }

    [HttpGet("{dbType}/indexes")]
    public async Task<IActionResult> GetIndexes(string dbType, [FromQuery] string? table = null)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var indexes = await _introspectionService.GetIndexesAsync(dbType, connString, table);
        return Ok(new { table, indexes });
    }

    [HttpGet("{dbType}/packages")]
    public async Task<IActionResult> GetPackages(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var packages = await _introspectionService.GetPackagesAsync(dbType, connString);
        return Ok(new { packages });
    }

    [HttpGet("{dbType}/schemas")]
    public async Task<IActionResult> GetSchemas(string dbType)
    {
        if (!TryGetConnectionString(dbType, out var connString))
        {
            return BadRequest("Invalid dbType");
        }

        var schemas = await _introspectionService.GetSchemasAsync(dbType, connString);
        return Ok(new { schemas });
    }

    private bool TryGetConnectionString(string dbType, out string connectionString)
    {
        connectionString = _configuration.GetConnectionString(dbType) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(connectionString);
    }
}
