#!/usr/bin/env dotnet
#:package Microsoft.Extensions.Logging@10.0.0
#:package Microsoft.Extensions.Logging.Console@10.0.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;


// ============================================================================
// Main Entry Point (stdio JSON-RPC server)
// ============================================================================
// This MCP server communicates via stdin/stdout using JSON-RPC 2.0 protocol.
// CRITICAL: stdout must ONLY contain JSON responses - no logs or console output!

// IMPORTANT: Do NOT log to console (stdout) - it interferes with JSON-RPC communication!
// Logging is disabled completely to prevent "stdout pollution" which breaks MCP protocol.
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.None); // Disable all logging to avoid stdout pollution
});

var logger = loggerFactory.CreateLogger("DateTimeMcpServer");
var server = new McpDateTimeServer(logger);

using var stdin = Console.OpenStandardInput();
using var stdout = Console.OpenStandardOutput();
using var reader = new StreamReader(stdin);
using var writer = new StreamWriter(stdout) { AutoFlush = true }; // AutoFlush ensures immediate response delivery

// Main event loop - reads JSON-RPC requests from stdin and writes responses to stdout
while (true)
{
    try
    {
        var line = reader.ReadLine();

        // EOF (Ctrl+Z or pipe close) signals graceful shutdown
        if (line == null)
        {
            logger.LogInformation("EOF received, shutting down");
            break;
        }

        // Skip empty lines (MCP clients may send these)
        if (string.IsNullOrWhiteSpace(line)) continue;

        logger.LogDebug("Received: {Line}", line);

        // Deserialize JSON-RPC request using source-generated context (AOT-friendly)
        var request = JsonSerializer.Deserialize(line, McpJsonContext.Default.JsonRpcRequest)!;
        var response = server.HandleRequest(request);

        // Serialize response using source-generated context (eliminates IL2026/IL3050 warnings)
        var responseJson = JsonSerializer.Serialize(response, McpJsonContext.Default.JsonRpcResponse);
        logger.LogDebug("Sending: {Response}", responseJson);

        // Write response to stdout - this is the ONLY output that should go to stdout
        writer.WriteLine(responseJson);
    }
    catch (Exception ex)
    {
        // Return JSON-RPC error response for any unexpected errors
        logger.LogError(ex, "Error processing request");
        var error = new JsonRpcResponse("2.0", null, null,
            new JsonRpcError(-32700, "Parse error", null));
        var errorJson = JsonSerializer.Serialize(error, McpJsonContext.Default.JsonRpcResponse);
        writer.WriteLine(errorJson);
    }
}

logger.LogInformation("DateTime MCP Server stopped");

// ============================================================================
// JSON-RPC 2.0 Types (Model Context Protocol compatible)
// ============================================================================
// These records define the JSON-RPC 2.0 message structure used by MCP.
// JSON-RPC 2.0 Spec: https://www.jsonrpc.org/specification
// MCP Spec: https://modelcontextprotocol.io/

/// <summary>JSON-RPC 2.0 request message</summary>
record JsonRpcRequest(
    string Jsonrpc,         // Always "2.0"
    int? Id,                // Request ID (null for notifications)
    string Method,          // Method name (e.g., "tools/list", "tools/call")
    JsonElement? Params     // Optional parameters (varies by method)
);

/// <summary>JSON-RPC 2.0 response message</summary>
record JsonRpcResponse(
    string Jsonrpc,         // Always "2.0"
    int? Id,                // Matches request ID
    object? Result,         // Success result (null if Error is set)
    object? Error           // Error object (null if Result is set)
);

/// <summary>JSON-RPC 2.0 error object</summary>
record JsonRpcError(
    int Code,               // Error code (e.g., -32700 = Parse error)
    string Message,         // Human-readable error message
    object? Data            // Additional error information
);

/// <summary>MCP tool definition (returned by tools/list)</summary>
record McpToolDefinition(
    string Name,            // Tool name (e.g., "get_current_datetime")
    string Description,     // Human-readable description
    object InputSchema      // JSON Schema for tool parameters
);

/// <summary>MCP tools/list response</summary>
record McpListToolsResult(
    McpToolDefinition[] Tools  // Array of available tools
);

// ============================================================================
// JSON Source Generation Context (AOT-friendly)
// ============================================================================
// Source generation eliminates reflection-based serialization, enabling:
// - Native AOT compilation support
// - Zero IL2026/IL3050 warnings
// - Better performance
// - Smaller binary size
//
// ALL types that will be serialized MUST be registered here with [JsonSerializable].

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,     // Use camelCase for JSON
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)] // Omit null values
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(McpListToolsResult))]
[JsonSerializable(typeof(McpToolDefinition))]
[JsonSerializable(typeof(McpToolDefinition[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object>[]))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]  // Required for is_weekend tool (returns boolean)
internal partial class McpJsonContext : JsonSerializerContext
{
}

// ============================================================================
// DateTime Tools (MCP Tools Implementation)
// ============================================================================
// Business logic for datetime operations.
// Each method returns Dictionary<string, object> for flexible JSON serialization.

class DateTimeTools
{
    /// <summary>Get current date/time in specified timezone</summary>
    /// <param name="timezoneName">Timezone ID (e.g., 'Europe/Oslo', 'UTC'). Defaults to local.</param>
    public Dictionary<string, object> GetCurrentDateTime(string? timezoneName = null)
    {
        TimeZoneInfo tz;
        try
        {
            tz = string.IsNullOrWhiteSpace(timezoneName)
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(timezoneName);
        }
        catch
        {
            // Fallback to local timezone if specified timezone is invalid
            tz = TimeZoneInfo.Local;
        }

        var now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);

        return new Dictionary<string, object>
        {
            ["datetime"] = now.ToString("o"),  // ISO 8601 format
            ["date"] = now.ToString("yyyy-MM-dd"),
            ["time"] = now.ToString("HH:mm:ss"),
            ["timezone"] = tz.Id,
            ["dayOfWeek"] = now.ToString("dddd"),  // Full day name (culture-sensitive)
            ["weekNumber"] = System.Globalization.ISOWeek.GetWeekOfYear(now),
            ["year"] = now.Year,
            ["month"] = now.Month,
            ["day"] = now.Day
        };
    }

    /// <summary>Get current UTC timestamp in ISO 8601 format</summary>
    public string GetIso8601Timestamp() => DateTime.UtcNow.ToString("o");

    /// <summary>Add or subtract days from a date</summary>
    /// <param name="date">Starting date in YYYY-MM-DD format (defaults to today)</param>
    /// <param name="days">Number of days to add (negative to subtract)</param>
    public Dictionary<string, object> AddDays(string? date = null, int days = 0)
    {
        DateTime baseDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(date))
        {
            DateTime.TryParse(date, out baseDate);
        }

        var result = baseDate.AddDays(days);

        return new Dictionary<string, object>
        {
            ["original"] = baseDate.ToString("yyyy-MM-dd"),
            ["result"] = result.ToString("yyyy-MM-dd"),
            ["daysAdded"] = days,
            ["dayOfWeek"] = result.ToString("dddd")
        };
    }

    /// <summary>Check if a date falls on a weekend (Saturday or Sunday)</summary>
    /// <param name="date">Date to check in YYYY-MM-DD format (defaults to today)</param>
    public Dictionary<string, object> IsWeekend(string? date = null)
    {
        DateTime checkDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(date))
        {
            DateTime.TryParse(date, out checkDate);
        }

        var isWeekend = checkDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        return new Dictionary<string, object>
        {
            ["date"] = checkDate.ToString("yyyy-MM-dd"),
            ["dayOfWeek"] = checkDate.ToString("dddd"),
            ["isWeekend"] = isWeekend
        };
    }

    /// <summary>Get ISO 8601 week number for a date</summary>
    /// <param name="date">Date in YYYY-MM-DD format (defaults to today)</param>
    public Dictionary<string, object> GetWeekNumber(string? date = null)
    {
        DateTime checkDate = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(date))
        {
            DateTime.TryParse(date, out checkDate);
        }

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(checkDate);

        return new Dictionary<string, object>
        {
            ["date"] = checkDate.ToString("yyyy-MM-dd"),
            ["weekNumber"] = weekNumber,
            ["year"] = checkDate.Year
        };
    }
}

// ============================================================================
// MCP Server (JSON-RPC 2.0 over stdio)
// ============================================================================
// Handles JSON-RPC requests and routes them to appropriate tool methods.

class McpDateTimeServer(ILogger logger)
{
    private readonly DateTimeTools _tools = new();

    /// <summary>
    /// Returns list of available tools (called during MCP initialization).
    /// This is how the client discovers what tools this server provides.
    /// </summary>
    public McpListToolsResult ListTools() => new(
        [
            new McpToolDefinition(
                "get_current_datetime",
                "Get current date and time in specified timezone (default: local)",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["timezone"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Timezone name (e.g., 'Europe/Oslo', 'UTC')"
                        }
                    }
                }
            ),
            new McpToolDefinition(
                "get_iso8601_timestamp",
                "Get current timestamp in ISO 8601 format (UTC)",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()  // No parameters
                }
            ),
            new McpToolDefinition(
                "add_days",
                "Add or subtract days from a date",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["date"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Date in YYYY-MM-DD format (default: today)"
                        },
                        ["days"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Number of days to add (negative to subtract)"
                        }
                    },
                    ["required"] = new[] { "days" }  // 'days' parameter is required
                }
            ),
            new McpToolDefinition(
                "is_weekend",
                "Check if a date is a weekend (Saturday or Sunday)",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["date"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Date in YYYY-MM-DD format (default: today)"
                        }
                    }
                }
            ),
            new McpToolDefinition(
                "get_week_number",
                "Get ISO 8601 week number for a date",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["date"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Date in YYYY-MM-DD format (default: today)"
                        }
                    }
                }
            )
        ]
    );

    /// <summary>
    /// Routes tool call to appropriate method.
    /// Extracts parameters from JsonElement and converts to method arguments.
    /// </summary>
    public object CallTool(string toolName, JsonElement? parameters)
    {
        logger.LogInformation("Calling tool: {ToolName}", toolName);

        return toolName switch
        {
            "get_current_datetime" => _tools.GetCurrentDateTime(
                parameters?.TryGetProperty("timezone", out var tz) == true ? tz.GetString() : null
            ),

            "get_iso8601_timestamp" => _tools.GetIso8601Timestamp(),

            "add_days" => _tools.AddDays(
                parameters?.TryGetProperty("date", out var d) == true ? d.GetString() : null,
                parameters?.TryGetProperty("days", out var dy) == true ? dy.GetInt32() : 0
            ),

            "is_weekend" => _tools.IsWeekend(
                parameters?.TryGetProperty("date", out var wd) == true ? wd.GetString() : null
            ),

            "get_week_number" => _tools.GetWeekNumber(
                parameters?.TryGetProperty("date", out var wn) == true ? wn.GetString() : null
            ),

            _ => throw new Exception($"Unknown tool: {toolName}")
        };
    }

    /// <summary>
    /// Main JSON-RPC request handler.
    /// Routes requests to appropriate handler based on method name.
    /// </summary>
    public JsonRpcResponse HandleRequest(JsonRpcRequest request)
    {
        try
        {
            return request.Method switch
            {
                // Return list of available tools
                "tools/list" => new JsonRpcResponse("2.0", request.Id, ListTools(), null),

                // Execute a specific tool
                "tools/call" when request.Params.HasValue =>
                    HandleToolsCall(request),

                // MCP initialization handshake
                "initialize" => new JsonRpcResponse("2.0", request.Id, new Dictionary<string, object>
                {
                    ["protocolVersion"] = "2024-11-05",  // MCP protocol version
                    ["serverInfo"] = new Dictionary<string, object>
                    {
                        ["name"] = "datetime-mcp-server",
                        ["version"] = "1.0.0"
                    },
                    ["capabilities"] = new Dictionary<string, object>
                    {
                        ["tools"] = new Dictionary<string, object>()  // We support tools
                    }
                }, null),

                // Unknown method - return error
                _ => new JsonRpcResponse("2.0", request.Id, null,
                    new JsonRpcError(-32601, $"Method not found: {request.Method}", null))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling request");
            return new JsonRpcResponse("2.0", request.Id, null,
                new JsonRpcError(-32603, ex.Message, null));
        }
    }

    /// <summary>
    /// Handles tools/call requests.
    /// Extracts tool name and arguments, calls the tool, and wraps result in MCP format.
    /// </summary>
    private JsonRpcResponse HandleToolsCall(JsonRpcRequest request)
    {
        var p = request.Params!.Value;
        var toolName = p.GetProperty("name").GetString()!;
        var args = p.TryGetProperty("arguments", out var a) ? a : (JsonElement?)null;

        var result = CallTool(toolName, args);

        // MCP requires tool results to be wrapped in a content array with type and text
        var resultText = result switch
        {
            string str => str,
            Dictionary<string, object> dict => JsonSerializer.Serialize(dict, McpJsonContext.Default.DictionaryStringObject),
            _ => result?.ToString() ?? ""
        };
        
        // Wrap in MCP-compliant content structure
        var mcpResult = new Dictionary<string, object>
        {
            ["content"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = resultText
                }
            }
        };

        return new JsonRpcResponse("2.0", request.Id, mcpResult, null);
    }
}
