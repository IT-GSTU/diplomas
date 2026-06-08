using System.Net;
using System.Text;
using System.Text.Json;
using DiplomasViewer.Models;
using DiplomasViewer.Services;
using DiplomasViewer.Tests.TestSupport;

namespace DiplomasViewer.Tests.Services;

public class GitHubClientTests
{
    private static readonly GitHubOptions Options = new()
    {
        Owner = "IT-GSTU",
        Repo = "diplomas",
        Branch = "main",
        DataPath = "data/diplomas.json",
    };

    private static GitHubClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new FakeHttpMessageHandler(responder)), Options);

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetRawAsync_returns_content_on_success()
    {
        var client = CreateClient(req =>
        {
            Assert.StartsWith(
                "https://raw.githubusercontent.com/IT-GSTU/diplomas/main/data/diplomas.json",
                req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        });

        Assert.Equal("[]", await client.GetRawAsync());
    }

    [Fact]
    public async Task GetRawAsync_returns_null_when_file_missing()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await client.GetRawAsync());
    }

    [Fact]
    public async Task GetRawAsync_returns_null_on_network_failure()
    {
        var client = CreateClient(_ => throw new HttpRequestException("network down"));

        Assert.Null(await client.GetRawAsync());
    }

    [Fact]
    public async Task GetWithShaAsync_decodes_base64_content_and_returns_sha()
    {
        const string jsonContent = "[{\"id\":\"abc\"}]";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));
        var wrappedWithNewlines = base64.Insert(base64.Length / 2, "\n"); // GitHub wraps base64 content with newlines

        var client = CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal(
                "https://api.github.com/repos/IT-GSTU/diplomas/contents/data/diplomas.json?ref=main",
                req.RequestUri!.ToString());
            Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", req.Headers.Authorization!.Parameter);

            return Json(HttpStatusCode.OK, new { content = wrappedWithNewlines, sha = "deadbeef" });
        });

        var result = await client.GetWithShaAsync("test-token");

        Assert.NotNull(result);
        Assert.Equal(jsonContent, result!.Value.Content);
        Assert.Equal("deadbeef", result.Value.Sha);
    }

    [Fact]
    public async Task GetWithShaAsync_returns_null_when_file_missing()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await client.GetWithShaAsync("token"));
    }

    [Fact]
    public async Task PutAsync_sends_base64_payload_with_message_branch_and_sha()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;

        var client = CreateClient(req =>
        {
            captured = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, new { content = new { sha = "newsha123" } });
        });

        var sha = await client.PutAsync("[]", "oldsha", "test-token", "Изменена работа: Иванов");

        Assert.Equal("newsha123", sha);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal(
            "https://api.github.com/repos/IT-GSTU/diplomas/contents/data/diplomas.json",
            captured.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("Изменена работа: Иванов", root.GetProperty("message").GetString());
        Assert.Equal("oldsha", root.GetProperty("sha").GetString());
        Assert.Equal("main", root.GetProperty("branch").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("[]")), root.GetProperty("content").GetString());
    }

    [Fact]
    public async Task PutAsync_omits_sha_when_creating_a_new_file()
    {
        string? capturedBody = null;

        var client = CreateClient(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, new { content = new { sha = "firstsha" } });
        });

        await client.PutAsync("[]", sha: null, "test-token", "Создан файл данных");

        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.False(doc.RootElement.TryGetProperty("sha", out _));
    }

    [Fact]
    public async Task PutAsync_throws_GitHubApiException_with_status_and_body_on_failure()
    {
        var client = CreateClient(_ => Json(HttpStatusCode.Conflict, new { message = "sha mismatch" }));

        var ex = await Assert.ThrowsAsync<GitHubApiException>(
            () => client.PutAsync("[]", "stale-sha", "token", "msg"));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Contains("sha mismatch", ex.Message);
    }

    [Fact]
    public async Task ValidateTokenAsync_reports_invalid_token()
    {
        var client = CreateClient(req =>
        {
            Assert.Equal("https://api.github.com/user", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var info = await client.ValidateTokenAsync("bad-token");

        Assert.False(info.ValidToken);
        Assert.False(info.CanPush);
        Assert.False(info.IsAdmin);
        Assert.Null(info.Login);
        Assert.Equal("Неверный токен.", info.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_reports_when_repo_is_not_accessible_with_this_token()
    {
        var client = CreateClient(req => req.RequestUri!.ToString() switch
        {
            "https://api.github.com/user" => Json(HttpStatusCode.OK, new { login = "octocat" }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var info = await client.ValidateTokenAsync("token-no-repo-access");

        Assert.True(info.ValidToken);
        Assert.False(info.CanPush);
        Assert.False(info.IsAdmin);
        Assert.Equal("octocat", info.Login);
        Assert.Contains("IT-GSTU/diplomas", info.Error);
        Assert.Contains("недоступен", info.Error);
    }

    [Fact]
    public async Task ValidateTokenAsync_reports_admin_when_token_can_push()
    {
        var client = CreateClient(req => req.RequestUri!.ToString() switch
        {
            "https://api.github.com/user" => Json(HttpStatusCode.OK, new { login = "admin-user" }),
            _ => Json(HttpStatusCode.OK, new { permissions = new { push = true } }),
        });

        var info = await client.ValidateTokenAsync("admin-token");

        Assert.True(info.ValidToken);
        Assert.True(info.CanPush);
        Assert.True(info.IsAdmin);
        Assert.Null(info.Error);
        Assert.Equal("admin-user", info.Login);
    }

    [Fact]
    public async Task ValidateTokenAsync_reports_read_only_when_token_cannot_push()
    {
        var client = CreateClient(req => req.RequestUri!.ToString() switch
        {
            "https://api.github.com/user" => Json(HttpStatusCode.OK, new { login = "reader" }),
            _ => Json(HttpStatusCode.OK, new { permissions = new { push = false } }),
        });

        var info = await client.ValidateTokenAsync("reader-token");

        Assert.True(info.ValidToken);
        Assert.False(info.CanPush);
        Assert.False(info.IsAdmin);
        Assert.Equal("У токена нет прав на запись в репозиторий.", info.Error);
    }
}
