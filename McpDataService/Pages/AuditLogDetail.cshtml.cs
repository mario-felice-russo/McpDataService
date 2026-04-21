using McpDataService.Data;
using McpDataService.Models.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace McpDataService.Pages;

public class AuditLogDetailModel : PageModel
{
    private readonly AuditDbContext _auditDb;

    public AuditLogDetailModel(AuditDbContext auditDb) => _auditDb = auditDb;

    public new AuditRequest Request { get; set; } = null!;
    public string RequestHeaders { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public List<(string Key, string Value)> Parameters { get; set; } = new();

    public IActionResult OnGet(Guid id)
    {
        Request = _auditDb.Requests
            .Include(r => r.Parameters)
            .FirstOrDefault(r => r.Id == id) ?? null!;

        if (Request == null) return NotFound();

        RequestHeaders = Request.RequestHeaders;
        RequestBody = Request.RequestBody;
        ResponseBody = Request.ResponseBody;
        Parameters = Request.Parameters.Select(p => (p.Key, p.Value)).ToList();

        return Page();
    }
}