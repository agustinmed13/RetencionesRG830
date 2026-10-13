using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// El certificado en PDF. Por ahora sólo se prueba la regla 10: una operación que
/// no retuvo no tiene certificado que generar. El contenido del PDF no se prueba
/// automáticamente; se revisa a mano contra la hoja Certificado de la planilla.
/// </summary>
public class GeneradorCertificadoPdfTests
{
    [Fact]
    public void Una_operacion_sin_certificado_no_genera_PDF()
    {
        var operacion = new Operacion
        {
            NumeroCertificado = null,
            MontoRetenido = 0m,
            FechaRetencion = new DateOnly(2026, 9, 4),
            Cliente = new Cliente { Cuit = "30-61234567-4", RazonSocial = "Metalúrgica del Valle S.A." },
            Proveedor = new Proveedor { Cuit = "20-31456789-8", RazonSocial = "Gómez, Carlos Alberto" },
            Regimen = new Regimen { Codigo = 78 }
        };

        var error = Assert.Throws<InvalidOperationException>(() => new GeneradorCertificadoPdf().Generar(operacion));
        Assert.Contains("no emitió certificado", error.Message);
    }
}
