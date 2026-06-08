using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DiplomasViewer.Models;

namespace DiplomasViewer.Services;

/// <summary>
/// Тонкая обёртка над GitHub API и raw-доступом к файлу данных.
/// Чтение для всех — анонимно по raw-URL; запись — через Contents API с токеном
/// (право на запись проверяет сам GitHub).
/// </summary>
public class GitHubClient
{
    private readonly HttpClient _http;
    private readonly GitHubOptions _opts;

    public GitHubClient(HttpClient http, GitHubOptions opts)
    {
        _http = http;
        _opts = opts;
    }

    private string RawUrl =>
        $"https://raw.githubusercontent.com/{_opts.Owner}/{_opts.Repo}/{_opts.Branch}/{_opts.DataPath}";

    private string ContentsUrl =>
        $"https://api.github.com/repos/{_opts.Owner}/{_opts.Repo}/contents/{_opts.DataPath}";

    /// <summary>Анонимное чтение данных по raw-URL. null, если файла ещё нет.</summary>
    public async Task<string?> GetRawAsync()
    {
        try
        {
            // cache-busting, чтобы свежие правки появлялись быстрее CDN-кэша
            var url = $"{RawUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var resp = await _http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Чтение через Contents API (с токеном): содержимое + sha. null, если файла нет.</summary>
    public async Task<(string Content, string Sha)?> GetWithShaAsync(string token)
    {
        using var req = NewApiRequest(HttpMethod.Get, $"{ContentsUrl}?ref={_opts.Branch}", token);
        using var resp = await _http.SendAsync(req);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<ContentsResponse>();
        if (payload?.Content is null) return null;
        var bytes = Convert.FromBase64String(payload.Content.Replace("\n", "").Replace("\r", ""));
        return (Encoding.UTF8.GetString(bytes), payload.Sha ?? "");
    }

    /// <summary>Коммитит новое содержимое файла. Возвращает sha созданного blob'а.</summary>
    public async Task<string> PutAsync(string json, string? sha, string token, string message)
    {
        var body = new PutRequest
        {
            Message = message,
            Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
            Branch = _opts.Branch,
            Sha = sha,
        };

        using var req = NewApiRequest(HttpMethod.Put, ContentsUrl, token);
        req.Content = JsonContent.Create(body, options: ApiJson);
        using var resp = await _http.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync();
            throw new GitHubApiException(resp.StatusCode, detail);
        }

        var result = await resp.Content.ReadFromJsonAsync<PutResponse>();
        return result?.Content?.Sha ?? "";
    }

    /// <summary>Проверка токена: возвращает логин и наличие права записи в репозиторий.</summary>
    public async Task<TokenInfo> ValidateTokenAsync(string token)
    {
        string? login = null;
        try
        {
            using var userReq = NewApiRequest(HttpMethod.Get, "https://api.github.com/user", token);
            using var userResp = await _http.SendAsync(userReq);
            if (userResp.StatusCode == HttpStatusCode.Unauthorized)
                return new TokenInfo(false, false, null, "Неверный токен.");
            if (userResp.IsSuccessStatusCode)
            {
                var user = await userResp.Content.ReadFromJsonAsync<UserResponse>();
                login = user?.Login;
            }

            using var repoReq = NewApiRequest(HttpMethod.Get,
                $"https://api.github.com/repos/{_opts.Owner}/{_opts.Repo}", token);
            using var repoResp = await _http.SendAsync(repoReq);
            if (repoResp.StatusCode == HttpStatusCode.NotFound)
                return new TokenInfo(true, false, login,
                    $"Репозиторий {_opts.Owner}/{_opts.Repo} недоступен по этому токену.");
            if (!repoResp.IsSuccessStatusCode)
                return new TokenInfo(true, false, login, "Не удалось проверить доступ к репозиторию.");

            var repo = await repoResp.Content.ReadFromJsonAsync<RepoResponse>();
            var canPush = repo?.Permissions?.Push == true;
            return new TokenInfo(true, canPush, login,
                canPush ? null : "У токена нет прав на запись в репозиторий.");
        }
        catch (HttpRequestException ex)
        {
            return new TokenInfo(false, false, null, $"Ошибка сети: {ex.Message}");
        }
    }

    private static HttpRequestMessage NewApiRequest(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // --- DTO для GitHub API ---
    private sealed class ContentsResponse
    {
        public string? Content { get; set; }
        public string? Sha { get; set; }
    }

    private sealed class PutRequest
    {
        public string Message { get; set; } = "";
        public string Content { get; set; } = "";
        public string Branch { get; set; } = "";
        public string? Sha { get; set; }
    }

    private sealed class PutResponse
    {
        public ContentInfo? Content { get; set; }
        public sealed class ContentInfo { public string? Sha { get; set; } }
    }

    private sealed class UserResponse { public string? Login { get; set; } }

    private sealed class RepoResponse
    {
        public PermissionsInfo? Permissions { get; set; }
        public sealed class PermissionsInfo { public bool Push { get; set; } }
    }
}

public record TokenInfo(bool ValidToken, bool CanPush, string? Login, string? Error)
{
    public bool IsAdmin => ValidToken && CanPush;
}

public class GitHubApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public GitHubApiException(HttpStatusCode status, string detail)
        : base($"GitHub API вернул {(int)status} {status}. {detail}")
    {
        StatusCode = status;
    }
}
