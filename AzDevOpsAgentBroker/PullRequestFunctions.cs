using AzDevOpsAgentBroker.Services;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using OpenAI.Responses;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

            if (string.IsNullOrWhiteSpace(input?.Project) || string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "Project, RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project;

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl or AzDoPat configuration.");

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

            if (string.IsNullOrWhiteSpace(input?.Project) || string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0)
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "Project, RepoId and PrId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project;

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl or AzDoPat configuration.");

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

            if (string.IsNullOrWhiteSpace(input?.Project) || string.IsNullOrEmpty(input?.RepoId) || string.IsNullOrEmpty(input?.FilePath) || string.IsNullOrEmpty(input?.CommitId))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "Project, RepoId, FilePath, and CommitId are required.");

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project;

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl or AzDoPat configuration.");

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

            if (string.IsNullOrWhiteSpace(input?.Project) || string.IsNullOrEmpty(input?.RepoId) || input.PrId <= 0 || string.IsNullOrEmpty(input?.Comment))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "Project, RepoId, PrId, and Comment are required.");

            if ((!string.IsNullOrWhiteSpace(input.Path) && !input.Line.HasValue) ||
                (string.IsNullOrWhiteSpace(input.Path) && input.Line.HasValue) ||
                (input.Line.HasValue && input.Line.Value <= 0))
            {
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "For inline comments, both Path and Line (> 0) are required.");
            }

            string orgUrl = Environment.GetEnvironmentVariable("AzDoOrgUrl");
            string pat = Environment.GetEnvironmentVariable("AzDoPat");
            string project = input.Project;

            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(pat))
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing AzDoOrgUrl or AzDoPat configuration.");

            var broker = new DevOpsBroker(orgUrl, pat);
            await broker.PostPRComment(project, input.RepoId, input.PrId, input.Comment, input.Path, input.Line);

            return await FunctionResponses.CreateJsonResponse(req, HttpStatusCode.OK, new { success = true, message = "Comment posted successfully." });
        }
    }

    //public static class ListFoundryAgents
    //{
    //    [Function("ListFoundryAgents")]
    //    public static async Task<HttpResponseData> Run(
    //        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
    //        FunctionContext context)
    //    {
    //        ILogger log = context.GetLogger("ListFoundryAgents");
    //        string? endpoint = Environment.GetEnvironmentVariable("FoundryProjectEndpoint");
    //        if (string.IsNullOrWhiteSpace(endpoint))
    //            return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "FoundryProjectEndpoint not configured.");

    //        log.LogInformation("Listing agents from Foundry endpoint: {Endpoint}", endpoint);

    //        var credential = FoundryHelper.CreateCredential();
    //        var projectClient = new AIProjectClient(new Uri(endpoint), credential);
    //        var agents = new System.Collections.Generic.List<object>();

    //        await foreach (var agent in projectClient.AgentAdministrationClient.GetAgentsAsync())
    //        {
    //            agents.Add(new { id = agent.Id, name = agent.Name ?? "(unnamed)" });
    //            log.LogInformation("Found agent -> Id: {Id} | Name: {Name}", agent.Id, agent.Name);
    //        }

    //        return await FunctionResponses.CreateJsonResponse(req, HttpStatusCode.OK, new
    //        {
    //            endpoint,
    //            agentCount = agents.Count,
    //            agents
    //        });
    //    }
    //}

    public static class PRWebhookReceiver
    {
        private static readonly string? ProjectEndpoint = FoundryHelper.GetEnvironmentValue("FoundryProjectEndpoint");
        private static readonly string? AgentName = FoundryHelper.GetEnvironmentValue("FoundryAgentName");

        [Function("PRWebhookReceiver")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            ILogger log = context.GetLogger("PRWebhookReceiver");
            log.LogInformation("Webhook received from Azure DevOps Pull Request trigger event.");

            if (string.IsNullOrWhiteSpace(ProjectEndpoint) || string.IsNullOrWhiteSpace(AgentName))
            {
                log.LogError("Missing Foundry configuration. FoundryProjectEndpoint and FoundryAgentName are required.");
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Missing FoundryProjectEndpoint and/or FoundryAgentName configuration.");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<PullRequestWebhookPayload>(requestBody);

            string? repoId = payload?.Resource?.Repository?.Id;
            int prId = payload?.Resource?.PullRequestId ?? 0;
            string? targetBranch = payload?.Resource?.TargetRefName;

            if (string.IsNullOrWhiteSpace(repoId) || prId <= 0)
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid webhook payload. Repository id and pullRequestId are required.");

            log.LogInformation("PR metadata parsed successfully -> PR ID: {PrId} | Repo GUID: {RepoId} | Target: {TargetBranch}", prId, repoId, targetBranch);

            try
            {
                var credential = FoundryHelper.CreateCredential();

                // Verify the portal agent exists via the Projects SDK
                var projectClient = new AIProjectClient(new Uri(ProjectEndpoint), credential);

                ProjectConversation conversation = projectClient.ProjectOpenAIClient.GetProjectConversationsClient().CreateProjectConversation();

                ProjectResponsesClient responsesClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(
                   defaultAgent: AgentName,
                   defaultConversationId: conversation.Id);

                // Invoke the agent via the Foundry Responses API (not the legacy Assistants API)
                string userPrompt = $"Please execute a thorough code quality and security review for repositoryId: '{repoId}' and pullRequestId: {prId}. " +
                                    "Autonomously invoke your connected OpenAPI tools to extract the git code diff changes, analyze the modified " +
                                    "lines against your Angular 8 and .NET Framework 4.8 core guidelines, and publish your code critiques directly to the PR thread.";


                var response = responsesClient.CreateResponse(userPrompt);

                log.LogInformation("Agent completed successfully via Responses API. {@OutputText}", response.Value.GetOutputText());

                return await FunctionResponses.CreateJsonResponse(req, HttpStatusCode.OK, new
                {
                    status = "Code review execution completed successfully.",
                    agentName = AgentName
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Critical error running the Agentic Review Orchestrator.");
                return await FunctionResponses.CreateTextResponse(req, HttpStatusCode.InternalServerError, "Failed to execute PR review agent.");
            }
        }
    }

    internal static class FoundryHelper
    {
        internal static Azure.Identity.DefaultAzureCredential CreateCredential()
        {
            string? tenantId = GetEnvironmentValue("AzureTenantId", "AZURE_TENANT_ID");
            return new Azure.Identity.DefaultAzureCredential(new Azure.Identity.DefaultAzureCredentialOptions
            {
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                ExcludeVisualStudioCredential = true,
                ExcludeSharedTokenCacheCredential = true
            });
        }

        internal static string? GetEnvironmentValue(params string[] names)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
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
        [JsonPropertyName("project")]
        public string? Project { get; set; }

        [JsonPropertyName("repoId")]
        public string? RepoId { get; set; }

        [JsonPropertyName("prId")]
        public int PrId { get; set; }
    }

    public class FileContentInput
    {
        [JsonPropertyName("project")]
        public string? Project { get; set; }

        [JsonPropertyName("repoId")]
        public string? RepoId { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("commitId")]
        public string? CommitId { get; set; }
    }

    public class PostCommentInput
    {
        [JsonPropertyName("project")]
        public string? Project { get; set; }

        [JsonPropertyName("repoId")]
        public string? RepoId { get; set; }

        [JsonPropertyName("prId")]
        public int PrId { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("line")]
        public int? Line { get; set; }
    }

    public class PullRequestWebhookPayload
    {
        [JsonPropertyName("resource")]
        public PullRequestWebhookResource? Resource { get; set; }
    }

    public class PullRequestWebhookResource
    {
        [JsonPropertyName("repository")]
        public PullRequestWebhookRepository? Repository { get; set; }

        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("targetRefName")]
        public string? TargetRefName { get; set; }
    }

    public class PullRequestWebhookRepository
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}

