using System.Text.Encodings.Web;
using System.Text.Json;

namespace DiplomasViewer.Services;

/// <summary>Единые настройки сериализации для файла данных.</summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        // не экранировать кириллицу в \uXXXX — JSON в репозитории остаётся читаемым
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
