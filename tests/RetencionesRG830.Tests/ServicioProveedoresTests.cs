using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Prueba que editar un proveedor no lo cambia de cliente (regla 8).
///
/// Reproduce el bug que había: el desplegable de cliente de la edición sólo
/// listaba clientes activos, así que con el cliente del proveedor dado de baja
/// el formulario mandaba otro ClienteId y el proveedor se mudaba solo. Por eso
/// el cliente original de estos tests está de baja.
///
/// Base SQLite en memoria, igual que ServicioRegistroOperacionesTests.
/// </summary>
public class ServicioProveedoresTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly DbContextOptions<RetencionesRG830DbContext> _opciones;

    private int _clienteOriginalId;
    private int _otroClienteId;
    private int _proveedorId;
    private int _regimenId;

    public ServicioProveedoresTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        _opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>()
            .UseSqlite(_conexion)
            .Options;

        using var db = new RetencionesRG830DbContext(_opciones);
        db.Database.EnsureCreated();
        Sembrar(db);
    }

    public void Dispose()
    {
        _conexion.Dispose();
    }

    private void Sembrar(RetencionesRG830DbContext db)
    {
        var clienteOriginal = new Cliente
        {
            Cuit = "30-61234567-4",
            RazonSocial = "Metalúrgica del Valle S.A.",
            Activo = false
        };
        var otroCliente = new Cliente
        {
            Cuit = "30-71845236-4",
            RazonSocial = "Agroinsumos del Norte S.R.L."
        };
        db.Clientes.AddRange(clienteOriginal, otroCliente);
        db.SaveChanges();

        var proveedor = new Proveedor
        {
            ClienteId = clienteOriginal.Id,
            Cuit = "20-12345678-6",
            RazonSocial = "Gómez, Carlos Alberto",
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa,
            Activo = false
        };
        db.Proveedores.Add(proveedor);

        // Sólo para que la operación que intentan colar los tests de alta sea
        // válida: si no, el código viejo fallaría por la clave foránea y no por
        // el agujero, y el test no probaría nada.
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
        db.Regimenes.Add(regimen);
        db.SaveChanges();

        _clienteOriginalId = clienteOriginal.Id;
        _otroClienteId = otroCliente.Id;
        _proveedorId = proveedor.Id;
        _regimenId = regimen.Id;
    }

    /// <summary>
    /// Lo que llega del formulario de edición: un objeto nuevo armado con los
    /// campos del POST, no el que está en la base.
    /// </summary>
    private Proveedor EdicionRecibida(int clienteId) => new()
    {
        Id = _proveedorId,
        ClienteId = clienteId,
        Cuit = "20-12345678-6",
        RazonSocial = "Gómez, Carlos Alberto (editado)",
        InscriptoEnGanancias = false,
        TipoPersona = TipoPersona.Resto
        // Activo no viene en el formulario: queda en true, su valor por defecto.
    };

    /// <summary>
    /// Lee el proveedor con un contexto nuevo. Si se leyera con el mismo del
    /// servicio, EF devolvería el objeto que ya tiene en memoria y el test no
    /// estaría mirando lo que quedó guardado en la base.
    /// </summary>
    private Proveedor LeerDeLaBase()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return db.Proveedores.Single(p => p.Id == _proveedorId);
    }

    [Fact]
    public async Task Editar_con_otro_ClienteId_no_cambia_el_cliente_del_proveedor()
    {
        using (var db = new RetencionesRG830DbContext(_opciones))
        {
            await new ServicioProveedores(db).ActualizarAsync(EdicionRecibida(_otroClienteId));
        }

        var guardado = LeerDeLaBase();

        Assert.Equal(_clienteOriginalId, guardado.ClienteId);
        // Y la edición sí se aplicó: el test no pasa por no haber guardado nada.
        Assert.Equal("Gómez, Carlos Alberto (editado)", guardado.RazonSocial);
        Assert.False(guardado.InscriptoEnGanancias);
        Assert.Equal(TipoPersona.Resto, guardado.TipoPersona);
    }

    [Fact]
    public async Task Editar_un_proveedor_de_baja_no_lo_reactiva()
    {
        using (var db = new RetencionesRG830DbContext(_opciones))
        {
            await new ServicioProveedores(db).ActualizarAsync(EdicionRecibida(_clienteOriginalId));
        }

        Assert.False(LeerDeLaBase().Activo);
    }

    [Fact]
    public async Task Editar_un_proveedor_inexistente_devuelve_null()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        var datos = EdicionRecibida(_clienteOriginalId);
        datos.Id = 9999;

        Assert.Null(await new ServicioProveedores(db).ActualizarAsync(datos));
    }

    // --- Alta ---------------------------------------------------------------
    // Antes el controlador guardaba con _db.Proveedores.Add() el objeto armado con
    // el POST, y se colaba todo lo que viniera, aunque el formulario no lo ofrezca.

    private const string CuitAlta = "27-32567894-7";

    /// <summary>Lo que el formulario de alta ofrece de verdad.</summary>
    private Proveedor AltaRecibida() => new()
    {
        ClienteId = _otroClienteId,
        Cuit = CuitAlta,
        RazonSocial = "Ramírez, Laura Beatriz",
        Domicilio = "Rivadavia 88",
        Localidad = "Salta - SALTA",
        InscriptoEnGanancias = false,
        TipoPersona = TipoPersona.Resto
    };

    private async Task<Proveedor> Crear(Proveedor datos)
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return await new ServicioProveedores(db).CrearAsync(datos);
    }

    /// <summary>
    /// Lee el proveedor dado de alta con un contexto nuevo, por el CUIT: si el
    /// código viejo hubiera forzado otro Id, buscarlo por Id escondería el problema.
    /// </summary>
    private Proveedor LeerAlta()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return db.Proveedores.Single(p => p.Cuit == CuitAlta);
    }

    [Fact]
    public async Task Alta_con_Operaciones_no_crea_ninguna_operacion()
    {
        var datos = AltaRecibida();
        // Una operación completa y válida, con importes y número de certificado
        // inventados, como podría venir en un POST armado a mano con
        // Operaciones[0].MontoRetenido=..., Operaciones[0].NumeroCertificado=...
        datos.Operaciones.Add(new Operacion
        {
            ClienteId = _otroClienteId,
            RegimenId = _regimenId,
            TipoComprobante = 1,
            PuntoVenta = 3,
            NumeroComprobante = 12847,
            FechaComprobante = new DateOnly(2026, 9, 22),
            FechaRetencion = new DateOnly(2026, 9, 22),
            ImporteComprobante = 2000000m,
            ImporteGravado = 2000000m,
            BaseCalculoAcumulada = 2000000m,
            MontoRetenido = 1m,
            NumeroCertificado = 1,
            CreadoPorUsuarioId = 1,
            FechaCarga = new DateTime(2026, 9, 22)
        });

        await Crear(datos);

        using var db = new RetencionesRG830DbContext(_opciones);
        Assert.Equal(0, db.Operaciones.Count());
        // El proveedor sí se dio de alta, con los datos del formulario.
        var creado = LeerAlta();
        Assert.Equal(_otroClienteId, creado.ClienteId);
        Assert.Equal("Ramírez, Laura Beatriz", creado.RazonSocial);
        Assert.False(creado.InscriptoEnGanancias);
        Assert.Equal(TipoPersona.Resto, creado.TipoPersona);
    }

    [Fact]
    public async Task Alta_con_un_Cliente_no_crea_un_cliente_nuevo()
    {
        var datos = AltaRecibida();
        datos.Cliente = new Cliente
        {
            Cuit = "30-70000000-1",
            RazonSocial = "Cliente colado S.A."
        };

        await Crear(datos);

        using var db = new RetencionesRG830DbContext(_opciones);
        Assert.Equal(2, db.Clientes.Count());
        Assert.Equal(_otroClienteId, LeerAlta().ClienteId);
    }

    [Fact]
    public async Task Alta_no_toma_Activo_del_formulario()
    {
        var datos = AltaRecibida();
        datos.Activo = false;

        await Crear(datos);

        Assert.True(LeerAlta().Activo);
    }

    [Fact]
    public async Task Alta_no_toma_Id_del_formulario()
    {
        var datos = AltaRecibida();
        datos.Id = 500;

        await Crear(datos);

        Assert.NotEqual(500, LeerAlta().Id);
    }
}
