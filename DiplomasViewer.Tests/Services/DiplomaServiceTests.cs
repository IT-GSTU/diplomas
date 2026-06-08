using System.Net;
using System.Text;
using System.Text.Json;
using DiplomasViewer.Models;
using DiplomasViewer.Services;
using DiplomasViewer.Tests.TestSupport;

namespace DiplomasViewer.Tests.Services;

public class DiplomaServiceTests
{
    private static readonly GitHubOptions Options = new()
    {
        Owner = "IT-GSTU",
        Repo = "diplomas",
        Branch = "main",
        DataPath = "data/diplomas.json",
    };

    private static (DiplomaService Service, AdminState Admin, FakeHttpMessageHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage?> responder, FakeJSRuntime? js = null)
    {
        var handler = new FakeHttpMessageHandler(req => responder(req) ?? new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://app.test/") };
        var gh = new GitHubClient(http, Options);
        var service = new DiplomaService(gh, http);
        var admin = new AdminState(js ?? new FakeJSRuntime());
        return (service, admin, handler);
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage RawJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Sample(params (string Group, string Student)[] entries) =>
        JsonSerializer.Serialize(
            entries.Select(e => new Diploma { Group = e.Group, Student = e.Student, Topic = "T" }),
            JsonConfig.Options);

    private static string PutMessage(HttpRequestMessage req) =>
        JsonDocument.Parse(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
            .RootElement.GetProperty("message").GetString()!;

    private static FakeJSRuntime SignedInJs()
    {
        var js = new FakeJSRuntime();
        js.Seed("gh_token", "admin-token");
        js.Seed("gh_login", "admin-login");
        return js;
    }

    [Fact]
    public async Task LoadAsync_anonymous_loads_from_raw_and_sorts_by_group_then_student()
    {
        var raw = Sample(("ИТ-42", "Борисов Борис"), ("ИТ-41", "Юдин Юрий"), ("ИТ-41", "Андреев Андрей"));

        var (service, admin, _) = Create(req =>
            req.RequestUri!.ToString().Contains("raw.githubusercontent.com") ? RawJson(raw) : null);

        await service.LoadAsync(admin);

        Assert.True(service.Loaded);
        Assert.Equal(
            new[] { "Андреев Андрей", "Юдин Юрий", "Борисов Борис" },
            service.Items.Select(d => d.Student));
    }

    [Fact]
    public async Task LoadAsync_falls_back_to_bundled_sample_when_raw_is_unavailable()
    {
        var sample = Sample(("ИТ-41", "Запасной Студент"));

        var (service, admin, handler) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("raw.githubusercontent.com")) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (url.EndsWith("sample-data/diplomas.json")) return RawJson(sample);
            return null;
        });

        await service.LoadAsync(admin);

        Assert.True(service.Loaded);
        var item = Assert.Single(service.Items);
        Assert.Equal("Запасной Студент", item.Student);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().EndsWith("sample-data/diplomas.json"));
    }

    [Fact]
    public async Task LoadAsync_with_no_sources_available_results_in_empty_but_loaded_list()
    {
        var (service, admin, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await service.LoadAsync(admin);

        Assert.True(service.Loaded);
        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task LoadAsync_as_admin_uses_contents_api_and_caches_sha_for_later_commits()
    {
        var fromContentsApi = Sample(("ИТ-41", "Админ Студент"));
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(fromContentsApi));
        var putBodies = new List<string>();

        var (service, admin, _) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("/contents/"))
                return Json(new { content = base64, sha = "sha-from-contents-api" });
            if (req.Method == HttpMethod.Put && url.Contains("/contents/"))
            {
                putBodies.Add(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return Json(new { content = new { sha = "sha-after-commit" } });
            }
            return null;
        }, SignedInJs());

        await admin.EnsureInitializedAsync();
        await service.LoadAsync(admin);

        Assert.True(service.Loaded);
        var loaded = Assert.Single(service.Items);
        Assert.Equal("Админ Студент", loaded.Student);

        // Saving afterwards must reuse the sha captured while loading rather than re-fetching it.
        loaded.Topic = "Обновлённая тема";
        await service.SaveAsync(loaded, admin.Token!);

        var body = JsonDocument.Parse(Assert.Single(putBodies));
        Assert.Equal("sha-from-contents-api", body.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task SaveAsync_adds_new_item_commits_with_add_message_and_raises_OnChange()
    {
        string? message = null;
        var (service, admin, _) = Create(req =>
        {
            if (req.Method != HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.NotFound);
            message = PutMessage(req);
            return Json(new { content = new { sha = "sha-1" } });
        });
        await service.LoadAsync(admin);

        var changeCount = 0;
        service.OnChange += () => changeCount++;

        var item = new Diploma { Group = "ИТ-41", Student = "Новиков Никита", Topic = "Новая тема" };
        await service.SaveAsync(item, "token");

        Assert.Contains(service.Items, d => d.Id == item.Id);
        Assert.Equal("Добавлена работа: Новиков Никита", message);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task SaveAsync_replaces_existing_item_by_id_and_commits_with_update_message()
    {
        string? message = null;
        var existingId = Guid.NewGuid().ToString("N");
        var raw = JsonSerializer.Serialize(
            new[] { new Diploma { Id = existingId, Group = "ИТ-41", Student = "Старое Имя", Topic = "Старая тема" } },
            JsonConfig.Options);

        var (service, admin, _) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("raw.githubusercontent.com")) return RawJson(raw);
            if (req.Method == HttpMethod.Put) { message = PutMessage(req); return Json(new { content = new { sha = "sha-2" } }); }
            return null;
        });
        await service.LoadAsync(admin);

        var updated = service.Items[0].Clone();
        updated.Student = "Новое Имя";
        updated.Topic = "Новая тема после правки";
        await service.SaveAsync(updated, "token");

        var only = Assert.Single(service.Items);
        Assert.Equal(existingId, only.Id);
        Assert.Equal("Новое Имя", only.Student);
        Assert.Equal("Новая тема после правки", only.Topic);
        Assert.Equal("Изменена работа: Новое Имя", message);
    }

    [Fact]
    public async Task SaveAsync_refetches_sha_before_committing_when_it_was_not_captured_during_load()
    {
        var raw = JsonSerializer.Serialize(Array.Empty<Diploma>(), JsonConfig.Options);
        string? putSha = "not-set";

        var (service, admin, _) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("raw.githubusercontent.com")) return RawJson(raw);
            if (req.Method == HttpMethod.Get && url.Contains("/contents/"))
                return Json(new { content = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)), sha = "sha-fetched-on-demand" });
            if (req.Method == HttpMethod.Put)
            {
                putSha = JsonDocument.Parse(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult())
                    .RootElement.GetProperty("sha").GetString();
                return Json(new { content = new { sha = "sha-after-commit" } });
            }
            return null;
        });

        await service.LoadAsync(admin); // anonymous load via raw URL never learns the blob sha
        await service.SaveAsync(new Diploma { Group = "ИТ-41", Student = "Новиков Никита", Topic = "Тема" }, "token");

        Assert.Equal("sha-fetched-on-demand", putSha);
    }

    [Fact]
    public async Task DeleteAsync_removes_item_and_commits_with_delete_message()
    {
        string? message = null;
        var raw = JsonSerializer.Serialize(
            new[]
            {
                new Diploma { Id = "keep", Group = "ИТ-41", Student = "Остаётся", Topic = "T" },
                new Diploma { Id = "drop", Group = "ИТ-41", Student = "Удаляемый", Topic = "T" },
            },
            JsonConfig.Options);

        var (service, admin, _) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("raw.githubusercontent.com")) return RawJson(raw);
            if (req.Method == HttpMethod.Put) { message = PutMessage(req); return Json(new { content = new { sha = "sha-3" } }); }
            return null;
        });
        await service.LoadAsync(admin);

        var changeCount = 0;
        service.OnChange += () => changeCount++;

        await service.DeleteAsync("drop", "token");

        var remaining = Assert.Single(service.Items);
        Assert.Equal("keep", remaining.Id);
        Assert.Equal("Удалена работа: Удаляемый", message);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task DeleteAsync_with_unknown_id_does_nothing_and_does_not_commit()
    {
        var putCalled = false;
        var raw = JsonSerializer.Serialize(
            new[] { new Diploma { Id = "keep", Group = "ИТ-41", Student = "Остаётся", Topic = "T" } },
            JsonConfig.Options);

        var (service, admin, _) = Create(req =>
        {
            var url = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && url.Contains("raw.githubusercontent.com")) return RawJson(raw);
            if (req.Method == HttpMethod.Put) { putCalled = true; return Json(new { content = new { sha = "x" } }); }
            return null;
        });
        await service.LoadAsync(admin);

        var changeCount = 0;
        service.OnChange += () => changeCount++;

        await service.DeleteAsync("does-not-exist", "token");

        Assert.Single(service.Items);
        Assert.False(putCalled);
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public async Task Find_returns_matching_item_or_null()
    {
        var raw = JsonSerializer.Serialize(
            new[] { new Diploma { Id = "abc", Group = "ИТ-41", Student = "Найдёнов Найден", Topic = "T" } },
            JsonConfig.Options);
        var (service, admin, _) = Create(req =>
            req.RequestUri!.ToString().Contains("raw.githubusercontent.com") ? RawJson(raw) : null);
        await service.LoadAsync(admin);

        Assert.Equal("Найдёнов Найден", service.Find("abc")?.Student);
        Assert.Null(service.Find("missing"));
    }
}
