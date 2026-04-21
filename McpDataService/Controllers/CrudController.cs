using Microsoft.AspNetCore.Mvc;
using McpDataService.Services;

namespace McpDataService.Controllers;

[ApiController]
[Route("crud")]
public class CrudController : ControllerBase
{
    private readonly CrudService _crudService;
    private readonly IConfiguration _configuration;

    public CrudController(
        CrudService crudService,
        IConfiguration configuration)
    {
        _crudService = crudService;
        _configuration = configuration;
    }

    [HttpGet("{dbType}/{table}")]
    public async Task<IActionResult> Read(string dbType, string table, [FromQuery] string? where)
    {

        var connString = _configuration.GetConnectionString(dbType);
        if (string.IsNullOrEmpty(connString)) return BadRequest("Invalid dbType");

        var result = await _crudService.Read(dbType, connString, table, where);
        return Ok(result);
    }

    [HttpPost("{dbType}/{table}")]
    public async Task<IActionResult> Create(string dbType, string table, [FromBody] object data)
    {

        var connString = _configuration.GetConnectionString(dbType);
        if (string.IsNullOrEmpty(connString)) return BadRequest("Invalid dbType");

        var result = await _crudService.Create(dbType, connString, table, data);
        return Ok(result);
    }

    [HttpPut("{dbType}/{table}/{id}")]
    public async Task<IActionResult> Update(string dbType, string table, string id, [FromBody] object data)
    {

        var connString = _configuration.GetConnectionString(dbType);
        if (string.IsNullOrEmpty(connString)) return BadRequest("Invalid dbType");

        var result = await _crudService.Update(dbType, connString, table, id, data);
        return Ok(result);
    }

    [HttpDelete("{dbType}/{table}/{id}")]
    public async Task<IActionResult> Delete(string dbType, string table, string id)
    {

        var connString = _configuration.GetConnectionString(dbType);
        if (string.IsNullOrEmpty(connString)) return BadRequest("Invalid dbType");

        var result = await _crudService.Delete(dbType, connString, table, id);
        return Ok(new { success = result });
    }
}