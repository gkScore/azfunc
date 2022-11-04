#nullable enable

using System;
using System.Web;
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
using System.Text.Json;
using System.Linq;

namespace azfunc.APIs
{
    public static class Reputations
    {
        [FunctionName("Reputations")]
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
                            var id = req.Query["evaluatee_id"];
                            if (id.Count != 1)
                            {
                                throw new Exception($"GET requires 'evaluatee_id' as a single parameter.");
                            }
                            return GetReputation(id.First());
                        }
                    case "POST":
                        {
                            log.LogInformation($"zkScore-API: '{method}' method is called.");
                            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                            var requestData = JsonDocument.Parse(requestBody).RootElement;
                            var reviewerAddress = !requestData.TryGetProperty("reviewer_address", out JsonElement reviewer_address_element)
                                    ? throw new Exception("POST requires 'reviewer_address' but is missing.")
                                    : reviewer_address_element.GetString()
                                        ?? throw new Exception("POST requires 'reviewer_address' string but is not a string.");
                            var evaluateeAddress = !requestData.TryGetProperty("evaluatee_address", out JsonElement evaluatee_address_element)
                                    ? throw new Exception("POST requires 'evaluatee_address' but is missing.")
                                    : evaluatee_address_element.GetString()
                                        ?? throw new Exception("POST requires 'evaluatee_address' string but is not a string.");
                            var score = !requestData.TryGetProperty("score", out JsonElement score_element)
                                    ? throw new Exception("POST requires 'score' but is missing.")
                                    : !score_element.TryGetDouble(out double scoreValue)
                                        ? throw new Exception("POST requires 'score' number but is not a number.")
                                        : scoreValue;

                            return await AddReputationTransaction(reviewerAddress, evaluateeAddress, score);
                        }
                    default:
                        throw new Exception($"invalid method '{method}' is called");
                }
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(
                    "Fail to call zkScore-api.Reputations. " +
                    $"method: {method}, error: {ex.GetType()},  message: {ex.Message}"
                );
            }
        }
        
        private static IActionResult GetReputation(string id)
        {
            var address = UserId.GetAddress(id) ?? throw new Exception($"User registration is not completed.");
            var record = GetReputationRecord(address) ?? throw new Exception($"Unexpected: User data is not found for id={id}.");
            var data = new
            {
                evaluatee_id = id,
                evaluatee_address = record.EvaluateeAddress,
                score = record.ReputationCount == 0 ? 0.0 : record.TotalScore / record.ReputationCount,
                score_count = record.ReputationCount
            };
            var jsonData = JsonConvert.SerializeObject(data);
            return new OkObjectResult(jsonData);
        }


        private static async Task<IActionResult> AddReputationTransaction(
            string reviewerAddress,
            string evaluateeAddress,
            double score
        )
        {
            evaluateeAddress = evaluateeAddress.Trim();

            // fetch current score
            double totalScore;
            int scoreCount;
            {
                var record = GetReputationRecord(evaluateeAddress) ?? throw new Exception("User registration is not compreleted");
                totalScore = record.TotalScore;
                scoreCount = record.ReputationCount;
            }

            //
            totalScore += score;
            scoreCount++;
            using (var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection")))
            {
                conn.Open();
                {
                    // insert reputation_transactions
                    var query =
                        "insert into reputation_transactions (reviewer_address, evaluatee_address, score, evaluated_time) " +
                        $"values ('{reviewerAddress}', '{evaluateeAddress}', {score}, GETUTCDATE());";
                    using var cmd = new SqlCommand(query, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                {
                    // update reputations
                    var query =
                        $"update reputations set total_score = {totalScore}, reputation_count = {scoreCount} " +
                        $"where evaluatee_address = '{evaluateeAddress}';";
                    using var cmd = new SqlCommand(query, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new OkObjectResult(
                $"Success to register reputation from '{reviewerAddress}' to '{evaluateeAddress}' with score={score}");
        }

        class ReputationsRecord
        {
            public string EvaluateeAddress { get; set; }
            public double TotalScore { get; set; }
            public int ReputationCount { get; set; }

            public ReputationsRecord(string evaluateeAddress, double score, int reputationCount)
            {
                EvaluateeAddress = evaluateeAddress;
                TotalScore = score;
                ReputationCount = reputationCount;
            }
        }

        private static ReputationsRecord? GetReputationRecord(string evaluateeAddress)
        {
            using var conn = new SqlConnection(Environment.GetEnvironmentVariable("sqldb_connection"));
            conn.Open();
            var tbl = new DataTable();
            {
                var query = $"select total_score, reputation_count from reputations where evaluatee_address = '{evaluateeAddress}';";
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
                return new ReputationsRecord(
                    evaluateeAddress,
                    (double)tbl.Rows[0][0],
                    (int)tbl.Rows[0][1]
                );
            }
        }
    }
}
