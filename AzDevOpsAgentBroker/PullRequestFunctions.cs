using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using AzDevOpsAgentBroker.Services;

namespace AzDevOpsAgentBroker
{
    public static class GetPullRequestDiff
    {
        [Function("GetPullRequestDiff")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            ILogger log = context.GetLogger("GetPullRequestDiff");
            log.LogInformation("GetPullRequestDiff triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonSerializer.Deserialize<PullRequestInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl, AzDoProject, or AzDoPat configuration.");

            var broker = new DevOpsBroker(orgUrl, pat);
            string diff = await broker.GetPullRequestDiff(project, input.RepoId, input.PrId);

            return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.OK, diff);
        }
    }

    public static class GetPullRequestDetails
    {
        [Function("GetPullRequestDetails")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            ILogger log = context.GetLogger("GetPullRequestDetails");
            log.LogInformation("GetPullRequestDetails triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonSerializer.Deserialize<PullRequestInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl, AzDoProject, or AzDoPat configuration.");

            var broker = new DevOpsBroker(orgUrl, pat);
            string details = await broker.GetPullRequestDetails(project, input.RepoId, input.PrId);

            return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.OK, details);
        }
    }

    public static class GetFileContent
    {
        [Function("GetFileContent")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            ILogger log = context.GetLogger("GetFileContent");
            log.LogInformation("GetFileContent triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonSerializer.Deserialize<FileContentInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || string.IsNullOrEmpty(input?.FilePath) || string.IsNullOrEmpty(input?.CommitId))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "RepoId, FilePath, and CommitId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl, AzDoProject, or AzDoPat configuration.");

            var broker = new DevOpsBroker(orgUrl, pat);
            string content = await broker.GetFileContent(project, input.RepoId, input.FilePath, input.CommitId);

            return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.OK, content);
        }
    }

    public static class PostPRComment
    {
        [Function("PostPRComment")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            ILogger log = context.GetLogger("PostPRComment");
            log.LogInformation("PostPRComment triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonSerializer.Deserialize<PostCommentInput>(requestBody);

            if (string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0 || string.IsNullOrEmpty(input?.Comment))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "RepoId, PrId, and Comment are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project ?? Environment.GetEnvironmentVariable("AzDoProject");

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl, AzDoProject, or AzDoPat configuration.");

            var broker = new DevOpsBroker(orgUrl, pat);
            await broker.PostPRComment(project, input.RepoId, input.PrId, input.Comment);

            return await FunctionResponses.CreateJsonResponse(req, HttpStatusCode.OK, new { success = true, message = "Comment posted successfully." });
        }
    }

    internal static class FunctionResponses
    {
        internal static async Task<HttpResponseData> CreateTextResponse(HttpRequestData req, HttpStatusCode statusCode, string content)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteStringAsync(content ?? string.Empty);
            return response;
        }

        internal static async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, HttpStatusCode statusCode, object payload)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(payload);
            return response;
        }
    }

    // Input models
    public class PullRequestInput
    {
        public string? Project { get; set; }
        public string? RepoId { get; set; }
        public int PrId { get; set; }
    }

    public class FileContentInput
    {
        public string? Project { get; set; }
        public string? RepoId { get; set; }
        public string? FilePath { get; set; }
        public string? CommitId { get; set; }
    }

    public class PostCommentInput
    {
        public string? Project { get; set; }
        public string? RepoId { get; set; }
        public int PrId { get; set; }
        public string? Comment { get; set; }
    }
}

