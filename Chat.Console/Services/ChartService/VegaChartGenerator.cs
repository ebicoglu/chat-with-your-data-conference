using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chat.Console.Services.ChartService
{
    public class VegaChartGenerator
    {
        private readonly Dictionary<string, Func<DataTable, string, VegaSpec>> _chartGenerators;

        public VegaChartGenerator()
        {
            _chartGenerators = new Dictionary<string, Func<DataTable, string, VegaSpec>>
            {
                ["bar"] = GenerateBarChart,
                ["line"] = GenerateLineChart,
                ["scatter"] = GenerateScatterChart,
                ["pie"] = GeneratePieChart,
                ["area"] = GenerateAreaChart
            };
        }

        public class VegaSpec
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; } = "https://vega.github.io/schema/vega-lite/v5.json";
            
            [JsonPropertyName("title")]
            public VegaTitle Title { get; set; }
            
            [JsonPropertyName("data")]
            public VegaData Data { get; set; }
            
            [JsonPropertyName("mark")]
            public object Mark { get; set; }
            
            [JsonPropertyName("encoding")]
            public VegaEncoding Encoding { get; set; }
            
            [JsonPropertyName("width")]
            public int Width { get; set; } = 1500;
            
            [JsonPropertyName("height")]
            public int Height { get; set; } = 600;
        }

        public class VegaTitle
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }
            
            [JsonPropertyName("fontSize")]
            public int FontSize { get; set; } = 16;
        }

        public class VegaData
        {
            [JsonPropertyName("values")]
            public List<Dictionary<string, object>> Values { get; set; }
        }

        public class VegaEncoding
        {
            [JsonPropertyName("x")]
            public VegaChannel X { get; set; }
            
            [JsonPropertyName("y")]
            public VegaChannel Y { get; set; }
            
            [JsonPropertyName("color")]
            public VegaChannel Color { get; set; }
            
            [JsonPropertyName("theta")]
            public VegaChannel Theta { get; set; }
        }

        public class VegaChannel
        {
            [JsonPropertyName("field")]
            public string Field { get; set; }
            
            [JsonPropertyName("type")]
            public string Type { get; set; }
            
            [JsonPropertyName("title")]
            public string Title { get; set; }
            
            [JsonPropertyName("aggregate")]
            public string Aggregate { get; set; }
        }

        /// <summary>
        /// Main method to generate Vega chart from SQL results
        /// </summary>
        public string GenerateChart(DataTable dataTable, string chartType, string title = null)
        {
            if (!_chartGenerators.ContainsKey(chartType.ToLower()))
            {
                throw new ArgumentException($"Unsupported chart type: {chartType}");
            }

            VegaSpec vegaSpec = _chartGenerators[chartType.ToLower()](dataTable, title);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            return JsonSerializer.Serialize(vegaSpec, options);
        }

        /// <summary>
        /// Converts DataTable to Vega data format
        /// </summary>
        private List<Dictionary<string, object>> ConvertDataTableToVegaData(DataTable dataTable)
        {
            var result = new List<Dictionary<string, object>>();
            
            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    var value = row[col];
                    dict[col.ColumnName] = value == DBNull.Value ? null : value;
                }
                result.Add(dict);
            }
            
            return result;
        }

        /// <summary>
        /// Infers data type for Vega encoding
        /// </summary>
        private string InferDataType(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                type == typeof(double) || type == typeof(decimal))
                return "quantitative";
            
            if (type == typeof(DateTime))
                return "temporal";
                
            return "nominal";
        }

        private VegaSpec GenerateBarChart(DataTable data, string title)
        {
            var columns = data.Columns.Cast<DataColumn>().ToList();
            var categoricalCol = columns.FirstOrDefault(c => InferDataType(c.DataType) == "nominal");
            var numericCol = columns.FirstOrDefault(c => InferDataType(c.DataType) == "quantitative");

            return new VegaSpec
            {
                Title = title != null ? new VegaTitle { Text = title } : null,
                Data = new VegaData { Values = ConvertDataTableToVegaData(data) },
                Mark = "bar",
                Encoding = new VegaEncoding
                {
                    X = new VegaChannel 
                    { 
                        Field = categoricalCol?.ColumnName ?? columns[0].ColumnName,
                        Type = InferDataType(categoricalCol?.DataType ?? columns[0].DataType),
                        Title = categoricalCol?.ColumnName ?? columns[0].ColumnName
                    },
                    Y = new VegaChannel 
                    { 
                        Field = numericCol?.ColumnName ?? columns[1].ColumnName,
                        Type = InferDataType(numericCol?.DataType ?? columns[1].DataType),
                        Title = numericCol?.ColumnName ?? columns[1].ColumnName
                    }
                }
            };
        }

        private VegaSpec GenerateLineChart(DataTable data, string title)
        {
            var columns = data.Columns.Cast<DataColumn>().ToList();
            var xCol = columns[0];
            var yCol = columns[1];

            return new VegaSpec
            {
                Title = title != null ? new VegaTitle { Text = title } : null,
                Data = new VegaData { Values = ConvertDataTableToVegaData(data) },
                Mark = new { type = "line", point = true },
                Encoding = new VegaEncoding
                {
                    X = new VegaChannel 
                    { 
                        Field = xCol.ColumnName,
                        Type = InferDataType(xCol.DataType),
                        Title = xCol.ColumnName
                    },
                    Y = new VegaChannel 
                    { 
                        Field = yCol.ColumnName,
                        Type = InferDataType(yCol.DataType),
                        Title = yCol.ColumnName
                    }
                }
            };
        }

        private VegaSpec GenerateScatterChart(DataTable data, string title)
        {
            var columns = data.Columns.Cast<DataColumn>().ToList();
            var xCol = columns[0];
            var yCol = columns[1];
            var colorCol = columns.Count > 2 ? columns[2] : null;

            var encoding = new VegaEncoding
            {
                X = new VegaChannel 
                { 
                    Field = xCol.ColumnName,
                    Type = InferDataType(xCol.DataType),
                    Title = xCol.ColumnName
                },
                Y = new VegaChannel 
                { 
                    Field = yCol.ColumnName,
                    Type = InferDataType(yCol.DataType),
                    Title = yCol.ColumnName
                }
            };

            if (colorCol != null)
            {
                encoding.Color = new VegaChannel
                {
                    Field = colorCol.ColumnName,
                    Type = InferDataType(colorCol.DataType),
                    Title = colorCol.ColumnName
                };
            }

            return new VegaSpec
            {
                Title = title != null ? new VegaTitle { Text = title } : null,
                Data = new VegaData { Values = ConvertDataTableToVegaData(data) },
                Mark = "circle",
                Encoding = encoding
            };
        }

        private VegaSpec GeneratePieChart(DataTable data, string title)
        {
            var columns = data.Columns.Cast<DataColumn>().ToList();
            var labelCol = columns[0];
            var valueCol = columns[1];

            return new VegaSpec
            {
                Title = title != null ? new VegaTitle { Text = title } : null,
                Data = new VegaData { Values = ConvertDataTableToVegaData(data) },
                Mark = "arc",
                Encoding = new VegaEncoding
                {
                    Theta = new VegaChannel
                    {
                        Field = valueCol.ColumnName,
                        Type = "quantitative"
                    },
                    Color = new VegaChannel
                    {
                        Field = labelCol.ColumnName,
                        Type = "nominal"
                    }
                }
            };
        }

        private VegaSpec GenerateAreaChart(DataTable data, string title)
        {
            var columns = data.Columns.Cast<DataColumn>().ToList();
            var xCol = columns[0];
            var yCol = columns[1];

            return new VegaSpec
            {
                Title = title != null ? new VegaTitle { Text = title } : null,
                Data = new VegaData { Values = ConvertDataTableToVegaData(data) },
                Mark = "area",
                Encoding = new VegaEncoding
                {
                    X = new VegaChannel 
                    { 
                        Field = xCol.ColumnName,
                        Type = InferDataType(xCol.DataType),
                        Title = xCol.ColumnName
                    },
                    Y = new VegaChannel 
                    { 
                        Field = yCol.ColumnName,
                        Type = InferDataType(yCol.DataType),
                        Title = yCol.ColumnName
                    }
                }
            };
        }
    }
}