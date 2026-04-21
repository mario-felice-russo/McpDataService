using Microsoft.AspNetCore.Mvc;

namespace McpDataService.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logsPath))
        {
            return Ok(new { Total = 0, Page = page, PageSize = pageSize, Logs = Array.Empty<object>() });
        }

        var selectedFiles = Directory.GetFiles(logsPath, "*.log")
            .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var logFiles = selectedFiles
            .Select(f =>
            {
                var content = TryReadAllTextShared(f);
                var lines = content.Split('\n').Take(100).ToList();

                return new
                {
                    FileName = Path.GetFileName(f),
                    LastModified = System.IO.File.GetLastWriteTime(f),
                    Size = TryGetFileSize(f),
                    Content = lines
                };
            })
            .ToList();

        var total = Directory.GetFiles(logsPath, "*.log").Length;

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Logs = logFiles });
    }

    [HttpGet("{fileName}")]
    public IActionResult GetLogFile(string fileName)
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        var filePath = Path.Combine(logsPath, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var content = TryReadAllTextShared(filePath);
        return Ok(new { fileName, content });
    }

    private string TryReadAllTextShared(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to read log file {LogFile}.", filePath);
            return "[Log non disponibile temporaneamente: file in uso da un altro processo.]";
        }
    }

    private static long TryGetFileSize(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }
}
