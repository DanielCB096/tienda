using System.Globalization;
using Microsoft.Maui.Devices;
using SistemaTienda.Application.Services;
using SistemaTienda.Core;
using SistemaTienda.Maui.Models;
using SistemaTienda.Maui.Services;

namespace SistemaTienda.Maui.Pages;

public partial class InventarioPage : ContentPage
{
    private enum VistaInventario
    {
        Detalle,
        EditorNuevo,
        EditorExistente,
        Entrada,
        Merma,
        Ajuste,
        Historial
    }

    private const int StockMaximo = 1_000_000;

    private readonly InventoryService _inventario;
    private readonly ConfiguracionService _configuracion;
    private readonly SesionApp _sesion;

    private VistaInventario _vista;
    private Producto? _producto;
    private bool _soloStockBajo;
    private bool _volverADetalle;
    private int _totalProductos;
    private int _totalBajos;

    private bool _puedeEditar;
    private bool _puedeCambiarPrecios;
    private bool _puedeEntrada;
    private bool _puedeMerma;
    private bool _puedeAjuste;
    private decimal _impuestoPredeterminado = 16m;

    public InventarioPage(InventoryService inventario, ConfiguracionService configuracion,
        SesionApp sesion)
    {
        InitializeComponent();
        _inventario = inventario;
        _configuracion = configuracion;
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var usuario = _sesion.Usuario;
        if (usuario == null)
            return;

        _sesion.Aplicar();
        _impuestoPredeterminado =
            _configuracion.ObtenerConfiguracion().Value?.ImpuestoPredeterminado ?? 16m;

        UsuarioLabel.Text = $"Sesión: {usuario.Nombre} · Rol: {usuario.Rol}";
        _puedeEditar = AutorizacionService.Puede(usuario, PermisoSistema.EditarProducto);
        _puedeCambiarPrecios = AutorizacionService.Puede(usuario, PermisoSistema.CambiarPrecios);
        _puedeEntrada = AutorizacionService.Puede(usuario, PermisoSistema.RegistrarEntrada);
        _puedeMerma = AutorizacionService.Puede(usuario, PermisoSistema.RegistrarMerma);
        _puedeAjuste = AutorizacionService.Puede(usuario, PermisoSistema.AjustarInventario);
        var puedeConsultar = AutorizacionService.Puede(usuario, PermisoSistema.Consultar);

        NuevoProductoButton.IsVisible = _puedeEditar;
        EditarButton.IsVisible = _puedeEditar;
        EntradaButton.IsVisible = _puedeEntrada;
        MermaButton.IsVisible = _puedeMerma;
        AjusteButton.IsVisible = _puedeAjuste;
        HistorialButton.IsVisible = puedeConsultar;
        SoloConsultaLabel.IsVisible =
            !_puedeEditar && !_puedeEntrada && !_puedeMerma && !_puedeAjuste;

        PanelOverlay.IsVisible = false;
        BuscarEntry.Text = string.Empty;
        _soloStockBajo = false;
        CargarCatalogo();

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            BuscarEntry.Focus();
    }

    private void CargarCatalogo(string? busqueda = null)
    {
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();

        var todos = _inventario.ObtenerProductos();
        var bajos = todos.Where(p => p.Stock <= p.StockMinimo).ToList();
        _totalProductos = todos.Count;
        _totalBajos = bajos.Count;

        TotalProductosLabel.Text = _totalProductos.ToString(CultureInfo.InvariantCulture);
        ValorizadoLabel.Text = Formato.Moneda(_inventario.ObtenerValorizadoInventario());
        StockBajoLabel.Text = _totalBajos.ToString(CultureInfo.InvariantCulture);
        StockBajoLabel.TextColor = _totalBajos > 0
            ? Color.FromArgb("#B3261E")
            : Color.FromArgb("#2E6B4F");
        ActualizarFiltros();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var coincidencias = _inventario.BuscarPorNombre(busqueda);
            if (_soloStockBajo)
                coincidencias = coincidencias.Where(p => p.Stock <= p.StockMinimo).ToList();

            MostrarLista(coincidencias, "No hay coincidencias.");
            ResultadosLabel.Text = $"{coincidencias.Count} resultado(s)";
            return;
        }

        var lista = _soloStockBajo ? bajos : todos;
        MostrarLista(lista, _soloStockBajo
            ? "No hay productos en stock bajo."
            : "No hay productos registrados. Usa + Nuevo o escanea un código.");
        ResultadosLabel.Text = $"{lista.Count} producto(s)";
    }

    private void MostrarLista(IEnumerable<Producto> productos, string textoVacio)
    {
        ListaProductos.ItemsSource = null;
        ListaProductos.ItemsSource = productos
            .Select(p => new ProductoInventarioFila { Producto = p })
            .ToList();
        ListaVaciaLabel.Text = textoVacio;
    }

    private void ActualizarFiltros()
    {
        FiltroTodosButton.Text = $"Todos ({_totalProductos})";
        FiltroBajoButton.Text = $"Stock bajo ({_totalBajos})";

        var primario = Color.FromArgb("#2E6B4F");
        FiltroTodosButton.BackgroundColor = _soloStockBajo ? Colors.Transparent : primario;
        FiltroTodosButton.TextColor = _soloStockBajo ? primario : Colors.White;
        FiltroBajoButton.BackgroundColor = _soloStockBajo ? primario : Colors.Transparent;
        FiltroBajoButton.TextColor = _soloStockBajo ? Colors.White : primario;
    }

    private void OnFiltroTodosClicked(object sender, EventArgs e)
    {
        _soloStockBajo = false;
        BuscarEntry.Text = string.Empty;
        MostrarMensaje(null);
        CargarCatalogo();
    }

    private void OnFiltroBajoClicked(object sender, EventArgs e)
    {
        _soloStockBajo = true;
        BuscarEntry.Text = string.Empty;
        MostrarMensaje(null);
        CargarCatalogo();
    }

    private void OnRefrescando(object sender, EventArgs e)
    {
        var texto = BuscarEntry.Text?.Trim();
        CargarCatalogo(string.IsNullOrEmpty(texto) ? null : texto);
        Refresco.IsRefreshing = false;
    }

    private void OnBuscarClicked(object sender, EventArgs e) => _ = BuscarAsync();

    private void OnBuscarCompleted(object sender, EventArgs e) => _ = BuscarAsync();

    private async Task BuscarAsync()
    {
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();
        var texto = BuscarEntry.Text?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            _soloStockBajo = false;
            MostrarMensaje(null);
            CargarCatalogo();
            return;
        }

        var exacto = _inventario.BuscarPorCodigo(texto);
        if (exacto != null)
        {
            BuscarEntry.Text = string.Empty;
            MostrarMensaje(null);
            AbrirDetalle(exacto.Id);
            return;
        }

        var coincidencias = _inventario.BuscarPorNombre(texto);
        if (coincidencias.Count > 0)
        {
            if (_soloStockBajo)
                coincidencias = coincidencias.Where(p => p.Stock <= p.StockMinimo).ToList();

            MostrarLista(coincidencias, "No hay coincidencias.");
            ResultadosLabel.Text = $"{coincidencias.Count} resultado(s)";
            MostrarMensaje(coincidencias.Count == 0
                ? "No hay coincidencias con stock bajo. Pulsa Todos para ver el catálogo."
                : null);
            return;
        }

        if (EsCodigoBarras(texto))
        {
            if (!_puedeEditar)
            {
                MostrarMensaje($"No existe el código {texto} y tu rol no permite darlo de alta.");
                return;
            }

            var alta = await DisplayAlertAsync("Código no registrado",
                $"No existe un producto con el código {texto}. ¿Quieres darlo de alta?",
                "Sí, registrar", "No");
            BuscarEntry.Text = string.Empty;

            if (alta)
            {
                MostrarMensaje(null);
                AbrirEditorNuevo(texto);
            }
            else
            {
                CargarCatalogo();
                MostrarMensaje(null);
            }

            return;
        }

        MostrarLista(Array.Empty<Producto>(), "No hay coincidencias.");
        ResultadosLabel.Text = "0 resultados";
        MostrarMensaje($"No se encontró \"{texto}\". Revisa el código o pulsa Todos.");
    }

    private void OnVerProductoClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ProductoInventarioFila fila)
            return;

        AbrirDetalle(fila.Producto.Id);
    }

    private void AbrirDetalle(int productoId)
    {
        if (_sesion.Usuario == null)
            return;

        _sesion.Aplicar();
        var producto = _inventario.BuscarPorId(productoId);
        if (producto == null)
        {
            PanelOverlay.IsVisible = false;
            MostrarMensaje("No se encontró el producto.");
            return;
        }

        _producto = producto;
        _volverADetalle = false;

        DetalleCostoLabel.Text = Formato.Moneda(producto.Costo);
        DetallePrecioLabel.Text = Formato.Moneda(producto.Precio);
        DetalleStockLabel.Text = producto.Stock.ToString(CultureInfo.InvariantCulture);
        DetalleStockLabel.TextColor = producto.Stock <= producto.StockMinimo
            ? Color.FromArgb("#B3261E")
            : Color.FromArgb("#2E6B4F");
        DetalleMinimoLabel.Text = producto.StockMinimo.ToString(CultureInfo.InvariantCulture);
        DetalleImpuestoLabel.Text =
            $"{producto.Impuesto.ToString("0.##", CultureInfo.InvariantCulture)} %";
        DetalleValorLabel.Text = Formato.Moneda(Dinero.Multiplicar(producto.Costo, producto.Stock));

        BajoDetalleBorder.IsVisible = producto.Stock <= producto.StockMinimo;
        BajoDetalleLabel.Text = producto.Stock == 0
            ? "Sin stock disponible. Registra una entrada o ajusta el inventario."
            : $"Stock bajo: quedan {producto.Stock} y el mínimo es {producto.StockMinimo}.";

        var categoria = string.IsNullOrWhiteSpace(producto.Categoria)
            ? "Sin categoría"
            : producto.Categoria;
        AbrirOverlay(VistaInventario.Detalle, producto.Nombre,
            $"Código {producto.CodigoBarras} · {categoria}");
    }

    private void OnNuevoProductoClicked(object sender, EventArgs e)
    {
        if (!_puedeEditar)
        {
            MostrarMensaje("Tu rol no permite dar de alta productos.");
            return;
        }

        var texto = BuscarEntry.Text?.Trim();
        BuscarEntry.Text = string.Empty;
        AbrirEditorNuevo(EsCodigoBarras(texto) ? texto : null);
    }

    private void AbrirEditorNuevo(string? codigo = null)
    {
        _producto = null;
        _volverADetalle = false;

        CodigoEditor.Text = codigo ?? string.Empty;
        CodigoEditor.IsReadOnly = false;
        CodigoNotaLabel.IsVisible = false;
        NombreEditor.Text = string.Empty;
        CategoriaEditor.Text = string.Empty;
        CostoEditor.Text = string.Empty;
        PrecioEditor.Text = string.Empty;
        CostoEditor.IsEnabled = true;
        PrecioEditor.IsEnabled = true;
        EditorNotaPreciosLabel.IsVisible = false;
        CampoStockInicial.IsVisible = true;
        StockInicialEditor.Text = "0";
        StockEditorInfoLabel.IsVisible = false;
        StockMinimoEditor.Text = "5";
        ImpuestoEditor.Text = _impuestoPredeterminado.ToString("0.##", CultureInfo.InvariantCulture);
        EditorNotaStockLabel.IsVisible = false;

        AbrirOverlay(VistaInventario.EditorNuevo, "Nuevo producto",
            "Captura los datos del producto.");

        Dispatcher.Dispatch(() =>
        {
            if (string.IsNullOrWhiteSpace(codigo))
                CodigoEditor.Focus();
            else
                NombreEditor.Focus();
        });
    }

    private void OnEditarClicked(object sender, EventArgs e)
    {
        if (!_puedeEditar)
        {
            MostrarMensaje("Tu rol no permite editar productos.");
            return;
        }

        if (_producto == null)
            return;

        AbrirEditorExistente();
    }

    private void AbrirEditorExistente()
    {
        if (_producto == null)
            return;

        var actual = _inventario.BuscarPorId(_producto.Id);
        if (actual == null)
        {
            MostrarErrorOverlay("No se encontró el producto.");
            return;
        }

        _producto = actual;
        _volverADetalle = true;

        CodigoEditor.Text = actual.CodigoBarras;
        CodigoEditor.IsReadOnly = true;
        CodigoNotaLabel.IsVisible = true;
        NombreEditor.Text = actual.Nombre;
        CategoriaEditor.Text = actual.Categoria;
        CostoEditor.Text = Dinero.APesos(actual.Costo).ToString("0.00", CultureInfo.InvariantCulture);
        PrecioEditor.Text = Dinero.APesos(actual.Precio).ToString("0.00", CultureInfo.InvariantCulture);
        CostoEditor.IsEnabled = _puedeCambiarPrecios;
        PrecioEditor.IsEnabled = _puedeCambiarPrecios;
        EditorNotaPreciosLabel.IsVisible = !_puedeCambiarPrecios;
        CampoStockInicial.IsVisible = false;
        StockEditorInfoLabel.IsVisible = true;
        StockEditorInfoLabel.Text =
            $"Stock actual: {actual.Stock} (solo cambia con ventas, entradas, mermas o ajustes).";
        StockMinimoEditor.Text = actual.StockMinimo.ToString(CultureInfo.InvariantCulture);
        ImpuestoEditor.Text = actual.Impuesto.ToString("0.##", CultureInfo.InvariantCulture);
        EditorNotaStockLabel.IsVisible = true;

        AbrirOverlay(VistaInventario.EditorExistente, "Editar producto", actual.Nombre);
        Dispatcher.Dispatch(() => NombreEditor.Focus());
    }

    private void OnEntradaClicked(object sender, EventArgs e)
    {
        if (!_puedeEntrada || _producto == null)
            return;

        var actual = _inventario.BuscarPorId(_producto.Id);
        if (actual == null)
        {
            MostrarErrorOverlay("No se encontró el producto.");
            return;
        }

        _producto = actual;
        _volverADetalle = true;

        StockEntradaLabel.Text =
            $"Stock actual: {actual.Stock} · Costo actual: {Formato.Moneda(actual.Costo)}";
        CantidadEntradaEntry.Text = string.Empty;
        CostoEntradaEntry.Text = Dinero.APesos(actual.Costo)
            .ToString("0.00", CultureInfo.InvariantCulture);
        MotivoEntradaEntry.Text = "Compra";
        TotalEntradaLabel.Text = "Total de la entrada: —";

        AbrirOverlay(VistaInventario.Entrada, "Registrar entrada", actual.Nombre);
        Dispatcher.Dispatch(() => CantidadEntradaEntry.Focus());
    }

    private void OnMermaClicked(object sender, EventArgs e)
    {
        if (!_puedeMerma || _producto == null)
            return;

        var actual = _inventario.BuscarPorId(_producto.Id);
        if (actual == null)
        {
            MostrarErrorOverlay("No se encontró el producto.");
            return;
        }

        _producto = actual;
        _volverADetalle = true;

        StockMermaLabel.Text =
            $"Stock actual: {actual.Stock} · Costo: {Formato.Moneda(actual.Costo)}";
        CantidadMermaEntry.Text = string.Empty;
        MotivoMermaEntry.Text = string.Empty;
        CostoPerdidoLabel.Text = "Costo perdido estimado: —";

        AbrirOverlay(VistaInventario.Merma, "Registrar merma", actual.Nombre);
        Dispatcher.Dispatch(() => CantidadMermaEntry.Focus());
    }

    private void OnAjusteClicked(object sender, EventArgs e)
    {
        if (!_puedeAjuste || _producto == null)
            return;

        var actual = _inventario.BuscarPorId(_producto.Id);
        if (actual == null)
        {
            MostrarErrorOverlay("No se encontró el producto.");
            return;
        }

        _producto = actual;
        _volverADetalle = true;

        StockAjusteLabel.Text = $"Stock actual: {actual.Stock}";
        NuevoStockAjusteEntry.Text = actual.Stock.ToString(CultureInfo.InvariantCulture);
        MotivoAjusteEntry.Text = string.Empty;
        DiferenciaAjusteLabel.Text = "Diferencia: 0 unidades";

        AbrirOverlay(VistaInventario.Ajuste, "Ajustar stock", actual.Nombre);
        Dispatcher.Dispatch(() => MotivoAjusteEntry.Focus());
    }

    private void OnHistorialClicked(object sender, EventArgs e)
    {
        var usuario = _sesion.Usuario;
        if (usuario == null || _producto == null ||
            !AutorizacionService.Puede(usuario, PermisoSistema.Consultar))
            return;

        _volverADetalle = true;
        var movimientos = _inventario.HistorialMovimientos(_producto.Id);
        ListaHistorial.ItemsSource = movimientos
            .Select(m => new MovimientoInventarioFila { Movimiento = m })
            .ToList();
        HistorialResumenLabel.Text = movimientos.Count == 0
            ? "Sin movimientos registrados."
            : $"{movimientos.Count} movimiento(s); se muestran los últimos 200.";

        AbrirOverlay(VistaInventario.Historial, "Movimientos", _producto.Nombre);
    }

    private void OnEntradaChanged(object sender, TextChangedEventArgs e)
    {
        var cantidad = LeerEntero(CantidadEntradaEntry.Text, 1, StockMaximo, out var unidades)
            ? unidades
            : 0;
        var costo = LeerCentavos(CostoEntradaEntry.Text);

        TotalEntradaLabel.Text = cantidad > 0 && costo.HasValue
            ? $"Total de la entrada: {Formato.Moneda(Dinero.Multiplicar(costo.Value, cantidad))}"
            : "Total de la entrada: —";
    }

    private void OnMermaChanged(object sender, TextChangedEventArgs e)
    {
        var cantidad = LeerEntero(CantidadMermaEntry.Text, 1, StockMaximo, out var unidades)
            ? unidades
            : 0;

        CostoPerdidoLabel.Text = cantidad > 0 && _producto != null
            ? $"Costo perdido estimado: {Formato.Moneda(Dinero.Multiplicar(_producto.Costo, cantidad))}"
            : "Costo perdido estimado: —";
    }

    private void OnAjusteChanged(object sender, TextChangedEventArgs e)
    {
        if (_producto == null ||
            !LeerEntero(NuevoStockAjusteEntry.Text, 0, StockMaximo, out var nuevoStock))
        {
            DiferenciaAjusteLabel.Text = "Diferencia: —";
            return;
        }

        var diferencia = (long)nuevoStock - _producto.Stock;
        var signo = diferencia > 0
            ? $"+{diferencia}"
            : diferencia.ToString(CultureInfo.InvariantCulture);
        DiferenciaAjusteLabel.Text = $"Diferencia: {signo} unidades";
    }

    private void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_sesion.Usuario == null || _vista == VistaInventario.Detalle)
            return;

        _sesion.Aplicar();
        GuardarButton.IsEnabled = false;

        try
        {
            switch (_vista)
            {
                case VistaInventario.EditorNuevo:
                    GuardarNuevo();
                    break;
                case VistaInventario.EditorExistente:
                    GuardarEdicion();
                    break;
                case VistaInventario.Entrada:
                    GuardarEntrada();
                    break;
                case VistaInventario.Merma:
                    GuardarMerma();
                    break;
                case VistaInventario.Ajuste:
                    GuardarAjuste();
                    break;
            }
        }
        catch (Exception ex)
        {
            MostrarErrorOverlay($"No se pudo completar la operación: {ex.Message}");
        }
        finally
        {
            GuardarButton.IsEnabled = true;
        }
    }

    private void GuardarNuevo()
    {
        var codigo = CodigoEditor.Text?.Trim() ?? string.Empty;
        if (codigo.Length == 0)
        {
            MostrarErrorOverlay("Escanea o escribe el código de barras.");
            return;
        }

        var nombre = NombreEditor.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            MostrarErrorOverlay("Escribe el nombre del producto.");
            return;
        }

        var costo = LeerCentavos(CostoEditor.Text);
        if (!costo.HasValue)
        {
            MostrarErrorOverlay("Escribe un costo válido (0.00 o mayor).");
            return;
        }

        var precio = LeerCentavos(PrecioEditor.Text);
        if (!precio.HasValue)
        {
            MostrarErrorOverlay("Escribe un precio válido (0.00 o mayor).");
            return;
        }

        if (!LeerEntero(StockInicialEditor.Text, 0, StockMaximo, out var stockInicial))
        {
            MostrarErrorOverlay("El stock inicial debe ser un entero de 0 o mayor.");
            return;
        }

        if (!LeerEntero(StockMinimoEditor.Text, 0, StockMaximo, out var stockMinimo))
        {
            MostrarErrorOverlay("El stock mínimo debe ser un entero de 0 o mayor.");
            return;
        }

        var impuesto = LeerPorcentaje(ImpuestoEditor.Text);
        if (!impuesto.HasValue)
        {
            MostrarErrorOverlay("El impuesto debe ser un porcentaje entre 0 y 100.");
            return;
        }

        var producto = new Producto
        {
            CodigoBarras = codigo,
            Nombre = nombre,
            Categoria = CategoriaEditor.Text?.Trim() ?? string.Empty,
            Costo = costo.Value,
            Precio = precio.Value,
            Stock = stockInicial,
            StockMinimo = stockMinimo,
            Impuesto = impuesto.Value
        };

        var resultado = _inventario.AgregarProducto(producto);
        if (resultado.IsError)
        {
            MostrarErrorOverlay(resultado.Message);
            return;
        }

        OperacionExitosa(resultado.Message, producto.Id);
    }

    private void GuardarEdicion()
    {
        if (_producto == null)
            return;

        var nombre = NombreEditor.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            MostrarErrorOverlay("Escribe el nombre del producto.");
            return;
        }

        long costo;
        long precio;
        if (_puedeCambiarPrecios)
        {
            var costoLeido = LeerCentavos(CostoEditor.Text);
            if (!costoLeido.HasValue)
            {
                MostrarErrorOverlay("Escribe un costo válido (0.00 o mayor).");
                return;
            }

            var precioLeido = LeerCentavos(PrecioEditor.Text);
            if (!precioLeido.HasValue)
            {
                MostrarErrorOverlay("Escribe un precio válido (0.00 o mayor).");
                return;
            }

            costo = costoLeido.Value;
            precio = precioLeido.Value;
        }
        else
        {
            costo = _producto.Costo;
            precio = _producto.Precio;
        }

        if (!LeerEntero(StockMinimoEditor.Text, 0, StockMaximo, out var stockMinimo))
        {
            MostrarErrorOverlay("El stock mínimo debe ser un entero de 0 o mayor.");
            return;
        }

        var impuesto = LeerPorcentaje(ImpuestoEditor.Text);
        if (!impuesto.HasValue)
        {
            MostrarErrorOverlay("El impuesto debe ser un porcentaje entre 0 y 100.");
            return;
        }

        var actualizado = new Producto
        {
            Id = _producto.Id,
            CodigoBarras = _producto.CodigoBarras,
            Nombre = nombre,
            Categoria = CategoriaEditor.Text?.Trim() ?? string.Empty,
            Costo = costo,
            Precio = precio,
            Stock = _producto.Stock,
            StockMinimo = stockMinimo,
            Impuesto = impuesto.Value,
            FechaCreacion = _producto.FechaCreacion,
            UltimaActualizacion = _producto.UltimaActualizacion
        };

        var resultado = _inventario.ActualizarProducto(actualizado);
        if (resultado.IsError)
        {
            MostrarErrorOverlay(resultado.Message);
            return;
        }

        OperacionExitosa(resultado.Message, actualizado.Id);
    }

    private void GuardarEntrada()
    {
        if (_producto == null)
            return;

        if (!LeerEntero(CantidadEntradaEntry.Text, 1, StockMaximo, out var cantidad))
        {
            MostrarErrorOverlay("La cantidad debe ser un entero mayor a cero.");
            return;
        }

        var costo = LeerCentavos(CostoEntradaEntry.Text);
        if (!costo.HasValue)
        {
            MostrarErrorOverlay("Escribe un costo unitario válido (0.00 o mayor).");
            return;
        }

        var motivo = MotivoEntradaEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            motivo = "Compra";

        var resultado = _inventario.RegistrarEntrada(
            _producto.Id, cantidad, costo.Value, _sesion.Usuario!.Nombre, motivo);
        if (resultado.IsError)
        {
            MostrarErrorOverlay(resultado.Message);
            return;
        }

        OperacionExitosa(resultado.Message, _producto.Id);
    }

    private void GuardarMerma()
    {
        if (_producto == null)
            return;

        if (!LeerEntero(CantidadMermaEntry.Text, 1, StockMaximo, out var cantidad))
        {
            MostrarErrorOverlay("La cantidad debe ser un entero mayor a cero.");
            return;
        }

        var motivo = MotivoMermaEntry.Text?.Trim() ?? string.Empty;
        if (motivo.Length == 0)
        {
            MostrarErrorOverlay("Escribe el motivo de la merma.");
            return;
        }

        var resultado = _inventario.RegistrarMerma(
            _producto.Id, cantidad, motivo, _sesion.Usuario!.Nombre);
        if (resultado.IsError)
        {
            MostrarErrorOverlay(resultado.Message);
            return;
        }

        OperacionExitosa(resultado.Message, _producto.Id);
    }

    private void GuardarAjuste()
    {
        if (_producto == null)
            return;

        if (!LeerEntero(NuevoStockAjusteEntry.Text, 0, StockMaximo, out var nuevoStock))
        {
            MostrarErrorOverlay("El nuevo stock debe ser un entero de 0 o mayor.");
            return;
        }

        var motivo = MotivoAjusteEntry.Text?.Trim() ?? string.Empty;
        if (motivo.Length == 0)
        {
            MostrarErrorOverlay("Escribe el motivo del ajuste.");
            return;
        }

        if (nuevoStock == _producto.Stock)
        {
            MostrarErrorOverlay($"El stock ya es {_producto.Stock}; no hay cambios que registrar.");
            return;
        }

        var resultado = _inventario.AjustarStock(
            _producto.Id, nuevoStock, motivo, _sesion.Usuario!.Nombre);
        if (resultado.IsError)
        {
            MostrarErrorOverlay(resultado.Message);
            return;
        }

        OperacionExitosa(resultado.Message, _producto.Id);
    }

    private void OperacionExitosa(string mensaje, int productoId)
    {
        CargarCatalogo();
        MostrarMensaje(null);
        AbrirDetalle(productoId);
        MostrarAvisoOverlay(mensaje);

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            BuscarEntry.Focus();
    }

    private void AbrirOverlay(VistaInventario vista, string titulo, string subtitulo)
    {
        _vista = vista;
        OverlayTitulo.Text = titulo;
        OverlaySubtitulo.Text = subtitulo;

        VistaDetalle.IsVisible = vista == VistaInventario.Detalle;
        VistaEditor.IsVisible = vista is VistaInventario.EditorNuevo or VistaInventario.EditorExistente;
        VistaEntrada.IsVisible = vista == VistaInventario.Entrada;
        VistaMerma.IsVisible = vista == VistaInventario.Merma;
        VistaAjuste.IsVisible = vista == VistaInventario.Ajuste;
        VistaHistorial.IsVisible = vista == VistaInventario.Historial;

        GuardarButton.IsVisible = vista is not VistaInventario.Detalle
            and not VistaInventario.Historial;
        GuardarButton.Text = vista switch
        {
            VistaInventario.EditorNuevo => "Guardar producto",
            VistaInventario.EditorExistente => "Guardar cambios",
            VistaInventario.Entrada => "Registrar entrada",
            VistaInventario.Merma => "Registrar merma",
            VistaInventario.Ajuste => "Ajustar stock",
            _ => "Guardar"
        };
        GuardarButton.IsEnabled = true;

        CerrarOverlayButton.Text = vista == VistaInventario.Detalle
            ? "Cerrar"
            : _volverADetalle ? "Volver" : "Cancelar";

        OverlayAvisoLabel.IsVisible = false;
        OverlayErrorLabel.IsVisible = false;
        PanelOverlay.IsVisible = true;
        OverlayScroll.ScrollToAsync(0, 0, false);
    }

    private void OnCerrarOverlayClicked(object sender, EventArgs e)
    {
        if (_vista != VistaInventario.Detalle && _volverADetalle && _producto != null)
        {
            AbrirDetalle(_producto.Id);
            return;
        }

        PanelOverlay.IsVisible = false;
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            BuscarEntry.Focus();
    }

    private void MostrarErrorOverlay(string mensaje)
    {
        OverlayErrorLabel.Text = mensaje;
        OverlayErrorLabel.IsVisible = true;
        OverlayAvisoLabel.IsVisible = false;
    }

    private void MostrarAvisoOverlay(string mensaje)
    {
        OverlayAvisoLabel.Text = mensaje;
        OverlayAvisoLabel.IsVisible = !string.IsNullOrWhiteSpace(mensaje);
        OverlayErrorLabel.IsVisible = false;
    }

    private void MostrarMensaje(string? mensaje)
    {
        MensajeLabel.Text = mensaje ?? string.Empty;
        MensajeLabel.IsVisible = !string.IsNullOrEmpty(mensaje);
    }

    private static bool EsCodigoBarras(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) &&
        texto.Length >= 8 &&
        texto.All(char.IsDigit);

    private static bool LeerEntero(string? texto, int minimo, int maximo, out int valor)
    {
        valor = 0;
        if (!int.TryParse((texto ?? string.Empty).Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var numero))
            return false;

        if (numero < minimo || numero > maximo)
            return false;

        valor = numero;
        return true;
    }

    private static long? LeerCentavos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace("$", string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var pesos))
            return null;

        if (pesos < 0 || pesos > 1_000_000)
            return null;

        return Dinero.DePesos(pesos);
    }

    private static decimal? LeerPorcentaje(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var limpio = texto.Replace(" ", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var porcentaje))
            return null;

        return porcentaje is < 0 or > 100 ? null : porcentaje;
    }
}
