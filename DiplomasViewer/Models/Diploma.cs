using System.ComponentModel.DataAnnotations;

namespace DiplomasViewer.Models;

/// <summary>
/// Одна дипломная работа. Соответствует колонкам исходного Excel-файла.
/// </summary>
public class Diploma
{
    /// <summary>Стабильный идентификатор записи (для редактирования/удаления).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Группа.</summary>
    public string Group { get; set; } = "";

    /// <summary>Студент.</summary>
    [Required(ErrorMessage = "Укажите студента")]
    public string Student { get; set; } = "";

    /// <summary>Тема.</summary>
    [Required(ErrorMessage = "Укажите тему")]
    public string Topic { get; set; } = "";

    /// <summary>Руководитель.</summary>
    public string Supervisor { get; set; } = "";

    /// <summary>Краткое описание.</summary>
    public string Description { get; set; } = "";

    /// <summary>URL репозитория с кодом.</summary>
    [RegularExpression(@"^\s*$|^https?://.+", ErrorMessage = "Ссылка должна начинаться с http:// или https://")]
    public string RepoUrl { get; set; } = "";

    /// <summary>URL инсталляционного файла.</summary>
    [RegularExpression(@"^\s*$|^https?://.+", ErrorMessage = "Ссылка должна начинаться с http:// или https://")]
    public string InstallUrl { get; set; } = "";

    /// <summary>URL демо-версии.</summary>
    [RegularExpression(@"^\s*$|^https?://.+", ErrorMessage = "Ссылка должна начинаться с http:// или https://")]
    public string DemoUrl { get; set; } = "";

    /// <summary>Год.</summary>
    [Range(1900, 2100, ErrorMessage = "Введите корректный год")]
    public int? Year { get; set; }

    public Diploma Clone() => (Diploma)MemberwiseClone();
}
