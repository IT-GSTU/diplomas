namespace DiplomasViewer.Models;

/// <summary>
/// Настройки репозитория-хранилища (из wwwroot/appsettings.json, секция "GitHub").
/// </summary>
public class GitHubOptions
{
    public string Owner { get; set; } = "IT-GSTU";
    public string Repo { get; set; } = "diplomas";
    public string Branch { get; set; } = "main";
    public string DataPath { get; set; } = "data/diplomas.json";
}
