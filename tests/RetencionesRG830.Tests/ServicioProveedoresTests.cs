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
        db.SaveChanges();

        _clienteOriginalId = clienteOriginal.Id;
        _otroClienteId = otroCliente.Id;
        _proveedorId = proveedor.Id;
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
}
