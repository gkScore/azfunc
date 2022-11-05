#nullable enable

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
using System.Net;
using System.Linq;
using System.Text.Json;

namespace azfunc.APIs
{
    public static class UserId
    {
        [FunctionName("UserId")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("zkScore-API: C# HTTP trigger function processed a request.");
            var method = req.Method.Trim().ToUpper();
            try
            {
                switch (method)
                {
                    case "GET":
                        {
                            log.LogInformation($"zkScore-API: '{method}' method is called.");
                            var walletAddress = req.Query["wallet_address"];
                            if (walletAddress.Count != 1)
                            {
                                throw new Exception("wallet_address must be a single parameter.");
                            }
                            return GetUserId(walletAddress.First());
                        }
                    case "POST":
                        {
                            log.LogInformation($"zkScore-API: '{method}' method is called.");
                            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                            var requestData = JsonDocument.Parse(requestBody).RootElement;

                            var walletAddress = !requestData.TryGetProperty("wallet_address", out JsonElement wallet_address_element)
                                    ? throw new Exception("POST requires 'wallet_address' but is missing.")
                                    : wallet_address_element.GetString()
                                        ?? throw new Exception("POST requires 'wallet_address' string but is not a string.");

                            return RegisterUser(walletAddress);
                        }
                    default:
                        throw new Exception($"invalid method '{method}' is called");
                }
            }
            catch (Exception ex)
            {
                var result = new { error = ex.GetType().FullName, message = ex.Message };
                var jsonData = JsonConvert.SerializeObject(result);
                return new BadRequestObjectResult(jsonData);
            }
        }

        private static IActionResult GetUserId(string address)
        {
            var maybeId = GetId(address);
            if (maybeId == null)
            {
                throw new Exception($"User resistration is not completed. for address '{address}'");
            }
            var result = new { user_id = maybeId.Value.ToString(), wallet_address = address };
            var jsonData = JsonConvert.SerializeObject(result);
            return new OkObjectResult(jsonData);
        }

        private static IActionResult RegisterUser(string address)
        { 
            var publishedId = InsertId(address);
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
            conn.Open();
            var query =
                "insert into reputations (evaluatee_address, total_score, reputation_count) " +
                $"values ('{address}', {0}, {0});";
            using var cmd = new SqlCommand(query, conn);
            cmd.ExecuteNonQuery();
            var result = new { user_id = publishedId.ToString(), wallet_address = address };
            var jsonData = JsonConvert.SerializeObject(result);
            return new OkObjectResult(jsonData);
        }

        public static long? GetId(string address)
        {
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
            conn.Open();
            var tbl = new DataTable();
            {
                var query = $"select user_id from user_master where wallet_address = '{address}';";
                using var cmd = new SqlCommand(query, conn);
                var result = cmd.ExecuteReader();
                tbl.Load(result);
            }
            if (tbl.Rows.Count == 0)
            {
                return null;
            }
            else
            {
                return (long)tbl.Rows[0][0];
            }
        }

        public static string? GetAddress(string idStr)
        {
            if (!long.TryParse(idStr, out long id))
            {
                throw new Exception("Invalid user id format.");
            }
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
            conn.Open();
            var tbl = new DataTable();
            {
                var query = $"select wallet_address from user_master where user_id = {id};";
                using var cmd = new SqlCommand(query, conn);
                var result = cmd.ExecuteReader();
                tbl.Load(result);
            }
            if (tbl.Rows.Count == 0)
            {
                return null;
            }
            else if (tbl.Rows.Count != 1)
            {
                throw new Exception("Unexpected exception because of duplicated id.");
            }
            {
                return (string)tbl.Rows[0][0];
            }
        }

        private static long InsertId(string address)
        {
            var maybeId = GetId(address);
            if (maybeId != null)
            {
                throw new Exception($"User is already registered. id={maybeId}");
            }
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
            conn.Open();
            var query =
                "insert into user_master (wallet_address) " +
                $"values ('{address}');";
            using var cmd = new SqlCommand(query, conn);
            cmd.ExecuteNonQuery();

            var publishedId = GetId(address);
            if (!publishedId.HasValue)
            {
                throw new Exception($"Fail to register user id.");
            }
            return publishedId.Value;
        }
    }
}
