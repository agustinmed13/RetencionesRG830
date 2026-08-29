using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Arma el certificado de retención en PDF, replicando la hoja "Certificado"
/// de la planilla original: encabezado, datos del agente de retención, datos
/// del sujeto retenido, detalle de la retención practicada y firma.
///
/// Vive en Infrastructure porque depende de una librería externa (QuestPDF).
/// Recibe la operación ya cargada y devuelve los bytes del archivo: no toca
/// la base de datos ni sabe nada de pantallas.
/// </summary>
public class GeneradorCertificadoPdf
{
    public byte[] Generar(Operacion operacion)
    {
        var cliente = operacion.Cliente
            ?? throw new InvalidOperationException("La operación no trae cargado el Cliente.");
        var proveedor = operacion.Proveedor
            ?? throw new InvalidOperationException("La operación no trae cargado el Proveedor.");
        var regimen = operacion.Regimen
            ?? throw new InvalidOperationException("La operación no trae cargado el Régimen.");

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Content().Column(col =>
                {
                    col.Spacing(16);

                    col.Item().AlignCenter()
                        .Text("CERTIFICADO DE RETENCIÓN IMPUESTO A LAS GANANCIAS")
                        .Bold().FontSize(13);

                    col.Item().AlignRight().Column(c =>
                    {
                        c.Item().Text($"Certificado N° {operacion.NumeroCertificado}").Bold();
                        c.Item().Text($"Fecha: {operacion.FechaRetencion:dd/MM/yyyy}");
                    });

                    Seccion(col, "DATOS DEL AGENTE DE RETENCIÓN", new[]
                    {
                        ("CUIT:", cliente.Cuit),
                        ("DENOMINACIÓN:", cliente.RazonSocial),
                        ("DOMICILIO:", cliente.Domicilio),
                        ("LOCALIDAD - PCIA:", cliente.Localidad),
                    });

                    Seccion(col, "DATOS DEL SUJETO RETENIDO", new[]
                    {
                        ("CUIT:", proveedor.Cuit),
                        ("DENOMINACIÓN:", proveedor.RazonSocial),
                        ("DOMICILIO:", proveedor.Domicilio),
                        ("LOCALIDAD - PCIA:", proveedor.Localidad),
                    });

                    Seccion(col, "DATOS DE LA RETENCIÓN PRACTICADA", new[]
                    {
                        ("IMPUESTO:", "Impuesto a las Ganancias"),
                        ("RÉGIMEN:", regimen.Descripcion),
                        ("CÓDIGO DE RÉGIMEN:", regimen.Codigo.ToString()),
                        ("COMPROBANTE:", $"{DescribirComprobante(operacion.TipoComprobante)} " +
                                         $"{operacion.PuntoVenta:D5}-{operacion.NumeroComprobante:D8}"),
                        ("FECHA DEL COMPROBANTE:", operacion.FechaComprobante.ToString("dd/MM/yyyy")),
                        ("MONTO DEL COMPROBANTE:", operacion.ImporteComprobante.ToString("C2")),
                        ("MONTO DE LA RETENCIÓN:", operacion.MontoRetenido.ToString("C2")),
                    });

                    col.Item().PaddingTop(50).AlignRight().Column(c =>
                    {
                        c.Item().Text("………………………………………");
                        c.Item().Text(cliente.NombreFirmante);
                        c.Item().Text(cliente.CargoFirmante);
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Dibuja un título de sección y sus filas de etiqueta/valor.</summary>
    private static void Seccion(ColumnDescriptor col, string titulo, (string Etiqueta, string Valor)[] filas)
    {
        col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(titulo).Bold();

        foreach (var fila in filas)
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(170).Text(fila.Etiqueta).Bold();
                row.RelativeItem().Text(fila.Valor);
            });
        }
    }

    /// <summary>Traduce el código AFIP del comprobante a texto legible.</summary>
    private static string DescribirComprobante(int tipo) => tipo switch
    {
        1 => "Factura",
        2 => "Nota de débito",
        3 => "Nota de crédito",
        4 => "Recibo",
        _ => $"Comprobante tipo {tipo}"
    };
}