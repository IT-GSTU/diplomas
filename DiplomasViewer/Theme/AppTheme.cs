using MudBlazor;

namespace DiplomasViewer.Theme;

/// <summary>
/// Тема оформления MudBlazor в фирменных тонах ГГТУ (IT ФАИС): корпоративный
/// синий как основной цвет, тёмно-синий AppBar в тон прежнему градиенту сайдбара.
/// </summary>
public static class AppTheme
{
    public static readonly MudTheme Gstu = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0B4DA2",          // корпоративный синий ГГТУ
            Secondary = "#6A1B9A",        // фиолетовый акцент (в тон прежнему градиенту)
            Tertiary = "#0277BD",
            AppbarBackground = "#052767",  // глубокий тёмно-синий (как верх прежнего градиента)
            AppbarText = "#ffffff",
            Background = "#f6f7f9",
            DrawerBackground = "#ffffff",
            DrawerText = "#2b2b3a",
            DrawerIcon = "#0B4DA2",
            Success = "#2e9e57",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#5B9BE0",
            Secondary = "#CE93D8",
            Tertiary = "#4FC3F7",
            AppbarBackground = "#0a1733",
            AppbarText = "#ffffff",
            Background = "#1a1a27",
            Surface = "#26263a",
            DrawerBackground = "#1f1f2e",
            Success = "#41c777",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
