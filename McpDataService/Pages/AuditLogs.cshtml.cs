using McpDataService.Data;
using McpDataService.Models.Audit;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace McpDataService.Pages;

public class AuditLogsModel : PageModel
{
    private readonly AuditDbContext _auditDb;

    public AuditLogsModel(AuditDbContext auditDb)
    {
        _auditDb = auditDb;
    }

    public List<AuditRequest> Requests { get; set; } = new();
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Endpoint { get; set; } = "";
    public string UserType { get; set; } = "";
    public string DbType { get; set; } = "";
    public bool? IsMcpRequest { get; set; }
    public string FilterOperator { get; set; } = "AND";

    public void OnGet(
        DateTime? fromDate,
        DateTime? toDate,
        string? endpoint,
        string? userType,
        string? dbType,
        bool? isMcpRequest,
        string filterOperator = "AND",
        int page = 1,
        int pageSize = 20)
    {
        FromDate = fromDate;
        ToDate = toDate;
        Endpoint = endpoint ?? "";
        UserType = userType ?? "";
        DbType = dbType ?? "";
        IsMcpRequest = isMcpRequest;
        FilterOperator = NormalizeOperator(filterOperator);
        PageSize = Math.Max(1, pageSize);

        var query = ApplyFilters(
            _auditDb.Requests.AsQueryable(),
            fromDate,
            toDate,
            endpoint,
            userType,
            dbType,
            isMcpRequest,
            FilterOperator);

        var total = query.Count();
        TotalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        CurrentPage = Math.Clamp(page, 1, TotalPages);

        Requests = query
            .OrderByDescending(r => r.Timestamp)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Include(r => r.Parameters)
            .ToList();
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
        if (!string.Equals(filterOperator, "OR", StringComparison.OrdinalIgnoreCase))
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
