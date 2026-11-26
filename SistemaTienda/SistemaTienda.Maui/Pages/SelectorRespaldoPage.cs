using Microsoft.Maui.Storage;

namespace SistemaTienda.Maui.Pages;

/// <summary>
/// Explorador sencillo para elegir un respaldo (.db/.db.stb). Evita los selectores
/// nativos que fallan en Windows sin empaquetar y no obliga a escribir rutas.
/// </summary>
public sealed class SelectorRespaldoPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _resultado = new();
    private readonly Label _rutaLabel;
    private readonly Label _estado;
    private readonly VerticalStackLayout _lista;
    private string _carpetaActual;

    public Task<string?> Resultado => _resultado.Task;

    public SelectorRespaldoPage()
    {
        Title = "Elegir respaldo";
        _carpetaActual = CarpetaInicial();

        _rutaLabel = new Label { FontSize = 13, LineBreakMode = LineBreakMode.MiddleTruncation };
        _estado = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#B3261E"),
            IsVisible = false
        };
        _lista = new VerticalStackLayout { Spacing = 6 };

        var volver = new Button { Text = "Subir", WidthRequest = 90 };
        volver.Clicked += (_, _) => Subir();
        var cancelar = new Button { Text = "Cancelar", WidthRequest = 110 };
        cancelar.Clicked += async (_, _) => await CerrarAsync(null);
        var ir = new Button { Text = "Ir", WidthRequest = 64 };
        var rutaEntrada = new Entry
        {
            Placeholder = @"C:\ruta\a\la\carpeta",
            FontSize = 13,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        ir.Clicked += (_, _) => Cargar(rutaEntrada.Text);
        rutaEntrada.Completed += (_, _) => Cargar(rutaEntrada.Text);

        var barra = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };
        barra.Add(rutaEntrada, 0, 0);
        barra.Add(ir, 1, 0);
        barra.Add(volver, 2, 0);

        var rapidos = new HorizontalStackLayout { Spacing = 6 };
        AgregarRapido(rapidos, "Descargas", Path.Combine(Entorno, "Downloads"));
        AgregarRapido(rapidos, "Escritorio", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AgregarRapido(rapidos, "Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AgregarRapido(rapidos, "Respaldos", Path.Combine(FileSystem.AppDataDirectory, "respaldos"));

        var encabezado = new VerticalStackLayout
        {
            Padding = new Thickness(16, 16, 16, 6),
            Spacing = 8
        };
        encabezado.Add(new Label
        {
            Text = "Elegir respaldo",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        });
        encabezado.Add(new Label
        {
            Text = "Abre una carpeta o toca un archivo .db o .db.stb.",
            FontSize = 13,
            TextColor = Colors.Gray
        });
        encabezado.Add(barra);
        encabezado.Add(rapidos);
        encabezado.Add(_rutaLabel);
        encabezado.Add(_estado);
        encabezado.Add(cancelar);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        grid.Add(encabezado, 0, 0);
        grid.Add(new ScrollView { Content = _lista }, 0, 1);
        Content = grid;

        Cargar(_carpetaActual);
    }

    private static string Entorno =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string CarpetaInicial()
    {
        foreach (var carpeta in new[]
                 {
                     Path.Combine(Entorno, "Downloads"),
                     Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                     Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                 })
        {
            if (Directory.Exists(carpeta))
                return carpeta;
        }

        return Entorno;
    }

    private void AgregarRapido(HorizontalStackLayout barra, string texto, string ruta)
    {
        var boton = new Button { Text = texto, FontSize = 12, HeightRequest = 36 };
        boton.Clicked += (_, _) => Cargar(ruta);
        barra.Add(boton);
    }

    private void Subir()
    {
        var padre = Directory.GetParent(_carpetaActual);
        if (padre != null)
            Cargar(padre.FullName);
    }

    private void Cargar(string? ruta)
    {
        _estado.IsVisible = false;
        _lista.Clear();

        if (string.IsNullOrWhiteSpace(ruta))
            return;

        string carpeta;
        try
        {
            carpeta = Path.GetFullPath(ruta.Trim().Trim('"'));
        }
        catch (Exception ex)
        {
            MostrarError($"Ruta no válida: {ex.Message}");
            return;
        }

        if (File.Exists(carpeta))
        {
            if (EsRespaldo(carpeta))
                _ = CerrarAsync(carpeta);
            else
                MostrarError("Selecciona un archivo .db o .db.stb.");
            return;
        }

        if (!Directory.Exists(carpeta))
        {
            MostrarError($"No existe la carpeta: {carpeta}");
            return;
        }

        _carpetaActual = carpeta;
        _rutaLabel.Text = carpeta;

        try
        {
            var carpetas = Directory.EnumerateDirectories(carpeta)
                .Where(d => !Path.GetFileName(d).StartsWith('.'))
                .OrderBy(d => Path.GetFileName(d), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var directorio in carpetas)
            {
                var boton = CrearFila($"Carpeta: {Path.GetFileName(directorio)}");
                boton.Clicked += (_, _) => Cargar(directorio);
                _lista.Add(boton);
            }

            var archivos = Directory.EnumerateFiles(carpeta)
                .Where(EsRespaldo)
                .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var archivo in archivos)
            {
                var nombre = Path.GetFileName(archivo);
                var tamano = new FileInfo(archivo).Length / 1024;
                var boton = CrearFila($"Respaldo: {nombre} ({tamano} KB)");
                boton.Clicked += async (_, _) => await CerrarAsync(archivo);
                _lista.Add(boton);
            }

            if (carpetas.Count == 0 && archivos.Count == 0)
            {
                _lista.Add(new Label
                {
                    Text = "Esta carpeta está vacía o no tiene respaldos.",
                    FontSize = 13,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(4, 8)
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            MostrarError("Windows no permite leer esta carpeta. Prueba con Descargas o Documentos.");
        }
        catch (IOException ex)
        {
            MostrarError($"No se pudo leer la carpeta: {ex.Message}");
        }
    }

    private static bool EsRespaldo(string ruta)
    {
        var extension = Path.GetExtension(ruta);
        return extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".stb", StringComparison.OrdinalIgnoreCase);
    }

    private static Button CrearFila(string texto)
    {
        var verde = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#8FD3B0")
            : Color.FromArgb("#2E6B4F");

        return new Button
        {
            Text = texto,
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(10, 8),
            HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
            TextColor = verde,
            BorderColor = verde,
            BorderWidth = 1,
            CornerRadius = 10
        };
    }

    private void MostrarError(string mensaje)
    {
        _estado.Text = mensaje;
        _estado.IsVisible = true;
    }

    private async Task CerrarAsync(string? ruta)
    {
        _resultado.TrySetResult(ruta);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _resultado.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
