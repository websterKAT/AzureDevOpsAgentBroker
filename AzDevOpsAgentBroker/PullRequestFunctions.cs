using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AzDevOpsAgentBroker.Services;

namespace AzDevOpsAgentBroker
{
    public static class GetPullRequestDiff
    {
        [FunctionName("GetPullRequestDiff")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("GetPullRequestDiff triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<PullRequestInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return new BadRequestObjectResult("RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string vaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            var broker = new DevOpsBroker(orgUrl);
            string diff = await broker.GetPullRequestDiff(project, input.RepoId, input.PrId, vaultUri);

            return new OkObjectResult(diff);
        }
    }

    public static class GetPullRequestDetails
    {
        [FunctionName("GetPullRequestDetails")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("GetPullRequestDetails triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<PullRequestInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return new BadRequestObjectResult("RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string vaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            var broker = new DevOpsBroker(orgUrl);
            string details = await broker.GetPullRequestDetails(project, input.RepoId, input.PrId, vaultUri);

            return new OkObjectResult(details);
        }
    }

    public static class GetFileContent
    {
        [FunctionName("GetFileContent")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("GetFileContent triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<FileContentInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || string.IsNullOrEmpty(input?.FilePath) || string.IsNullOrEmpty(input?.CommitId))
                return new BadRequestObjectResult("RepoId, FilePath, and CommitId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string vaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            var broker = new DevOpsBroker(orgUrl);
            string content = await broker.GetFileContent(project, input.RepoId, input.FilePath, input.CommitId, vaultUri);

            return new OkObjectResult(content);
        }
    }

    public static class PostPRComment
    {
        [FunctionName("PostPRComment")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("PostPRComment triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<PostCommentInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0 || string.IsNullOrEmpty(input?.Comment))
                return new BadRequestObjectResult("RepoId, PrId, and Comment are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string vaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            var broker = new DevOpsBroker(orgUrl);
            await broker.PostPRComment(project, input.RepoId, input.PrId, input.Comment, vaultUri);

            return new OkObjectResult(new { success = true, message = "Comment posted successfully." });
        }
    }

    // Input models
    public class PullRequestInput
    {
        public string Project { get; set; }
        public string RepoId { get; set; }
        public int PrId { get; set; }
    }

    public class FileContentInput
    {
        public string Project { get; set; }
        public string RepoId { get; set; }
        public string FilePath { get; set; }
        public string CommitId { get; set; }
    }

    public class PostCommentInput
    {
        public string Project { get; set; }
        public string RepoId { get; set; }
        public int PrId { get; set; }
        public string Comment { get; set; }
    }
}

