using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AzDevOpsAgentBroker.Services
{
    public class DevOpsBroker
    {
        private readonly HttpClient _httpClient;
        private readonly string _orgUrl;
        private readonly string _pat;

        public DevOpsBroker(string orgUrl, string pat)
        {
            _httpClient = new HttpClient();
            _orgUrl = orgUrl.TrimEnd('/');
            _pat = pat;
        }

        public async Task<string> GetPullRequestDiff(string project, string repoId, int prId)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations?api-version=7.1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string iterations = await response.Content.ReadAsStringAsync();

            // Get changes from the latest iteration
            var iterationsObj = System.Text.Json.JsonDocument.Parse(iterations);
            int iterationCount = iterationsObj.RootElement.GetProperty("count").GetInt32();

            var changesUrl = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations/{iterationCount}/changes?api-version=7.1";
            var changesRequest = new HttpRequestMessage(HttpMethod.Get, changesUrl);
            changesRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var changesResponse = await _httpClient.SendAsync(changesRequest);
            changesResponse.EnsureSuccessStatusCode();
            return await changesResponse.Content.ReadAsStringAsync();
        }

        public async Task<string> GetPullRequestDetails(string project, string repoId, int prId)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}?api-version=7.1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetFileContent(string project, string repoId, string path, string commitId)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(path)}&versionDescriptor.version={commitId}&versionDescriptor.versionType=commit&api-version=7.1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return $"[Could not retrieve file: {response.StatusCode}]";

            return await response.Content.ReadAsStringAsync();
        }

        public async Task PostPRComment(string project, string repoId, int prId, string content, string? path = null, int? line = null)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/threads?api-version=7.1";

            object payload;

            if (!string.IsNullOrWhiteSpace(path) && line.HasValue)
            {
                payload = new
                {
                    comments = new[] { new { content, commentType = 1 } },
                    status = 1,
                    threadContext = new
                    {
                        filePath = path,
                        rightFileStart = new { line = line.Value, offset = 1 },
                        rightFileEnd = new { line = line.Value, offset = 1 }
                    }
                };
            }
            else
            {
                payload = new
                {
                    comments = new[] { new { content, commentType = 1 } },
                    status = 1
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
