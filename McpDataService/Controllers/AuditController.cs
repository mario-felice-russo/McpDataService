using McpDataService.Data;
using McpDataService.Models.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace McpDataService.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly AuditDbContext _auditDb;

    public AuditController(AuditDbContext auditDb)
    {
        _auditDb = auditDb;
    }

    [HttpGet("requests")]
    public IActionResult GetRequests(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? endpoint,
        [FromQuery] string? userType,
        [FromQuery] string? dbType,
        [FromQuery] bool? isMcpRequest,
        [FromQuery] string filterOperator = "AND",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = ApplyFilters(
            _auditDb.Requests.AsQueryable(),
            fromDate,
            toDate,
            endpoint,
            userType,
            dbType,
            isMcpRequest,
            filterOperator);

        var total = query.Count();
        var requests = query
            .OrderByDescending(r => r.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Parameters)
            .ToList();

        return Ok(new
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            FilterOperator = NormalizeOperator(filterOperator),
            Requests = requests.Select(r => new
            {
                r.Id,
                r.Timestamp,
                r.Endpoint,
                r.HttpMethod,
                r.ResponseStatus,
                r.ExecutionTimeMs,
                r.DbType,
                r.UserType,
                r.UserIdentifier,
                r.IsMcpRequest,
                Parameters = r.Parameters.ToDictionary(p => p.Key, p => p.Value)
            })
        });
    }

    [HttpGet("requests/{id:guid}")]
    public IActionResult GetRequest(Guid id)
    {
        var request = _auditDb.Requests
            .Include(r => r.Parameters)
            .FirstOrDefault(r => r.Id == id);

        if (request == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            request.Id,
            request.Timestamp,
            request.Endpoint,
            request.HttpMethod,
            RequestHeaders = request.RequestHeaders,
            RequestBody = request.RequestBody,
            request.ResponseStatus,
            request.ResponseBody,
            request.ExecutionTimeMs,
            request.DbType,
            request.UserType,
            request.UserIdentifier,
            request.IsMcpRequest,
            Parameters = request.Parameters.ToDictionary(p => p.Key, p => p.Value)
        });
    }

    private static IQueryable<AuditRequest> ApplyAndFilters(
        IQueryable<AuditRequest> query,
        DateTime? fromDate,
        DateTime? toDate,
        string? endpoint,
        string? userType,
        string? dbType,
        bool? isMcpRequest)
    {
        if (fromDate.HasValue)
        {
            query = query.Where(r => r.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.Timestamp <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            query = query.Where(r => r.Endpoint.Contains(endpoint));
        }

        if (!string.IsNullOrWhiteSpace(userType))
        {
            query = query.Where(r => r.UserType == userType);
        }

        if (!string.IsNullOrWhiteSpace(dbType))
        {
            query = query.Where(r => r.DbType == dbType);
        }

        if (isMcpRequest.HasValue)
        {
            query = query.Where(r => r.IsMcpRequest == isMcpRequest.Value);
        }

        return query;
    }

    private static IQueryable<AuditRequest> ApplyFilters(
        IQueryable<AuditRequest> query,
        DateTime? fromDate,
        DateTime? toDate,
        string? endpoint,
        string? userType,
        string? dbType,
        bool? isMcpRequest,
        string filterOperator)
    {
        var normalizedOperator = NormalizeOperator(filterOperator);
        if (!string.Equals(normalizedOperator, "OR", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyAndFilters(query, fromDate, toDate, endpoint, userType, dbType, isMcpRequest);
        }

        var hasFilters = false;
        IQueryable<Guid> matchedIds = query
            .Where(r => false)
            .Select(r => r.Id);

        if (fromDate.HasValue)
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.Timestamp >= fromDate.Value).Select(r => r.Id));
        }

        if (toDate.HasValue)
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.Timestamp <= toDate.Value).Select(r => r.Id));
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.Endpoint.Contains(endpoint)).Select(r => r.Id));
        }

        if (!string.IsNullOrWhiteSpace(userType))
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.UserType == userType).Select(r => r.Id));
        }

        if (!string.IsNullOrWhiteSpace(dbType))
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.DbType == dbType).Select(r => r.Id));
        }

        if (isMcpRequest.HasValue)
        {
            hasFilters = true;
            matchedIds = matchedIds.Concat(query.Where(r => r.IsMcpRequest == isMcpRequest.Value).Select(r => r.Id));
        }

        return hasFilters
            ? query.Where(r => matchedIds.Distinct().Contains(r.Id))
            : query;
    }

    private static string NormalizeOperator(string? filterOperator)
    {
        return string.Equals(filterOperator, "OR", StringComparison.OrdinalIgnoreCase) ? "OR" : "AND";
    }
}
