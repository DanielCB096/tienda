using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly ArranqueApp _arranque;

	public App(ArranqueApp arranque)
	{
		InitializeComponent();
		_arranque = arranque;
	}

	protected override Window CreateWindow(IActivationState? activationState) =>
		new(_arranque.CrearPaginaInicial()) { Title = "SistemaTienda" };
}
