using System.ComponentModel.DataAnnotations;
using DiplomasViewer.Models;

namespace DiplomasViewer.Tests.Models;

public class DiplomaValidationTests
{
    private static List<ValidationResult> Validate(Diploma diploma)
    {
        var context = new ValidationContext(diploma);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(diploma, context, results, validateAllProperties: true);
        return results;
    }

    private static Diploma Valid() => new()
    {
        Group = "ИТ-41",
        Student = "Иванов Иван",
        Topic = "Тема диплома",
        Supervisor = "Петров П.П.",
        Year = 2026,
    };

    [Fact]
    public void Valid_diploma_passes_validation()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Student_is_required(string student)
    {
        var diploma = Valid();
        diploma.Student = student;

        var results = Validate(diploma);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Diploma.Student)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Topic_is_required(string topic)
    {
        var diploma = Valid();
        diploma.Topic = topic;

        var results = Validate(diploma);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Diploma.Topic)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://github.com/example/repo")]
    [InlineData("http://example.com")]
    public void Url_fields_accept_blank_or_http_links(string url)
    {
        var diploma = Valid();
        diploma.RepoUrl = url;
        diploma.InstallUrl = url;
        diploma.DemoUrl = url;

        Assert.Empty(Validate(diploma));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("javascript:alert(1)")]
    public void Url_fields_reject_non_http_values(string url)
    {
        var diploma = Valid();
        diploma.RepoUrl = url;

        var results = Validate(diploma);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Diploma.RepoUrl)));
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2101)]
    public void Year_outside_allowed_range_is_invalid(int year)
    {
        var diploma = Valid();
        diploma.Year = year;

        var results = Validate(diploma);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Diploma.Year)));
    }

    [Theory]
    [InlineData(1900)]
    [InlineData(2100)]
    [InlineData(2026)]
    public void Year_within_allowed_range_is_valid(int year)
    {
        var diploma = Valid();
        diploma.Year = year;

        Assert.Empty(Validate(diploma));
    }

    [Fact]
    public void Year_can_be_null()
    {
        var diploma = Valid();
        diploma.Year = null;

        Assert.Empty(Validate(diploma));
    }

    [Fact]
    public void New_diploma_gets_a_unique_id()
    {
        var a = new Diploma();
        var b = new Diploma();

        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEmpty(a.Id);
    }

    [Fact]
    public void Clone_copies_values_into_an_independent_instance()
    {
        var original = Valid();

        var clone = original.Clone();
        clone.Student = "Другой студент";

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal("Иванов Иван", original.Student);
        Assert.Equal("Другой студент", clone.Student);
    }
}
