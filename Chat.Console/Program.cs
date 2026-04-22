using System.Text;
using Chat.Console.Services;
using Chat.Console.Services.ChartService;
using Chat.Console.Utils;

Console.OutputEncoding = Console.InputEncoding = Encoding.UTF8;
const string connectionString = "Data Source=Db/chinook.db;Version=3;Read Only=True;";

/*
which artists have the most albums?
how many songs are there in each genre?
write down the top 3 best-selling songs?
which music track is related to festival?
which countries generate the most sales, list only top 5?
how many invoices were created per month in 2009? Show in YYYY-MM format.
---
What is the monthly revenue trend in 2011?
Which are the top 10 genres by total revenue?
What is the percentage share of total sales by country (Top 8 countries + Other)?
Is there a relationship between number of items per invoice and invoice total amount

*** INVALID SQL on purpose to test retry mechanism ***
How does cumulative revenue growth change over time by month?
*/

try
{
    /* 0 - GET USER INPUT */
    ConsoleHelper.WriteInfo("0.) ASK SOMETHING ABOUT YOUR DATA...");
    string userInput = Console.ReadLine();


    /* 1 - GET THE DATABASE SCHEMA */
    ConsoleHelper.WriteInfo("1.) FETCHING DATABASE SCHEMA...");
    string dbSchema = await DbSchemaService.GetSchema(connectionString);
    Console.WriteLine("● Schema created → " + DbSchemaService.SchemaFilePath);


    /* 2 - ASK AI FOR a VALID SQL QUERY */
    ConsoleHelper.WriteInfo("2.) GENERATING SQL with AI...");
    string sql = await AiService.GenerateSqlQuery(userInput, dbSchema, connectionString);
    sql = Helpers.ExtractSqlQuery(sql);
    Console.WriteLine(sql);


    /* 3 - RUN THE QUERY */
    ConsoleHelper.WriteInfo("3.) EXECUTING THE SQL QUERY...");
    DbResult dbResult = await DbService.RunSqlQuery(userInput, sql, connectionString);
    if (dbResult.Fails)
    {
        /* 3.1 - QUERY IS NOT VALID. ASK AI AGAIN UNTIL WE FIND A WORKING ONE */
        dbResult = await RetryService.TryToFindWorkingSql(dbResult, maxRetry: 5, connectionString);
        if (dbResult.Fails)
        {
            //all attempts failed. Show the error and exit.
            ConsoleHelper.WriteError($"Error running SQL query: {dbResult.ErrorMessage}");
            Console.ReadKey();
            return;
        }
    }

    ConsoleHelper.Print(dbResult.DataTable);

    /*4 - EXPORT EXCEL */
    ConsoleHelper.WriteInfo("4.) GENERATING EXCEL REPORT...");
    var excelPath = ExcelService.GenerateExcel(dbResult.DataTable);
    Console.WriteLine("● Excel created → " + excelPath);

    /*5 - GENERATE CHART */
    ConsoleHelper.WriteInfo("5.) GENERATING THE CHART...");
    var chartResult = await ChartGenerationService.GenerateChartFromDataTable(dbResult.DataTable, userInput);
    if (chartResult.Success)
    {
        var chartPath = ChartTemplates.GenerateChartHtml(chartResult.VegaSpec, userInput);
        Console.WriteLine("● Chart created → " + chartPath);
    }

    /*7 - RECOMMEND NEW QUESTION IDEAS*/
    ConsoleHelper.WriteInfo("6.) RECOMMENDING NEW QUESTIONS...");
    await QuestionRecommendService.Recommend(dbSchema, userInput);


}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.ReadKey();