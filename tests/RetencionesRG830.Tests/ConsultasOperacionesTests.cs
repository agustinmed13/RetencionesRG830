using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Las consultas de las pantallas de Operaciones, que hasta noviembre de 2026 estaban
/// escritas en OperacionesController y por eso no tenían tests (el proyecto de tests
/// no referencia la Web). Se mudaron a ServicioRegistroOperaciones sin cambiarlas.
///
/// Estos tests fijan el comportamiento que tenían en el controlador: no lo mejoran.
/// Son además la base de la HU-15, que agrega a estas consultas el filtro por la
/// empresa del usuario Cliente.
///
/// Cada test lee con un contexto nuevo, como en producción, donde cada pedido tiene
/// el suyo: así cliente, proveedor y régimen tienen que venir de la base, y no de las
/// entidades que el contexto que las guardó ya tiene en memoria. Sin eso, un Include
/// que faltara pasaría inadvertido.
/// </summary>
public class ConsultasOperacionesTests : IDisposable
{
    private readonly BaseDePrueba _base = new();

    private int _clienteA;
    private int _clienteB;
    private int _proveedorA;        // de A, activo
    private int _proveedorAInactivo; // de A, de baja
    private int _proveedorA2;       // de A, activo; razón social que va antes
    private int _proveedorB;        // de B
    private int _operacionA0509;
    private int _operacionA1009;
    private int _operacionB1009;

    public ConsultasOperacionesTests()
    {
        using var db = _base.NuevoContexto();

        var a = new Cliente { Cuit = "30612345674", RazonSocial = "Metalúrgica del Valle S.A." };
        var b = new Cliente { Cuit = "30718452364", RazonSocial = "Agroinsumos del Norte S.R.L." };
        var regimen = new Regimen
        {
            Codigo = 78,
            Descripcion = "Enajenación de bienes muebles y bienes de cambio",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.10m,
            TasaNoInscriptoResto = 0.10m,
            MontoNoSujetoARetencion = 224000m,
            MinimoRetencion = 240m,
            VigenciaDesde = new DateOnly(2026, 1, 1)
        };
        db.Clientes.AddRange(a, b);
        db.Regimenes.Add(regimen);
        db.SaveChanges();

        var gomez = new Proveedor { ClienteId = a.Id, Cuit = "20314567898", RazonSocial = "Gómez, Carlos Alberto", InscriptoEnGanancias = true };
        var servicios = new Proveedor { ClienteId = a.Id, Cuit = "30645678911", RazonSocial = "Servicios del Sur S.H.", InscriptoEnGanancias = true, Activo = false };
        var ramirez = new Proveedor { ClienteId = a.Id, Cuit = "27325678947", RazonSocial = "Abril, Laura Beatriz", InscriptoEnGanancias = true };
        var transportes = new Proveedor { ClienteId = b.Id, Cuit = "30709275182", RazonSocial = "Transportes Altiplano S.A.", InscriptoEnGanancias = true };
        db.Proveedores.AddRange(gomez, servicios, ramirez, transportes);
        db.SaveChanges();

        // Se cargan en este orden a propósito: la del 10/09 de B tiene un Id mayor que
        // la del 10/09 de A, así el desempate por orden de carga se ve en el resultado.
        var a0509 = Operacion(a, gomez, regimen, new DateOnly(2026, 9, 5), 1);
        var a1009 = Operacion(a, gomez, regimen, new DateOnly(2026, 9, 10), 2);
        db.Operaciones.AddRange(a0509, a1009);
        db.SaveChanges();
        var b1009 = Operacion(b, transportes, regimen, new DateOnly(2026, 9, 10), 1);
        db.Operaciones.Add(b1009);
        db.SaveChanges();

        (_clienteA, _clienteB) = (a.Id, b.Id);
        (_proveedorA, _proveedorAInactivo, _proveedorA2, _proveedorB) = (gomez.Id, servicios.Id, ramirez.Id, transportes.Id);
        (_operacionA0509, _operacionA1009, _operacionB1009) = (a0509.Id, a1009.Id, b1009.Id);
    }

    public void Dispose() => _base.Dispose();

    private static Operacion Operacion(Cliente cliente, Proveedor proveedor, Regimen regimen, DateOnly fecha, int certificado) => new()
    {
        ClienteId = cliente.Id,
        ProveedorId = proveedor.Id,
        RegimenId = regimen.Id,
        TipoComprobante = 1,
        PuntoVenta = 1,
        NumeroComprobante = certificado,
        FechaComprobante = fecha,
        FechaRetencion = fecha,
        ImporteComprobante = 500_000m,
        ImporteGravado = 500_000m,
        NumeroCertificado = certificado,
        CreadoPorUsuarioId = 1
    };

    /// <summary>Un servicio con un contexto recién creado, que no tiene nada en memoria.</summary>
    private (ServicioRegistroOperaciones Servicio, RetencionesRG830DbContext Db) Nuevo()
    {
        var db = _base.NuevoContexto();
        return (new ServicioRegistroOperaciones(db), db);
    }

    [Fact]
    public async Task El_listado_sin_cliente_trae_todas_por_fecha_de_retencion_y_despues_por_orden_de_carga()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        var operaciones = await servicio.ListarOperacionesAsync(null);

        // Más reciente primero; en el mismo día, la cargada después va primero.
        Assert.Equal(new[] { _operacionB1009, _operacionA1009, _operacionA0509 }, operaciones.Select(o => o.Id));
        Assert.All(operaciones, o =>
        {
            Assert.NotNull(o.Cliente);
            Assert.NotNull(o.Proveedor);
            Assert.NotNull(o.Regimen);
        });
    }

    [Fact]
    public async Task El_listado_con_cliente_trae_solo_las_de_ese_cliente()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        var operaciones = await servicio.ListarOperacionesAsync(_clienteA);

        Assert.Equal(new[] { _operacionA1009, _operacionA0509 }, operaciones.Select(o => o.Id));
    }

    [Fact]
    public async Task Una_operacion_viene_con_cliente_proveedor_y_regimen()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        var operacion = await servicio.ObtenerOperacionAsync(_operacionB1009);

        Assert.NotNull(operacion);
        Assert.Equal("Agroinsumos del Norte S.R.L.", operacion!.Cliente!.RazonSocial);
        Assert.Equal("Transportes Altiplano S.A.", operacion.Proveedor!.RazonSocial);
        Assert.Equal(78, operacion.Regimen!.Codigo);
    }

    [Fact]
    public async Task Una_operacion_que_no_existe_da_null()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        Assert.Null(await servicio.ObtenerOperacionAsync(_operacionB1009 + 1000));
    }

    [Fact]
    public async Task Cliente_y_proveedor_se_obtienen_por_Id_y_dan_null_si_no_existen()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        Assert.Equal("Metalúrgica del Valle S.A.", (await servicio.ObtenerClienteAsync(_clienteA))!.RazonSocial);
        Assert.Null(await servicio.ObtenerClienteAsync(_clienteB + 1000));

        // ObtenerProveedorAsync no mira el cliente: también trae uno de baja.
        Assert.Equal("Servicios del Sur S.H.", (await servicio.ObtenerProveedorAsync(_proveedorAInactivo))!.RazonSocial);
        Assert.Null(await servicio.ObtenerProveedorAsync(_proveedorB + 1000));
    }

    [Fact]
    public async Task El_proveedor_de_otro_cliente_da_null()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        Assert.NotNull(await servicio.ProveedorDelClienteAsync(_proveedorA, _clienteA));
        Assert.Null(await servicio.ProveedorDelClienteAsync(_proveedorB, _clienteA));
    }

    [Fact]
    public async Task El_desplegable_ofrece_los_proveedores_activos_del_cliente_por_razon_social()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        var proveedores = await servicio.ProveedoresActivosAsync(_clienteA);

        // Sin el de baja (Servicios del Sur) ni el de otro cliente (Transportes).
        Assert.Equal(new[] { _proveedorA2, _proveedorA }, proveedores.Select(p => p.Id));
    }

    [Fact]
    public async Task Los_regimenes_son_todas_las_versiones()
    {
        var (servicio, db) = Nuevo();
        using var _ = db;

        var regimenes = await servicio.RegimenesAsync();

        Assert.Equal(78, Assert.Single(regimenes).Codigo);
    }
}
