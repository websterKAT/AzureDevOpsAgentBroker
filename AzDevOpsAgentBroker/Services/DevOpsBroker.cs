using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

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
            // 1. Get latest iteration
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations?api-version=7.1";
            var iterationsJson = await SendGetRequest(url);

            var iterationsObj = JsonDocument.Parse(iterationsJson);
            int iterationCount = iterationsObj.RootElement.GetProperty("count").GetInt32();

            // 2. Get change entries from the latest iteration
            var changesUrl = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}/iterations/{iterationCount}/changes?api-version=7.1";
            var changesJson = await SendGetRequest(changesUrl);

            var changesDoc = JsonDocument.Parse(changesJson);
            var changeEntries = changesDoc.RootElement.GetProperty("changeEntries");

            // 3. For each changed file, fetch blobs and compute diff
            var filesChanged = new List<object>();

            foreach (var entry in changeEntries.EnumerateArray())
            {
                var item = entry.GetProperty("item");
                string path = item.GetProperty("path").GetString() ?? "";
                string objectId = item.GetProperty("objectId").GetString() ?? "";

                // Determine change type
                string changeType = "edit";
                if (entry.TryGetProperty("changeType", out var ct))
                {
                    if (ct.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        changeType = ct.GetString()?.ToLowerInvariant() ?? "edit";
                    }
                    else if (ct.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        changeType = ct.GetInt32() switch
                        {
                            1 => "add",
                            2 => "edit",
                            16 => "delete",
                            18 => "rename",
                            _ => "edit"
                        };
                    }
                }

                // Skip folder entries (no objectId or path ends with /)
                if (string.IsNullOrEmpty(objectId) || path.EndsWith("/"))
                {
                    continue;
                }

                string diff;
                try
                {
                    if (changeType == "add")
                    {
                        string newContent = await GetBlobContent(project, repoId, objectId);
                        diff = FormatAsAddition(newContent);
                    }
                    else if (changeType == "delete")
                    {
                        string originalObjectId = item.TryGetProperty("originalObjectId", out var origProp)
                            ? origProp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(originalObjectId))
                        {
                            string oldContent = await GetBlobContent(project, repoId, originalObjectId);
                            diff = FormatAsDeletion(oldContent);
                        }
                        else
                        {
                            diff = "[File deleted - original content unavailable]";
                        }
                    }
                    else
                    {
                        // edit or rename: compute diff between original and new blob
                        string originalObjectId = item.TryGetProperty("originalObjectId", out var origProp)
                            ? origProp.GetString() ?? "" : "";

                        if (!string.IsNullOrEmpty(originalObjectId))
                        {
                            string oldContent = await GetBlobContent(project, repoId, originalObjectId);
                            string newContent = await GetBlobContent(project, repoId, objectId);
                            diff = ComputeUnifiedDiff(oldContent, newContent);
                        }
                        else
                        {
                            string newContent = await GetBlobContent(project, repoId, objectId);
                            diff = FormatAsAddition(newContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    diff = $"[Error computing diff: {ex.Message}]";
                }

                filesChanged.Add(new { path, changeType, diff });
            }

            var result = new { pullRequestId = prId, filesChanged };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task<string> GetBlobContent(string project, string repoId, string objectId)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/blobs/{objectId}?api-version=7.1&$format=text";
            return await SendGetRequest(url);
        }

        private static string ComputeUnifiedDiff(string oldText, string newText)
        {
            const int contextLines = 3;
            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(oldText, newText);
            var lines = diff.Lines;

            // Identify which line indices have changes
            var changeIndices = new HashSet<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Type != ChangeType.Unchanged)
                    changeIndices.Add(i);
            }

            // Include context lines around each change
            var includeIndices = new HashSet<int>();
            foreach (int idx in changeIndices)
            {
                for (int c = Math.Max(0, idx - contextLines); c <= Math.Min(lines.Count - 1, idx + contextLines); c++)
                    includeIndices.Add(c);
            }

            // Track line numbers in both old and new files
            int oldLineNum = 0;
            int newLineNum = 0;
            var sb = new StringBuilder();
            bool inHunk = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // Advance line counters regardless of inclusion
                int currentOldLine = oldLineNum;
                int currentNewLine = newLineNum;
                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        newLineNum++;
                        currentNewLine = newLineNum;
                        break;
                    case ChangeType.Deleted:
                        oldLineNum++;
                        currentOldLine = oldLineNum;
                        break;
                    case ChangeType.Modified:
                        oldLineNum++;
                        newLineNum++;
                        currentOldLine = oldLineNum;
                        currentNewLine = newLineNum;
                        break;
                    case ChangeType.Unchanged:
                        oldLineNum++;
                        newLineNum++;
                        currentOldLine = oldLineNum;
                        currentNewLine = newLineNum;
                        break;
                }

                if (!includeIndices.Contains(i))
                {
                    if (inHunk)
                    {
                        sb.AppendLine("...");
                        inHunk = false;
                    }
                    continue;
                }

                inHunk = true;
                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        sb.AppendLine($"L{currentNewLine}: + {line.Text}");
                        break;
                    case ChangeType.Deleted:
                        sb.AppendLine($"     - {line.Text}");
                        break;
                    case ChangeType.Modified:
                        sb.AppendLine($"     - {line.Text}");
                        sb.AppendLine($"L{currentNewLine}: + {line.Text}");
                        break;
                    case ChangeType.Unchanged:
                        sb.AppendLine($"L{currentNewLine}:   {line.Text}");
                        break;
                }
            }
            return sb.ToString();
        }

        private static string FormatAsAddition(string content)
        {
            var sb = new StringBuilder();
            int lineNum = 0;
            foreach (var line in content.Split('\n'))
            {
                lineNum++;
                sb.AppendLine($"L{lineNum}: + {line.TrimEnd('\r')}");
            }
            return sb.ToString();
        }

        private static string FormatAsDeletion(string content)
        {
            var sb = new StringBuilder();
            foreach (var line in content.Split('\n'))
            {
                sb.AppendLine($"     - {line.TrimEnd('\r')}");
            }
            return sb.ToString();
        }

        private async Task<string> SendGetRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetPullRequestDetails(string project, string repoId, int prId)
        {
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/pullRequests/{prId}?api-version=7.1";
            return await SendGetRequest(url);
        }

        public async Task<string> GetFileContent(string project, string repoId, string path, string commitId)
        {
            // Try as commit version first
            var url = $"{_orgUrl}/{project}/_apis/git/repositories/{repoId}/items?path={path}&versionDescriptor.version={commitId}&versionDescriptor.versionType=commit&api-version=7.1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}")));

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            // Fallback: treat commitId as a blob objectId
            try
            {
                return await GetBlobContent(project, repoId, commitId);
            }
            catch
            {
                return $"[Could not retrieve file: {response.StatusCode}]";
            }
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
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
