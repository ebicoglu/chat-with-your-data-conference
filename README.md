# About This Project

A small **.NET console** demo that answers natural-language questions against a **SQLite** database. It reads the live schema, asks an **OpenAI** model to produce SQL, runs the query (with retries if SQL fails), then optionally builds an **Excel** export, a **Vega-Lite** chart as HTML, and suggests follow-up questions.

# Prerequirements

* Install a **.NET 10** SDK.
* It's using OpenAI, set your own `OPENAI_API_KEY` environment variable value.

# Run the demo

```powershell
cd Chat.Console.OpenAI
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

## What happens on screen (pipeline):

1. Load schema from SQLite
2. User question → model returns `SELECT` only
3. Run the query
4. On failure, automatic retry with feedback
5. Print table
6. Write Excel via ClosedXML
7. Generate related chart via Vega-Lite
8. Suggest follow-up questions

## Artifacts the app writes
The app generates charts, excels, db-schema files into the `Output` folder. 
For debug environment, it's `./bin/Debug/net10.0/Output/` (relative to the process working directory).


# Database

It's using a sample SQLite DB for demo. This database is called "Chinook DB". For more info read [about-db](about-db.md).

# Issues, Limitations & Improvements

- For large databases, split the schema into chunks
- If the table/column names are not user-friendly, use Table/Column comments in the database or create an extra table for more metadata to explain AI what's what. 

  - For example you can add comments like this:

    > COMMENT ON TABLE users IS 'Stores application users';
    > COMMENT ON COLUMN users.email IS 'User primary email address';

  - And read it back like this:
    > SELECT obj_description('users'::regclass);
    > SELECT col_description('users'::regclass, 2); -- column position
- Row-level user permissions
- Multi-tenancy filtering
- Working in microservice architecture
- Paged listing and exporting big data