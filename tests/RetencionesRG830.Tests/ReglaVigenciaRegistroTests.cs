using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// La regla 9 en el registro de operaciones: cada operación se calcula con la
/// versión del régimen vigente a su fecha de retención, y el acumulado del mes
/// se arma por CÓDIGO de régimen, no por versión.
///
/// Hay dos versiones del 78 cargadas, con valores inventados para que la
/// diferencia se vea (no son valores de AFIP):
/// - vieja: 2 % a inscriptos, $ 224.000 no sujeto, desde el 01/01/2025;
/// - nueva: 3 % a inscriptos, $ 300.000 no sujeto, desde el 01/09/2026.
///
/// El primer test es la reproducción del 11/10/2026 convertida en test: antes del
/// arreglo el sistema daba $ 3.520 y $ 0 (retenía $ 8.480 de menos en el mes).
/// </summary>
public class ReglaVigenciaRegistroTests : IDisposable
{
    // SQLite en memoria, o SQL Server si está definida SIGERE_PRUEBAS_SQLSERVER.
    private readonly BaseDePrueba _base = new();
    private readonly DbContextOptions<RetencionesRG830DbContext> _opciones;

    private int _clienteId;
    private int _proveedorId;
    private int _version78Vieja;
    private int _version78Nueva;
    private int _regimen94;
    private long _ultimoComprobante;

    public ReglaVigenciaRegistroTests()
    {
        _opciones = _base.Opciones;

        using var db = new RetencionesRG830DbContext(_opciones);
        Sembrar(db);
    }

    public void Dispose() => _base.Dispose();

    private static Regimen Regimen78(decimal tasaInscripto, decimal noSujeto, DateOnly desde) => new()
    {
        Codigo = 78,
        Descripcion = "Enajenación de bienes muebles y bienes de cambio",
        TipoCalculo = TipoCalculoRegimen.TasaFija,
        TasaInscripto = tasaInscripto,
        TasaNoInscriptoHumana = 0.10m,
        TasaNoInscriptoResto = 0.10m,
        MontoNoSujetoARetencion = noSujeto,
        MinimoRetencion = 240m,
        VigenciaDesde = desde
    };

    private void Sembrar(RetencionesRG830DbContext db)
    {
        var cliente = new Cliente { Cuit = "30-61234567-4", RazonSocial = "Metalúrgica del Valle S.A." };
        db.Clientes.Add(cliente);
        db.SaveChanges();

        var proveedor = new Proveedor
        {
            ClienteId = cliente.Id,
            Cuit = "20-31456789-8",
            RazonSocial = "Gómez, Carlos Alberto",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        db.Proveedores.Add(proveedor);

        var vieja = Regimen78(0.02m, 224_000m, new DateOnly(2025, 1, 1));
        var nueva = Regimen78(0.03m, 300_000m, new DateOnly(2026, 9, 1));
        var r94 = new Regimen
        {
            Codigo = 94,
            Descripcion = "Locaciones de obra y/o servicios no ejecutados en relación de dependencia",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.28m,
            TasaNoInscriptoResto = 0.25m,
            MontoNoSujetoARetencion = 67_170m,
            MinimoRetencion = 240m,
            VigenciaDesde = new DateOnly(2025, 1, 1)
        };
        db.Regimenes.AddRange(vieja, nueva, r94);
        db.SaveChanges();

        _clienteId = cliente.Id;
        _proveedorId = proveedor.Id;
        _version78Vieja = vieja.Id;
        _version78Nueva = nueva.Id;
        _regimen94 = r94.Id;
    }

    /// <summary>
    /// Registra una operación con un contexto propio, como pasa en la aplicación,
    /// donde cada pedido tiene el suyo. El regimenId es lo que manda quien llama:
    /// puede ser cualquier versión del código.
    /// </summary>
    private async Task<Operacion> Registrar(int regimenId, DateOnly fecha, decimal gravado)
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return await new ServicioRegistroOperaciones(db).RegistrarAsync(new Operacion
        {
            ClienteId = _clienteId,
            ProveedorId = _proveedorId,
            RegimenId = regimenId,
            TipoComprobante = 1,
            PuntoVenta = 3,
            NumeroComprobante = ++_ultimoComprobante,
            FechaComprobante = fecha,
            FechaRetencion = fecha,
            ImporteComprobante = gravado * 1.21m,
            ImporteGravado = gravado,
            CreadoPorUsuarioId = 1
        });
    }

    [Fact]
    public async Task Dos_operaciones_del_mismo_mes_con_dos_versiones_cargadas_dan_3000_y_9000()
    {
        // La primera se manda con la versión VIEJA, como la elegía el desplegable
        // antes del arreglo. Las dos son de octubre: rige la nueva para las dos.
        var primera = await Registrar(_version78Vieja, new DateOnly(2026, 10, 5), 400_000m);
        var segunda = await Registrar(_version78Nueva, new DateOnly(2026, 10, 20), 300_000m);

        // Primera: 400.000 - 300.000 = 100.000 al 3 % = 3.000.
        Assert.Equal(400_000m, primera.BaseCalculoAcumulada);
        Assert.Equal(3_000m, primera.MontoRetenido);
        // Segunda: acumula las dos, 700.000 - 300.000 = 400.000 al 3 % = 12.000,
        // menos los 3.000 ya retenidos = 9.000.
        Assert.Equal(700_000m, segunda.BaseCalculoAcumulada);
        Assert.Equal(9_000m, segunda.MontoRetenido);

        // Las dos quedan guardadas con la versión que se usó de verdad.
        Assert.Equal(_version78Nueva, primera.RegimenId);
        Assert.Equal(_version78Nueva, segunda.RegimenId);
    }

    [Fact]
    public async Task Una_operacion_atrasada_se_calcula_con_la_version_vigente_a_su_fecha()
    {
        // Agosto de 2026: todavía rige la vieja, aunque se mande la nueva y aunque
        // hoy rija la nueva. 400.000 - 224.000 = 176.000 al 2 % = 3.520.
        var atrasada = await Registrar(_version78Nueva, new DateOnly(2026, 8, 20), 400_000m);

        Assert.Equal(3_520m, atrasada.MontoRetenido);
        Assert.Equal(_version78Vieja, atrasada.RegimenId);
    }

    [Fact]
    public async Task Si_la_version_cambia_a_mitad_de_mes_el_acumulado_junta_las_dos()
    {
        // Comportamiento actual, a confirmar con el contador: la segunda operación
        // aplica a todo el acumulado del mes los valores de SU versión (la nueva),
        // y descuenta lo retenido con la vieja.
        using (var db = new RetencionesRG830DbContext(_opciones))
        {
            var nueva = db.Regimenes.Single(r => r.Id == _version78Nueva);
            nueva.VigenciaDesde = new DateOnly(2026, 9, 15);
            db.SaveChanges();
        }

        // 10/09: rige la vieja. 400.000 - 224.000 = 176.000 al 2 % = 3.520.
        var antes = await Registrar(_version78Vieja, new DateOnly(2026, 9, 10), 400_000m);
        // 20/09: rige la nueva. 700.000 - 300.000 = 400.000 al 3 % = 12.000 - 3.520 = 8.480.
        var despues = await Registrar(_version78Vieja, new DateOnly(2026, 9, 20), 300_000m);

        Assert.Equal(3_520m, antes.MontoRetenido);
        Assert.Equal(_version78Vieja, antes.RegimenId);
        Assert.Equal(700_000m, despues.BaseCalculoAcumulada);
        Assert.Equal(8_480m, despues.MontoRetenido);
        Assert.Equal(_version78Nueva, despues.RegimenId);
    }

    [Fact]
    public async Task El_acumulado_por_codigo_no_mezcla_regimenes_distintos()
    {
        // Agrupar por código no tiene que juntar el 94 con el 78.
        await Registrar(_regimen94, new DateOnly(2026, 10, 3), 500_000m);
        var del78 = await Registrar(_version78Nueva, new DateOnly(2026, 10, 5), 400_000m);

        Assert.Equal(400_000m, del78.BaseCalculoAcumulada);
        Assert.Equal(3_000m, del78.MontoRetenido);
    }

    [Fact]
    public async Task Una_fecha_sin_version_vigente_no_se_puede_calcular()
    {
        // Antes del 01/01/2025 no rige ninguna versión del 78.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Registrar(_version78Vieja, new DateOnly(2024, 12, 20), 400_000m));

        Assert.Contains("78", error.Message);

        using var db = new RetencionesRG830DbContext(_opciones);
        Assert.Equal(0, db.Operaciones.Count());
    }

    [Fact]
    public async Task VersionVigenteAsync_resuelve_el_codigo_por_la_fecha()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        var servicio = new ServicioRegistroOperaciones(db);

        Assert.Equal(_version78Vieja, (await servicio.VersionVigenteAsync(78, new DateOnly(2026, 8, 31)))!.Id);
        Assert.Equal(_version78Nueva, (await servicio.VersionVigenteAsync(78, new DateOnly(2026, 9, 1)))!.Id);
        Assert.Null(await servicio.VersionVigenteAsync(78, new DateOnly(2024, 12, 31)));
        Assert.Null(await servicio.VersionVigenteAsync(31, new DateOnly(2026, 9, 1))); // código no cargado
    }
}
