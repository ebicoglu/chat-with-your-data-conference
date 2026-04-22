# Talk.Web - Talk to Your Data

This is a .NET 10 Blazor Server app that lets you ask questions about data in natural language, generate SQL, run it on SQLite, and view the result as a table or chart.

## What It Does

- Converts user questions to SQLite `SELECT` queries.
- Executes queries safely on the bundled Chinook database (`Db/chinook.db`).
- Supports voice conversation for real-time, spoken query flow.
- Shows query results as a data table and optional Vega-Lite chart.
- Exports results to Excel.

## Important Components

- `Components/Pages/Home.razor`  
  Main UI: prompt input, SQL editor, run/generate actions, table/chart tabs, voice controls.
- `Services/AiService.cs`  
  Generates SQL from user input and can regenerate SQL when execution fails.
- `Services/DbService.cs`  
  Runs SQL against SQLite with simple safety checks (read-only query policy).
- `RealtimeConversationManager.cs`  
  Handles real-time voice session and tool-calling flow.
- `Services/ChartService/*`  
  Recommends chart type and generates Vega-Lite specs from query results.

## AI Models 

- `gpt-realtime-1.5`  
  Used for real-time voice interaction.
- `gpt-5.4`  
  Used for SQL generation and chart-type recommendation through chat completions.

## Quick Start

1. Set API key:
   - Windows PowerShell: `setx OPENAI_API_KEY "your_api_key"`
2. Run the app:
   - `dotnet run`
3. Open the local URL shown in terminal.

## Notes

- Target framework: `.NET 10`.
- Database file is included in the project and copied to output on build.
