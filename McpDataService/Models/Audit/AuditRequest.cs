using System.ComponentModel.DataAnnotations;

namespace McpDataService.Models.Audit;

public class AuditRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RequestHeaders { get; set; } = string.Empty;
    public string RequestBody { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public int ExecutionTimeMs { get; set; }
    public string DbType { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string UserIdentifier { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsMcpRequest { get; set; }
    public List<AuditRequestParameter> Parameters { get; set; } = new();
}

public class AuditRequestParameter
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public AuditRequest Request { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}