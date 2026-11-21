using System.Globalization;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class AjustesPage : ContentPage
{
    private readonly ConfiguracionService _config;
    private readonly RespaldoService _respaldos;
    private readonly SesionApp _sesion;
    private readonly NavegacionApp _navegacion;

    public AjustesPage(ConfiguracionService config, RespaldoService respaldos,
        SesionApp sesion, NavegacionApp navegacion)
    {
        InitializeComponent();
        _config = config;
        _respaldos = respaldos;
        _sesion = sesion;
        _navegacion = navegacion;
        UsuarioRolPicker.ItemsSource = new[] { "Dueño", "Encargado", "Empleado" };
        UsuarioRolPicker.SelectedIndex = 2;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Cargar();
    }

    private void Cargar()
    {
        var config = _config.ObtenerConfiguracion().Value;
        if (config != null)
        {
            TiendaLabel.Text = config.NombreTienda;
            DetalleTiendaLabel.Text =
                $"{config.Giro} · Fondo inicial {Formato.Moneda(config.FondoCajaInicial)}";
            ReglasLabel.Text =
                $"Impuesto {config.ImpuestoPredeterminado.ToString("F2", CultureInfo.InvariantCulture)} % · " +
                $"Comisión tarjeta {config.ComisionTarjeta.ToString("F2", CultureInfo.InvariantCulture)} % · " +
                $"Fiado {(config.FiadoActivado ? "activado" : "desactivado")} · " +
                (config.RespaldoIntervaloDias > 0
                    ? $"respaldo cada {config.RespaldoIntervaloDias} día(s)"
                    : "respaldos automáticos desactivados");
        }

        UsuarioLabel.Text = _sesion.Usuario == null
            ? string.Empty
            : $"Sesión: {_sesion.Usuario.Nombre} · Rol: {_sesion.Usuario.Rol}";

        EditarConfiguracionButton.IsVisible = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar);
        UsuariosButton.IsVisible = _sesion.Usuario != null &&
            AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.GestionarUsuarios);

        RutaBaseLabel.Text = _respaldos.RutaBaseDatos;
        RutaRespaldosLabel.Text = _respaldos.DirectorioRespaldos;

        var ultimo = _respaldos.ObtenerUltimoRespaldo();
        var lista = _respaldos.ListarRespaldos();
        var cantidad = lista.IsSuccess ? lista.Value.Count : 0;

        UltimoRespaldoLabel.Text = ultimo.IsSuccess && ultimo.Value.HasValue
            ? Formato.FechaHora(ultimo.Value.Value)
            : "Sin respaldos";
        RespaldoDetalleLabel.Text = cantidad == 1
            ? "1 copia local verificada."
            : $"{cantidad} copias locales verificadas.";
    }

    private UsuarioSistema? _usuarioEditando;

    private void OnUsuariosClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.GestionarUsuarios))
        {
            _ = DisplayAlertAsync("Sin permiso",
                "Solo el dueño puede gestionar usuarios y PIN.", "Continuar");
            return;
        }

        UsuariosOverlay.IsVisible = true;
        CargarUsuarios();
    }

    private void CargarUsuarios()
    {
        var usuarios = _config.ObtenerUsuarios().Value ?? new List<UsuarioSistema>();
        ListaUsuarios.Children.Clear();

        if (usuarios.Count == 0)
        {
            ListaUsuarios.Children.Add(new Label
            {
                Text = "No hay usuarios registrados.",
                HorizontalTextAlignment = TextAlignment.Center
            });
            return;
        }

        foreach (var usuario in usuarios)
        {
            var fila = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            var datos = new VerticalStackLayout { Spacing = 1 };
            datos.Children.Add(new Label
            {
                Text = usuario.Nombre,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15
            });
            datos.Children.Add(new Label
            {
                Text = usuario.Rol.ToString(),
                FontSize = 13
            });

            var editar = new Button { Text = "Editar" };
            editar.SetDynamicResource(Button.StyleProperty, "BotonSecundario");
            editar.Clicked += (_, _) => AbrirFormularioUsuario(usuario);

            var pin = new Button { Text = "Cambiar PIN" };
            pin.SetDynamicResource(Button.StyleProperty, "BotonSecundario");
            pin.Clicked += (_, _) => AbrirFormularioPin(usuario);

            Grid.SetColumn(editar, 1);
            Grid.SetColumn(pin, 2);
            fila.Children.Add(datos);
            fila.Children.Add(editar);
            fila.Children.Add(pin);

            var borde = new Border { Content = fila, Padding = 10 };
            borde.SetDynamicResource(VisualElement.StyleProperty, "Tarjeta");
            ListaUsuarios.Children.Add(borde);
        }
    }

    private void OnNuevoUsuarioClicked(object sender, EventArgs e) =>
        AbrirFormularioUsuario(null);

    private void AbrirFormularioUsuario(UsuarioSistema? usuario)
    {
        _usuarioEditando = usuario;
        UsuarioFormTitulo.Text = usuario == null ? "Nuevo usuario" : "Editar usuario";
        UsuarioNombreEntry.Text = usuario?.Nombre ?? string.Empty;
        UsuarioRolPicker.SelectedIndex = usuario == null ? 2 : (int)usuario.Rol;
        UsuarioPinEntry.Text = string.Empty;
        UsuarioPinEntry.Placeholder = usuario == null
            ? "PIN de 4 a 6 dígitos"
            : "Nuevo PIN (vacío = conservar)";
        UsuarioFormAyuda.IsVisible = usuario != null;
        UsuarioFormError.IsVisible = false;
        UsuarioFormOverlay.IsVisible = true;
    }

    private void AbrirFormularioPin(UsuarioSistema usuario)
    {
        _usuarioEditando = usuario;
        UsuarioFormTitulo.Text = "Cambiar PIN";
        UsuarioNombreEntry.Text = usuario.Nombre;
        UsuarioRolPicker.SelectedIndex = (int)usuario.Rol;
        UsuarioPinEntry.Text = string.Empty;
        UsuarioPinEntry.Placeholder = "Nuevo PIN de 4 a 6 dígitos";
        UsuarioFormAyuda.IsVisible = false;
        UsuarioFormError.IsVisible = false;
        UsuarioFormOverlay.IsVisible = true;
        UsuarioPinEntry.Focus();
    }

    private async void OnGuardarUsuarioClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.GestionarUsuarios))
        {
            MostrarErrorUsuario("Solo el dueño puede gestionar usuarios y PIN.");
            return;
        }

        UsuarioFormError.IsVisible = false;
        var nombre = UsuarioNombreEntry.Text?.Trim() ?? string.Empty;
        var pin = UsuarioPinEntry.Text?.Trim() ?? string.Empty;
        var rol = UsuarioRolPicker.SelectedIndex switch
        {
            0 => RolUsuario.Dueño,
            1 => RolUsuario.Encargado,
            _ => RolUsuario.Empleado
        };

        if (nombre.Length == 0)
        {
            MostrarErrorUsuario("El nombre del usuario es obligatorio.");
            return;
        }

        GuardarUsuarioButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();

            if (_usuarioEditando == null)
            {
                if (!ConfiguracionService.PinValido(pin))
                {
                    MostrarErrorUsuario("El PIN debe tener entre 4 y 6 dígitos.");
                    return;
                }

                var alta = _config.AgregarUsuario(nombre, pin, rol);
                if (alta.IsError)
                {
                    MostrarErrorUsuario(alta.Message);
                    return;
                }

                CerrarFormularioUsuario();
                CargarUsuarios();
                await DisplayAlertAsync("Usuario agregado", alta.Value.Mensaje, "Continuar");
                return;
            }

            var edicion = _config.ActualizarUsuario(_usuarioEditando.Id, nombre, rol);
            if (edicion.IsError)
            {
                MostrarErrorUsuario(edicion.Message);
                return;
            }

            if (pin.Length > 0)
            {
                var cambioPin = _config.CambiarPin(_usuarioEditando.Id, pin);
                if (cambioPin.IsError)
                {
                    MostrarErrorUsuario(cambioPin.Message);
                    return;
                }
            }

            var esUsuarioActual = _usuarioEditando.Id == _sesion.Usuario.Id;
            CerrarFormularioUsuario();
            CargarUsuarios();
            await DisplayAlertAsync("Usuario actualizado", edicion.Message, "Continuar");

            if (esUsuarioActual && (rol != _sesion.Usuario.Rol ||
                                    !string.Equals(nombre, _sesion.Usuario.Nombre,
                                        StringComparison.CurrentCulture)))
                await DisplayAlertAsync("Sesión",
                    "Cierra sesión y vuelve a entrar para aplicar los cambios en tu cuenta.",
                    "Continuar");
        }
        finally
        {
            GuardarUsuarioButton.IsEnabled = true;
        }
    }

    private void OnCancelarUsuarioClicked(object sender, EventArgs e) =>
        CerrarFormularioUsuario();

    private void CerrarFormularioUsuario()
    {
        UsuarioFormOverlay.IsVisible = false;
        UsuarioFormError.IsVisible = false;
        _usuarioEditando = null;
    }

    private void OnCerrarUsuariosClicked(object sender, EventArgs e) =>
        UsuariosOverlay.IsVisible = false;

    private void MostrarErrorUsuario(string mensaje)
    {
        UsuarioFormError.Text = mensaje;
        UsuarioFormError.IsVisible = true;
    }

    private void OnEditarConfiguracionClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar))
        {
            _ = DisplayAlertAsync("Sin permiso",
                "Solo el dueño puede cambiar la configuración.", "Continuar");
            return;
        }

        var config = _config.ObtenerConfiguracion().Value;
        if (config == null)
        {
            _ = DisplayAlertAsync("No se pudo abrir",
                "No se encontró la configuración de la tienda.", "Continuar");
            return;
        }

        ConfigNombreEntry.Text = config.NombreTienda;
        ConfigGiroEntry.Text = config.Giro;
        ConfigImpuestoEntry.Text =
            config.ImpuestoPredeterminado.ToString("0.##", CultureInfo.InvariantCulture);
        ConfigComisionEntry.Text =
            config.ComisionTarjeta.ToString("0.##", CultureInfo.InvariantCulture);
        ConfigFondoEntry.Text = Pesos(config.FondoCajaInicial);
        ConfigFiadoSwitch.IsToggled = config.FiadoActivado;
        ConfigLimiteEntry.Text = Pesos(config.LimiCreditoCliente);
        ConfigRespaldoEntry.Text =
            config.RespaldoIntervaloDias.ToString(CultureInfo.InvariantCulture);
        ConfigErrorLabel.IsVisible = false;
        ConfigOverlay.IsVisible = true;
    }

    private void OnCancelarConfiguracionClicked(object sender, EventArgs e)
    {
        ConfigOverlay.IsVisible = false;
        ConfigErrorLabel.IsVisible = false;
    }

    private async void OnGuardarConfiguracionClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null ||
            !AutorizacionService.Puede(_sesion.Usuario, PermisoSistema.Configurar))
        {
            MostrarErrorConfig("Solo el dueño puede cambiar la configuración.");
            return;
        }

        ConfigErrorLabel.IsVisible = false;
        var nombre = ConfigNombreEntry.Text?.Trim() ?? string.Empty;
        var giro = ConfigGiroEntry.Text?.Trim() ?? string.Empty;
        var impuesto = LeerPorcentaje(ConfigImpuestoEntry.Text);
        var comision = LeerPorcentaje(ConfigComisionEntry.Text);
        var fondo = LeerCentavos(ConfigFondoEntry.Text);
        var fiado = ConfigFiadoSwitch.IsToggled;
        var limite = fiado ? LeerCentavos(ConfigLimiteEntry.Text) : 0L;
        var respaldo = LeerDias(ConfigRespaldoEntry.Text);

        if (nombre.Length == 0 || giro.Length == 0 ||
            !impuesto.HasValue || !comision.HasValue || !fondo.HasValue ||
            !limite.HasValue || !respaldo.HasValue)
        {
            MostrarErrorConfig(
                "Revisa los datos: nombre y giro son obligatorios; los importes y días no pueden ser negativos.");
            return;
        }

        var config = new ConfiguracionTienda
        {
            NombreTienda = nombre,
            Giro = giro,
            ImpuestoPredeterminado = impuesto.Value,
            ComisionTarjeta = comision.Value,
            FondoCajaInicial = fondo.Value,
            FiadoActivado = fiado,
            LimiCreditoCliente = limite.Value,
            RespaldoIntervaloDias = respaldo.Value
        };

        GuardarConfiguracionButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _config.ActualizarConfiguracion(config);
            if (resultado.IsError)
            {
                MostrarErrorConfig(resultado.Message);
                return;
            }

            ConfigOverlay.IsVisible = false;
            Cargar();
            await DisplayAlertAsync("Configuración actualizada", resultado.Message, "Continuar");
        }
        finally
        {
            GuardarConfiguracionButton.IsEnabled = true;
        }
    }

    private void MostrarErrorConfig(string mensaje)
    {
        ConfigErrorLabel.Text = mensaje;
        ConfigErrorLabel.IsVisible = true;
    }

    private static decimal? LeerPorcentaje(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("%", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var valor))
            return null;

        return valor is >= 0 and <= 100 ? valor : null;
    }

    private static int? LeerDias(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (!int.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                out var valor) || valor is < 0 or > 365)
            return null;

        return valor;
    }

    private static long? LeerCentavos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("$", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var pesos))
            return null;

        if (pesos < 0 || pesos > 1_000_000)
            return null;

        return Dinero.DePesos(pesos);
    }

    private static string Pesos(long centavos) =>
        (centavos / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private async void OnCrearRespaldoClicked(object sender, EventArgs e)
    {        CrearRespaldoButton.IsEnabled = false;
        try
        {
            _sesion.Aplicar();
            var resultado = _respaldos.CrearRespaldo("manual");
            await DisplayAlertAsync(
                resultado.IsError ? "No se pudo respaldar" : "Respaldo creado",
                resultado.IsError ? resultado.Message : $"{resultado.Message}\n\n{resultado.Value}",
                "Continuar");
            Cargar();
        }
        finally
        {
            CrearRespaldoButton.IsEnabled = true;
        }
    }

    private async void OnCerrarSesionClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlertAsync("Cerrar sesión", "¿Quieres cerrar la sesión actual?", "Sí", "Cancelar");
        if (!confirmar)
            return;

        _sesion.Aplicar();
        var respaldo = _respaldos.EjecutarAutomaticoSiCorresponde();
        if (respaldo.IsError)
            await DisplayAlertAsync("Respaldo pendiente", respaldo.Message, "Continuar");

        _sesion.Cerrar();
        _navegacion.MostrarLogin();
    }
}
