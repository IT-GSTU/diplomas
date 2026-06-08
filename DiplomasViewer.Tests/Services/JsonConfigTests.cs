using System.Text.Json;
using DiplomasViewer.Models;
using DiplomasViewer.Services;

namespace DiplomasViewer.Tests.Services;

public class JsonConfigTests
{
    [Fact]
    public void Properties_are_serialized_as_camelCase()
    {
        var diploma = new Diploma { Group = "ИТ-41", Student = "Иванов Иван", Topic = "Тема" };

        var json = JsonSerializer.Serialize(diploma, JsonConfig.Options);

        Assert.Contains("\"group\"", json);
        Assert.Contains("\"student\"", json);
        Assert.Contains("\"topic\"", json);
        Assert.DoesNotContain("\"Group\"", json);
    }

    [Fact]
    public void Cyrillic_text_is_not_escaped_to_unicode_sequences()
    {
        var diploma = new Diploma { Student = "Иванов Иван", Topic = "Разработка веб-приложения" };

        var json = JsonSerializer.Serialize(diploma, JsonConfig.Options);

        Assert.Contains("Иванов Иван", json);
        Assert.DoesNotContain("\\u04", json);
    }

    [Fact]
    public void Round_trip_preserves_data()
    {
        var original = new List<Diploma>
        {
            new() { Group = "ИТ-41", Student = "Иванов Иван", Topic = "Тема 1", Year = 2026, RepoUrl = "https://github.com/x/y" },
            new() { Group = "ИТ-42", Student = "Петров Пётр", Topic = "Тема 2", Year = null },
        };

        var json = JsonSerializer.Serialize(original, JsonConfig.Options);
        var roundTripped = JsonSerializer.Deserialize<List<Diploma>>(json, JsonConfig.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Count, roundTripped!.Count);
        for (var i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].Id, roundTripped[i].Id);
            Assert.Equal(original[i].Group, roundTripped[i].Group);
            Assert.Equal(original[i].Student, roundTripped[i].Student);
            Assert.Equal(original[i].Topic, roundTripped[i].Topic);
            Assert.Equal(original[i].Year, roundTripped[i].Year);
            Assert.Equal(original[i].RepoUrl, roundTripped[i].RepoUrl);
        }
    }

    [Fact]
    public void Deserialization_is_property_name_case_insensitive()
    {
        const string json = """[{"GROUP":"ИТ-41","STUDENT":"Иванов Иван","TOPIC":"Тема"}]""";

        var items = JsonSerializer.Deserialize<List<Diploma>>(json, JsonConfig.Options);

        Assert.NotNull(items);
        var item = Assert.Single(items!);
        Assert.Equal("ИТ-41", item.Group);
        Assert.Equal("Иванов Иван", item.Student);
    }

    [Fact]
    public void Output_is_indented_for_readability_in_git_diffs()
    {
        var diploma = new Diploma { Student = "Иванов Иван", Topic = "Тема" };

        var json = JsonSerializer.Serialize(diploma, JsonConfig.Options);

        Assert.Contains("\n", json);
    }
}
