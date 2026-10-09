using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Prueba el acumulado mensual automático, que es el corazón del hito 3.
///
/// Usa una base de datos SQLite que vive sólo en memoria: se crea vacía al
/// empezar cada test y desaparece al terminar. Así los tests son rápidos,
/// no dependen de la base real, y no se pisan entre sí.
/// </summary>
public class ServicioRegistroOperacionesTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly RetencionesRG830DbContext _db;
    private readonly ServicioRegistroOperaciones _servicio;

    private int _clienteId;
    private int _proveedorId;
    private int _regimenId;

    public ServicioRegistroOperacionesTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        var opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>()
            .UseSqlite(_conexion)
            .Options;

        _db = new RetencionesRG830DbContext(opciones);
        _db.Database.EnsureCreated();

        Sembrar();

        _servicio = new ServicioRegistroOperaciones(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private void Sembrar()
    {
        var cliente = new Cliente
        {
            Cuit = "30-61234567-4",
            RazonSocial = "Metalúrgica del Valle S.A."
        };

        var regimen = new Regimen
        {
            Codigo = 78,
            Descripcion = "Enajenación de bienes muebles y bienes de cambio.",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.10m,
            TasaNoInscriptoResto = 0.10m,
            MontoNoSujetoARetencion = 224000m,
            MinimoRetencion = 240m,
            VigenciaDesde = new DateOnly(2026, 1, 1)
        };

        _db.Clientes.Add(cliente);
        _db.Regimenes.Add(regimen);
        _db.SaveChanges();

        var proveedor = new Proveedor
        {
            ClienteId = cliente.Id,
            Cuit = "20-31456789-8",
            RazonSocial = "Gómez, Carlos Alberto",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };

        _db.Proveedores.Add(proveedor);
        _db.SaveChanges();

        _clienteId = cliente.Id;
        _proveedorId = proveedor.Id;
        _regimenId = regimen.Id;
    }

    private Operacion NuevaOperacion(decimal gravado, DateOnly fechaRetencion, long numero)
        => new Operacion
        {
            ClienteId = _clienteId,
            ProveedorId = _proveedorId,
            RegimenId = _regimenId,
            TipoComprobante = 1,
            PuntoVenta = 1,
            NumeroComprobante = numero,
            FechaComprobante = fechaRetencion,
            FechaRetencion = fechaRetencion,
            ImporteComprobante = gravado,
            ImporteGravado = gravado,
            CreadoPorUsuarioId = 1
        };

    /// <summary>
    /// El caso real de Gómez: dos comprobantes en el mismo mes.
    /// Nadie le informa al sistema cuánto llevaba acumulado.
    /// </summary>
    [Fact]
    public async Task La_segunda_operacion_del_mes_acumula_y_descuenta_lo_ya_retenido()
    {
        var primera = await _servicio.RegistrarAsync(
            NuevaOperacion(414049.59m, new DateOnly(2026, 8, 4), 2514));

        Assert.Equal(3800.9918m, primera.MontoRetenido, 4);

        var segunda = await _servicio.RegistrarAsync(
            NuevaOperacion(87190.09m, new DateOnly(2026, 8, 12), 2515));

        Assert.Equal(1743.8018m, segunda.MontoRetenido, 4);
    }

    /// <summary>
    /// El acumulado se reinicia cada mes: una operación de septiembre no
    /// tiene que arrastrar lo de agosto, y el piso vuelve a aplicarse entero.
    /// </summary>
    [Fact]
    public async Task El_acumulado_no_cruza_de_un_mes_al_siguiente()
    {
        await _servicio.RegistrarAsync(
            NuevaOperacion(414049.59m, new DateOnly(2026, 8, 4), 2514));

        var deSeptiembre = await _servicio.RegistrarAsync(
            NuevaOperacion(414049.59m, new DateOnly(2026, 9, 3), 2600));

        Assert.Equal(3800.9918m, deSeptiembre.MontoRetenido, 4);
    }

    /// <summary>
    /// La numeración de certificados la asigna el sistema, correlativa y
    /// sin saltos. Es lo que reemplaza al conteo manual del estudio.
    /// </summary>
    [Fact]
    public async Task Los_certificados_se_numeran_correlativamente()
    {
        var primera = await _servicio.RegistrarAsync(
            NuevaOperacion(100000m, new DateOnly(2026, 8, 4), 1));
        var segunda = await _servicio.RegistrarAsync(
            NuevaOperacion(100000m, new DateOnly(2026, 8, 5), 2));
        var tercera = await _servicio.RegistrarAsync(
            NuevaOperacion(100000m, new DateOnly(2026, 8, 6), 3));

        Assert.Equal(1, primera.NumeroCertificado);
        Assert.Equal(2, segunda.NumeroCertificado);
        Assert.Equal(3, tercera.NumeroCertificado);
    }

    /// <summary>
    /// Un proveedor pertenece a un único cliente. Si una operación de un cliente
    /// llegara con el proveedor de otro, el acumulado del mes se mezclaría entre
    /// dos empresas: no se puede simular ni registrar, y no queda nada guardado.
    /// </summary>
    [Fact]
    public async Task No_se_puede_registrar_con_un_proveedor_de_otro_cliente()
    {
        var otroCliente = new Cliente { Cuit = "30-71845236-4", RazonSocial = "Agroinsumos del Norte S.R.L." };
        _db.Clientes.Add(otroCliente);
        _db.SaveChanges();
        var proveedorAjeno = new Proveedor
        {
            ClienteId = otroCliente.Id,
            Cuit = "20-28765431-7",
            RazonSocial = "Ruiz, Héctor Damián",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        _db.Proveedores.Add(proveedorAjeno);
        _db.SaveChanges();

        var operacion = NuevaOperacion(500000m, new DateOnly(2026, 9, 10), 1);
        operacion.ProveedorId = proveedorAjeno.Id; // cliente Metalúrgica, proveedor de Agroinsumos

        Assert.False(await _servicio.ProveedorPerteneceAlClienteAsync(proveedorAjeno.Id, _clienteId));
        Assert.True(await _servicio.ProveedorPerteneceAlClienteAsync(_proveedorId, _clienteId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.SimularAsync(operacion));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.RegistrarAsync(operacion));
        Assert.Equal(0, await _db.Operaciones.CountAsync());
    }

    /// <summary>
    /// El mismo comprobante del mismo proveedor puede tener dos retenciones
    /// legítimas (una factura pagada en dos cuotas): el sistema lo detecta para
    /// avisar, pero no impide registrar.
    /// </summary>
    [Fact]
    public async Task El_comprobante_repetido_se_detecta_pero_no_se_bloquea()
    {
        var primeraCuota = await _servicio.RegistrarAsync(NuevaOperacion(300000m, new DateOnly(2026, 9, 10), 777));

        var segundaCuota = NuevaOperacion(300000m, new DateOnly(2026, 9, 25), 777);
        var repetidas = await _servicio.OperacionesConMismoComprobanteAsync(segundaCuota);

        Assert.Single(repetidas);
        Assert.Equal(primeraCuota.NumeroCertificado, repetidas[0].NumeroCertificado);

        var registrada = await _servicio.RegistrarAsync(segundaCuota);
        Assert.Equal(2, registrada.NumeroCertificado);

        // Otro número de comprobante no se considera repetido.
        Assert.Empty(await _servicio.OperacionesConMismoComprobanteAsync(
            NuevaOperacion(300000m, new DateOnly(2026, 9, 26), 778)));
    }

    /// <summary>
    /// Volver a cargar un comprobante después de anular su operación es la forma
    /// normal de corregir un error: la anulada no cuenta como repetida.
    /// </summary>
    [Fact]
    public async Task Una_operacion_anulada_no_cuenta_como_comprobante_repetido()
    {
        var conError = await _servicio.RegistrarAsync(NuevaOperacion(300000m, new DateOnly(2026, 9, 10), 777));
        await _servicio.AnularAsync(conError.Id, "Importe mal cargado", usuarioId: 1);

        var corregida = NuevaOperacion(350000m, new DateOnly(2026, 9, 10), 777);

        Assert.Empty(await _servicio.OperacionesConMismoComprobanteAsync(corregida));
    }
}