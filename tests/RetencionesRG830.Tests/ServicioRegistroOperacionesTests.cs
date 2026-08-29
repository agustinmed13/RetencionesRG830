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
}