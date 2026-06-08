using DiplomasViewer.Services;
using DiplomasViewer.Tests.TestSupport;

namespace DiplomasViewer.Tests.Services;

public class AdminStateTests
{
    [Fact]
    public void Initially_not_signed_in()
    {
        var state = new AdminState(new FakeJSRuntime());

        Assert.False(state.IsAdmin);
        Assert.Null(state.Token);
        Assert.Null(state.Login);
    }

    [Fact]
    public async Task EnsureInitializedAsync_restores_session_from_storage()
    {
        var js = new FakeJSRuntime();
        js.Seed("gh_token", "stored-token");
        js.Seed("gh_login", "stored-login");
        var state = new AdminState(js);

        await state.EnsureInitializedAsync();

        Assert.True(state.IsAdmin);
        Assert.Equal("stored-token", state.Token);
        Assert.Equal("stored-login", state.Login);
    }

    [Fact]
    public async Task EnsureInitializedAsync_with_empty_storage_leaves_anonymous()
    {
        var state = new AdminState(new FakeJSRuntime());

        await state.EnsureInitializedAsync();

        Assert.False(state.IsAdmin);
        Assert.Null(state.Token);
    }

    [Fact]
    public async Task EnsureInitializedAsync_runs_only_once_for_concurrent_callers()
    {
        var js = new FakeJSRuntime();
        js.Seed("gh_token", "stored-token");
        var state = new AdminState(js);

        await Task.WhenAll(state.EnsureInitializedAsync(), state.EnsureInitializedAsync(), state.EnsureInitializedAsync());

        // InitAsync reads two keys (token + login) per run; three concurrent callers must still
        // share a single run, so exactly 2 reads total — not 6 — should reach the JS runtime.
        Assert.Equal(2, js.InvokedIdentifiers.Count(id => id == "sessionStorage.getItem"));
    }

    [Fact]
    public async Task EnsureInitializedAsync_raises_OnChange_only_when_session_is_restored()
    {
        var anonymousJs = new FakeJSRuntime();
        var anonymous = new AdminState(anonymousJs);
        var anonymousChanges = 0;
        anonymous.OnChange += () => anonymousChanges++;

        var signedInJs = new FakeJSRuntime();
        signedInJs.Seed("gh_token", "stored-token");
        var signedIn = new AdminState(signedInJs);
        var signedInChanges = 0;
        signedIn.OnChange += () => signedInChanges++;

        await anonymous.EnsureInitializedAsync();
        await signedIn.EnsureInitializedAsync();

        Assert.Equal(0, anonymousChanges);
        Assert.Equal(1, signedInChanges);
    }

    [Fact]
    public async Task SignInAsync_stores_token_and_login_and_notifies()
    {
        var js = new FakeJSRuntime();
        var state = new AdminState(js);
        var changes = 0;
        state.OnChange += () => changes++;

        await state.SignInAsync("new-token", "octocat");

        Assert.True(state.IsAdmin);
        Assert.Equal("new-token", state.Token);
        Assert.Equal("octocat", state.Login);
        Assert.Equal(1, changes);
        Assert.True(js.Contains("gh_token"));
        Assert.True(js.Contains("gh_login"));
    }

    [Fact]
    public async Task SignInAsync_persists_empty_string_when_login_is_unknown()
    {
        var js = new FakeJSRuntime();
        var state = new AdminState(js);

        await state.SignInAsync("new-token", login: null);

        Assert.Null(state.Login);
        Assert.True(js.Contains("gh_login"));
    }

    [Fact]
    public async Task SignOutAsync_clears_state_storage_and_notifies()
    {
        var js = new FakeJSRuntime();
        var state = new AdminState(js);
        await state.SignInAsync("token", "octocat");
        var changes = 0;
        state.OnChange += () => changes++;

        await state.SignOutAsync();

        Assert.False(state.IsAdmin);
        Assert.Null(state.Token);
        Assert.Null(state.Login);
        Assert.Equal(1, changes);
        Assert.False(js.Contains("gh_token"));
        Assert.False(js.Contains("gh_login"));
    }
}
