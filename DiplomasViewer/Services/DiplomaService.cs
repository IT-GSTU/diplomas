using System.Net.Http.Json;
using System.Text.Json;
using DiplomasViewer.Models;

namespace DiplomasViewer.Services;

/// <summary>
/// Хранит список работ в памяти, загружает его из репозитория (или встроенного
/// фолбэка) и публикует изменения через GitHub Contents API.
/// </summary>
public class DiplomaService
{
    private readonly GitHubClient _gh;
    private readonly HttpClient _http;

    private List<Diploma> _items = new();
    private string? _sha;

    public DiplomaService(GitHubClient gh, HttpClient http)
    {
        _gh = gh;
        _http = http;
    }

    public IReadOnlyList<Diploma> Items => _items;
    public bool Loaded { get; private set; }
    public event Action? OnChange;

    /// <summary>
    /// Загружает данные. Для админа — через Contents API (берём sha для записи и
    /// самые свежие данные), иначе — по raw-URL; при неудаче — встроенный фолбэк.
    /// </summary>
    public async Task LoadAsync(AdminState admin)
    {
        if (admin.IsAdmin)
        {
            var withSha = await _gh.GetWithShaAsync(admin.Token!);
            if (withSha is not null)
            {
                _items = Deserialize(withSha.Value.Content);
                _sha = withSha.Value.Sha;
                Finish();
                return;
            }
            // файла в репозитории ещё нет — sha неизвестен, первая запись создаст его
            _sha = null;
        }

        var raw = await _gh.GetRawAsync();
        if (raw is not null)
        {
            _items = Deserialize(raw);
            Finish();
            return;
        }

        // фолбэк: встроенная копия в wwwroot
        try
        {
            _items = await _http.GetFromJsonAsync<List<Diploma>>(
                "sample-data/diplomas.json", JsonConfig.Options) ?? new();
        }
        catch
        {
            _items = new();
        }
        Finish();
    }

    public Diploma? Find(string id) => _items.FirstOrDefault(d => d.Id == id);

    /// <summary>Добавляет или обновляет запись (по Id) и публикует изменения.</summary>
    public async Task SaveAsync(Diploma item, string token)
    {
        var existing = _items.FirstOrDefault(d => d.Id == item.Id);
        if (existing is null)
            _items.Add(item);
        else
            _items[_items.IndexOf(existing)] = item;

        await CommitAsync(token, existing is null
            ? $"Добавлена работа: {item.Student}"
            : $"Изменена работа: {item.Student}");
        OnChange?.Invoke();
    }

    public async Task DeleteAsync(string id, string token)
    {
        var item = _items.FirstOrDefault(d => d.Id == id);
        if (item is null) return;
        _items.Remove(item);
        await CommitAsync(token, $"Удалена работа: {item.Student}");
        OnChange?.Invoke();
    }

    private async Task CommitAsync(string token, string message)
    {
        // если sha неизвестен, но файл уже существует — получим его, иначе PUT создаст файл
        if (_sha is null)
        {
            var withSha = await _gh.GetWithShaAsync(token);
            if (withSha is not null) _sha = withSha.Value.Sha;
        }

        var ordered = _items
            .OrderBy(d => d.Group)
            .ThenBy(d => d.Student, StringComparer.CurrentCulture)
            .ToList();
        _items = ordered;

        var json = JsonSerializer.Serialize(ordered, JsonConfig.Options);
        _sha = await _gh.PutAsync(json, _sha, token, message);
    }

    private static List<Diploma> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<Diploma>>(json, JsonConfig.Options) ?? new();

    private void Finish()
    {
        _items = _items
            .OrderBy(d => d.Group)
            .ThenBy(d => d.Student, StringComparer.CurrentCulture)
            .ToList();
        Loaded = true;
        OnChange?.Invoke();
    }
}
