using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;

namespace zkScoreTest.APIs
{
    public static class InitializeSandbox
    {
        struct RecordItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public double? Value { get; set; }
        }

        [FunctionName("InitializeSandbox")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var connstr = Environment.GetEnvironmentVariable("sqldb_connection");
            try
            {
                using var conn = new SqlConnection(connstr);
                conn.Open();
                {
                    var query = "drop table if exists sandbox;";
                    using var cmd = new SqlCommand(query, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                {
                    var query = "create table sandbox ("
                        + "id int primary key,"
                        + "name nvarchar(50) not null,"
                        + "value float"
                        + ");";
                    using var cmd = new SqlCommand(query, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                var records = new RecordItem[]
                {
                    new RecordItem() { Id = 1, Name = "hoge", Value = 3.14 },
                    new RecordItem() { Id = 2, Name = "mario", Value = null },
                    new RecordItem() { Id = 3, Name = "fuga", Value = 42 },
                };
                foreach (var record in records)
                {
                    var query = $"insert into sandbox values ({record.Id}, '{record.Name}', {record.Value?.ToString() ?? "NULL"})";
                    using var cmd = new SqlCommand(query, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                return new OkObjectResult("Success to initialize sandbox");
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Fail to initialize sandbox due to {ex.Message}");
            }
        }
    }
}
