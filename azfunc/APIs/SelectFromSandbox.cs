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
using System.Data;
using System.Linq;

namespace zkScoreTest.APIs
{
    public static class SelectFromSandbox
    {
        [FunctionName("Sandbox")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var id = req.Query["id"].First();
            try
            {
                var tbl = new DataTable();
                {
                    using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
                    conn.Open();
                    var query = $"select name, value from sandbox where id = {id};";
                    using var cmd = new SqlCommand(query, conn);
                    var result = await cmd.ExecuteReaderAsync();
                    tbl.Load(result);
                }
                if (tbl.Rows.Count == 0)
                {
                    throw new Exception($"Missing data for id={id}");
                }
                var topRow = tbl.Rows[0];
                var data = new {
                    id = id, 
                    name = (string)topRow[0], 
                    value = topRow[1] == DBNull.Value ? null : (double?)topRow[1] 
                };
                var jsonData = JsonConvert.SerializeObject(data);
                return new OkObjectResult(jsonData);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Fail to select data for id={id} due to  {ex.Message}");
            }
        }
    }
}
