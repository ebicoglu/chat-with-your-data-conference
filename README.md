# About 

This repo is a demonstration of how humans can chat with their data using AI. To prove it works, there are 2 applications. Both are .NET demo projects that answers natural-language questions against a **SQLite** database. They read the live schema, asks an **OpenAI** model to produce SQL, runs the query (with retries if SQL fails), then optionally builds an **Excel** export, a chart as HTML, and suggests follow-up questions. Both projects use `chinook.db`. You can find more info about this database in the [Database section](#Database). Both projects use OpenAI's `gpt-5.4` model. And for the chart creation, they both use [Vega charts](https://github.com/vega/vega). For excel export they use [ClosedXML](https://www.nuget.org/packages/closedxml/). All these components are replacable, I just picked them because they're easy to use and show.

There are 2 projects in this repo.

* **Chat.Console**: This is the core and actual project. To strip the UI and other non-related stuff I created a simple console app, so that you can focus the real solution. So if you want to understand the pipeline check this one.

* **Talk.Web**: When people see event an amazing idea working in a simple console app, it's not giving the wow effect. That's why I want to show this idea in a web application. But still I kept this project simple and understandable. When you implement in your app you can do better I guess. For more technical info check out [Talk.Web ReadMe](/Talk.Web/README.md). 

  

  >  AI is not deterministic so, it might change the generated SQL query even you use the same input. So when your query works good, implement a save/load query section in your project. Users can save a query after it successfully runs. They can share the queries among the other users, etc...

# Prerequirements

* Install a **.NET 10** SDK.
* It's using OpenAI, set your own `OPENAI_API_KEY` environment variable value.

# Run the demo 

To run **Chat.Console** app;

```powershell
cd Chat.Console
$env:OPENAI_API_KEY = "sk-..."   # session only
dotnet run
```

## Demo questions based on Chinook DB

- which artists have the most albums?
- how many songs are there in each genre?
- write down the top 3 best-selling songs?
- which music track is related to festival?
- which countries generate the most sales, list only top 5?
- how many invoices were created per month in 2009? Show in YYYY-MM format.
- What is the monthly revenue trend over the last 24 months?
- Which are the top 10 genres by total revenue?
- What is the percentage share of total sales by country?
- Is there a relationship between number of items per invoice and invoice total amount?

## Application flow pipeline

1. Loads schema from SQLite database. Then caches the schema at the output folder's `Output/db-schema.txt`
2. User asks a question → model returns `SELECT` only
3. Runs the generated SQL query
4. On a failure, automatic retry mechanism works
5. Prints the table on the terminal window
6. Writes an Excel file at the output folder's `Output/excel.xlsx`
7. Generates related chart at the output folder's `Output/chart.html`
8. Suggests 3 follow-up questions


For debug environment, the output folder is `./bin/Debug/net10.0/Output/` (relative to the process working directory).


# Database

It's using a sample SQLite DB for demo. 
This database is called **Chinook DB**.
For more info about this little DB, read [about-db](about-db.md).

# Issues, Limitations & Improvements

- For large databases, split the schema into chunks
- If the table/column names are not user-friendly, use Table/Column comments in the database or create an extra table for more metadata to explain AI what's what. 

  - For example you can add comments like this:

    > COMMENT ON TABLE users IS 'Stores application users';
    > COMMENT ON COLUMN users.email IS 'User primary email address';

  - And read it back like this:
    > SELECT obj_description('users'::regclass);
    > SELECT col_description('users'::regclass, 2); -- column position
- Row-level user permissions are missing, so only power/admin users must use this with the current implementation.
- Multi-tenancy filtering is absent.
- I didn't figure out working in microservice architecture for this demo.
- Need to improve implementation for paged-listing and exporting large set of data.