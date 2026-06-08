using Microsoft.JSInterop;

namespace DiplomasViewer.Tests.TestSupport;

/// <summary>
/// Minimal <see cref="IJSRuntime"/> backed by an in-memory dictionary, standing in for the
/// browser's sessionStorage that <c>AdminState</c> talks to via "sessionStorage.*" calls.
/// </summary>
public sealed class FakeJSRuntime : IJSRuntime
{
    private readonly Dictionary<string, string?> _store = new();

    public List<string> InvokedIdentifiers { get; } = new();

    public void Seed(string key, string value) => _store[key] = value;

    public bool Contains(string key) => _store.ContainsKey(key);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        args ??= Array.Empty<object?>();
        InvokedIdentifiers.Add(identifier);

        object? result = identifier switch
        {
            "sessionStorage.getItem" => _store.GetValueOrDefault((string)args[0]!),
            "sessionStorage.setItem" => Apply(() => _store[(string)args[0]!] = args[1] as string),
            "sessionStorage.removeItem" => Apply(() => _store.Remove((string)args[0]!)),
            _ => throw new InvalidOperationException($"Unexpected JS interop call: {identifier}"),
        };

        return ValueTask.FromResult((TValue)result!);
    }

    private static object? Apply(Action action)
    {
        action();
        return null;
    }
}
