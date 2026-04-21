using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McpDataService.Pages;

public class LogsModel : PageModel
{
    public List<LogFileInfo> LogFiles { get; set; } = new();
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; } = 1;

    public void OnGet(int page = 1, int pageSize = 10)
    {
        var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        CurrentPage = page;

        if (!Directory.Exists(logsPath))
        {
            return;
        }

        var files = Directory.GetFiles(logsPath, "*.log")
            .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
            .ToList();

        var total = files.Count;
        TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        LogFiles = files
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new LogFileInfo
            {
                FileName = Path.GetFileName(f),
                LastModified = System.IO.File.GetLastWriteTime(f),
                Size = new FileInfo(f).Length,
                Content = ReadAllTextShared(f)
            })
            .ToList();
    }

    private static string ReadAllTextShared(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return "[Log non disponibile temporaneamente: file in uso da un altro processo.]";
        }
    }
}

public class LogFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public string Content { get; set; } = string.Empty;
}