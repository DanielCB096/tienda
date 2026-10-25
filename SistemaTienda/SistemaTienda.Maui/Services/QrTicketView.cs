using Net.Codecrete.QrCodeGenerator;

namespace SistemaTienda.Maui.Services;

/// <summary>
/// Dibuja el QR del ticket con módulos de tamaño entero para que se mantenga nítido
/// en pantallas de distinta densidad. No genera archivos ni usa red.
/// </summary>
public sealed class QrTicketView : GraphicsView
{
    private const int ModulosQuietos = 4;

    private QrTicketView(QrCode codigo)
    {
        Drawable = new QrTicketDrawable(codigo);
        WidthRequest = 220;
        HeightRequest = 220;
        HorizontalOptions = LayoutOptions.Center;
        BackgroundColor = Colors.White;
    }

    public static QrTicketView? Crear(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            return null;

        try
        {
            return new QrTicketView(QrCode.EncodeText(contenido, QrCode.Ecc.Medium));
        }
        catch
        {
            return null;
        }
    }

    private sealed class QrTicketDrawable : IDrawable
    {
        private readonly QrCode _codigo;

        public QrTicketDrawable(QrCode codigo) => _codigo = codigo;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.Antialias = false;
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            var lado = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var totalModulos = _codigo.Size + ModulosQuietos * 2;
            var modulo = Math.Max(1, (int)MathF.Floor(lado / totalModulos));
            var dibujado = modulo * totalModulos;
            var origenX = (int)(dirtyRect.X + (dirtyRect.Width - dibujado) / 2);
            var origenY = (int)(dirtyRect.Y + (dirtyRect.Height - dibujado) / 2);

            canvas.FillColor = Colors.Black;
            for (var y = -ModulosQuietos; y < _codigo.Size + ModulosQuietos; y++)
            {
                for (var x = -ModulosQuietos; x < _codigo.Size + ModulosQuietos; x++)
                {
                    if (!_codigo.GetModule(x, y))
                        continue;

                    canvas.FillRectangle(
                        origenX + (x + ModulosQuietos) * modulo,
                        origenY + (y + ModulosQuietos) * modulo,
                        modulo,
                        modulo);
                }
            }
        }
    }
}
