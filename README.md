# DateTime MCP Server

**Model Context Protocol (MCP) Server** for date/time operations - production-ready C# script for Visual Studio 2026 Copilot Chat.

---

## 🎯 Features

Provides 5 datetime tools via JSON-RPC 2.0:

- **get_current_datetime** - Get current date/time in any timezone
- **get_iso8601_timestamp** - Get UTC timestamp in ISO 8601 format
- **add_days** - Add or subtract days from a date
- **is_weekend** - Check if a date is a weekend
- **get_week_number** - Get ISO 8601 week number

## 🚀 Quick Start

### 1. Download

```bash
# Clone the repository
git clone https://github.com/eyjolfurgudnivatne/dotnet-mcp-datetime-server.git
cd dotnet-mcp-datetime-server
```

### 2. Configure

Create `.mcp.json` in your workspace root:

```json
{
  "inputs": [],
  "servers": {
    "datetime": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["DateTimeMcpServer.cs"],
      "env": {}
    }
  }
}
```

### 3. Activate in VS 2026

1. Open **Copilot Chat** in Visual Studio
2. Click the **tool icon** (⚙️ bottom of chat)
3. Expand **"datetime"** under **"Added"**
4. **Check** the tools you want to use
5. Start asking datetime questions!

## 📖 Example Usage

```
You: What's today's date?
Copilot: Today is Saturday, November 29, 2025 (week 48)

You: Is it weekend?
Copilot: Yes! It's Saturday, November 29, 2025

You: What day was 2 days ago?
Copilot: 2 days ago was Thursday, November 27, 2025
```

## 🔧 Requirements

- **.NET 10** or later
- **Visual Studio 2026** (November 2025 Feature Update or later)
- **GitHub Copilot** subscription

## 📝 Technical Highlights

- ✅ **Zero warnings** - IL2026/IL3050 eliminated with JSON Source Generation
- ✅ **AOT-friendly** - Full ahead-of-time compilation support
- ✅ **No build required** - Runs directly as C# script
- ✅ **Production-ready** - MCP-compliant with proper error handling
- ✅ **Stdout-safe** - No console logging pollution

## 🏗️ Extending

Add new tools in 4 steps:

1. Add method to `DateTimeTools` class
2. Add tool definition in `ListTools()`
3. Add case in `CallTool()` switch
4. Register return types in `McpJsonContext`

## 🐛 Troubleshooting

**Tools not appearing?**
- Restart Visual Studio
- Check `.mcp.json` path is relative to workspace root

**"Failed" when calling tools?**
- Ensure all return types registered in `McpJsonContext`
- Verify no console output (logging disabled)

**Server shows "Cached"?**
- Normal! Server starts on-demand when you call tools

## 📚 Resources

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [JSON-RPC 2.0 Spec](https://www.jsonrpc.org/specification)
- [.NET Source Generation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation)

## 🤝 Contributing

Contributions welcome! Please open an issue or PR.

## 📄 License

MIT License - free for any use, commercial or personal.

## 🎉 Acknowledgments

Built with ☕ and persistence during a Saturday research session.  
Special thanks to the debugging journey that made this possible!

---

**Author**: ARKo AS - AHelse Development Team  
**Date**: November 29, 2025  
**Status**: Production-ready ✅
