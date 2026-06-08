using Microsoft.JSInterop;

namespace DiplomasViewer.Services;

/// <summary>
/// Состояние режима администратора. Токен хранится в sessionStorage (живёт до
/// закрытия вкладки) и в памяти. Реальную защиту записи обеспечивает GitHub —
/// это лишь признак для UI и источник токена для запросов.
/// </summary>
public class AdminState
{
    private const string TokenKey = "gh_token";
    private const string LoginKey = "gh_login";

    private readonly IJSRuntime _js;
    private Task? _initTask;

    public AdminState(IJSRuntime js) => _js = js;

    public string? Token { get; private set; }
    public string? Login { get; private set; }
    public bool IsAdmin => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    /// <summary>
    /// Однократно восстанавливает токен из sessionStorage. Параллельные вызовы
    /// ждут одну и ту же задачу — токен гарантированно загружен к моменту возврата.
    /// </summary>
    public Task EnsureInitializedAsync() => _initTask ??= InitAsync();

    private async Task InitAsync()
    {
        Token = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        Login = await _js.InvokeAsync<string?>("sessionStorage.getItem", LoginKey);
        if (IsAdmin) OnChange?.Invoke();
    }

    public async Task SignInAsync(string token, string? login)
    {
        Token = token;
        Login = login;
        await _js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
        await _js.InvokeVoidAsync("sessionStorage.setItem", LoginKey, login ?? "");
        OnChange?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Token = null;
        Login = null;
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", LoginKey);
        OnChange?.Invoke();
    }
}
