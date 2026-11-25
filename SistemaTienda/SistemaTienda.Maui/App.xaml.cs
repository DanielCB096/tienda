using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly ArranqueApp _arranque;
	private readonly BloqueoSesion _bloqueo;

	public App(ArranqueApp arranque, BloqueoSesion bloqueo)
	{
		InitializeComponent();
		_arranque = arranque;
		_bloqueo = bloqueo;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (MauiProgram.InstanciaDuplicada)
			return new Window(new Pages.InstanciaDuplicadaPage()) { Title = "POSale" };

		var ventana = new Window(_arranque.CrearPaginaInicial()) { Title = "POSale" };
		ventana.Activated += (_, _) => _bloqueo.Verificar();
		return ventana;
	}
}
