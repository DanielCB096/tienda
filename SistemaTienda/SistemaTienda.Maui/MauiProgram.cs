using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;
using SistemaTienda.Application.Services;
using SistemaTienda.Infrastructure;
using SistemaTienda.Maui.Pages;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// En Android y Windows los datos de la app viven en el directorio privado
		// del usuario; nunca se comparten con otras aplicaciones.
		var rutaBaseDatos = Path.Combine(FileSystem.AppDataDirectory, "tienda.db");

		builder.Services.AddSingleton(new DatabaseTools(rutaBaseDatos));
		builder.Services.AddSingleton<ConfiguracionService>();
		builder.Services.AddSingleton<InventoryService>();
		builder.Services.AddSingleton<VentaService>();
		builder.Services.AddSingleton<CorteCajaService>();
		builder.Services.AddSingleton<ReporteUtilidadService>();
		builder.Services.AddSingleton<RespaldoService>();
		builder.Services.AddSingleton<SesionApp>();
		builder.Services.AddSingleton<NavegacionApp>();
		builder.Services.AddSingleton<ArranqueApp>();

		builder.Services.AddTransient<ConfiguracionInicialPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<PrincipalPage>();
		builder.Services.AddTransient<InicioPage>();
		builder.Services.AddTransient<VentasPage>();
		builder.Services.AddTransient<CajaPage>();
		builder.Services.AddTransient<AjustesPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
