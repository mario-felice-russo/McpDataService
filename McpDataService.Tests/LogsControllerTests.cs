using McpDataService.Controllers;
using McpDataService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpDataService.Tests;

public class LogsControllerTests
{
    [Fact]
    public void GetLogs_ReturnsCreatedLogFile_AndSupportsDetailEndpoint()
    {
        var controller = new LogsController(NullLogger<LogsController>.Instance);
        var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        var logFileName = $"test-{Guid.NewGuid():N}.log";
        var logFilePath = Path.Combine(logsDirectory, logFileName);
        File.WriteAllText(logFilePath, "riga-1\nriga-2");

        try
        {
            var listResult = controller.GetLogs(page: 1, pageSize: 100);
            var listOkResult = Assert.IsType<OkObjectResult>(listResult);
            var listPayload = TestHelpers.ToJsonElement(listOkResult.Value);
            var logs = listPayload.GetProperty("Logs").EnumerateArray().ToList();

            Assert.True(listPayload.GetProperty("Total").GetInt32() >= 1);
            Assert.Contains(logs, log => string.Equals(log.GetProperty("FileName").GetString(), logFileName, StringComparison.OrdinalIgnoreCase));

            var detailResult = controller.GetLogFile(logFileName);
            var detailOkResult = Assert.IsType<OkObjectResult>(detailResult);
            var detailPayload = TestHelpers.ToJsonElement(detailOkResult.Value);
            Assert.Equal(logFileName, detailPayload.GetProperty("fileName").GetString());
            Assert.Contains("riga-1", detailPayload.GetProperty("content").GetString());
        }
        finally
        {
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }
        }
    }

    [Fact]
    public void GetLogFile_ReturnsNotFound_WhenFileDoesNotExist()
    {
        var controller = new LogsController(NullLogger<LogsController>.Instance);

        var actionResult = controller.GetLogFile($"missing-{Guid.NewGuid():N}.log");

        Assert.IsType<NotFoundResult>(actionResult);
    }
}
