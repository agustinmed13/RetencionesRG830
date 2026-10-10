using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Prueba que editar un cliente no le cambia el estado (activo o de baja).
///
/// Reproduce el bug que había: el formulario de edición no manda Activo, el
/// objeto armado con el POST lo trae en true (su valor por defecto) y
/// _db.Clientes.Update() lo guardaba, así que editar un cliente de baja lo
/// reactivaba. Por eso el cliente de estos tests está de baja.
///
/// Base SQLite en memoria, igual que ServicioProveedoresTests.
/// </summary>
public class ServicioClientesTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly DbContextOptions<RetencionesRG830DbContext> _opciones;

    private int _clienteId;

    public ServicioClientesTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        _opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>()
            .UseSqlite(_conexion)
            .Options;

        using var db = new RetencionesRG830DbContext(_opciones);
        db.Database.EnsureCreated();

        var cliente = new Cliente
        {
            Cuit = "30-61234567-4",
            RazonSocial = "Metalúrgica del Valle S.A.",
            Domicilio = "Av. San Martín 1250",
            Localidad = "Salta - SALTA",
            NombreFirmante = "Roberto Daniel Suárez",
            CargoFirmante = "Presidente",
            Activo = false
        };
        db.Clientes.Add(cliente);
        db.SaveChanges();
        _clienteId = cliente.Id;
    }

    public void Dispose()
    {
        _conexion.Dispose();
    }

    /// <summary>
    /// Lo que llega del formulario de edición: un objeto nuevo armado con los
    /// campos del POST, no el que está en la base.
    /// </summary>
    private Cliente EdicionRecibida() => new()
    {
        Id = _clienteId,
        Cuit = "30-61234567-4",
        RazonSocial = "Metalúrgica del Valle S.A.",
        Domicilio = "Av. San Martín 1300",
        Localidad = "Salta - SALTA",
        NombreFirmante = "Roberto Daniel Suárez",
        CargoFirmante = "Vicepresidente"
        // Activo no viene en el formulario: queda en true, su valor por defecto.
    };

    /// <summary>
    /// Lee el cliente con un contexto nuevo, para mirar lo que quedó guardado en
    /// la base y no el objeto que EF ya tiene en memoria.
    /// </summary>
    private Cliente LeerDeLaBase()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return db.Clientes.Single(c => c.Id == _clienteId);
    }

    [Fact]
    public async Task Editar_un_cliente_de_baja_no_lo_reactiva()
    {
        using (var db = new RetencionesRG830DbContext(_opciones))
        {
            await new ServicioClientes(db).ActualizarAsync(EdicionRecibida());
        }

        var guardado = LeerDeLaBase();

        Assert.False(guardado.Activo);
        // Y la edición sí se aplicó: el test no pasa por no haber guardado nada.
        Assert.Equal("Av. San Martín 1300", guardado.Domicilio);
        Assert.Equal("Vicepresidente", guardado.CargoFirmante);
    }

    [Fact]
    public async Task Editar_un_cliente_inexistente_devuelve_null()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        var datos = EdicionRecibida();
        datos.Id = 9999;

        Assert.Null(await new ServicioClientes(db).ActualizarAsync(datos));
    }
}
