using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
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

    // --- Alta ---------------------------------------------------------------
    // Antes el controlador guardaba con _db.Clientes.Add() el objeto armado con el
    // POST, y se colaba todo lo que viniera, aunque el formulario no lo ofrezca.

    private const string CuitAlta = "30-71845236-4";

    /// <summary>Lo que el formulario de alta ofrece de verdad.</summary>
    private static Cliente AltaRecibida() => new()
    {
        Cuit = CuitAlta,
        RazonSocial = "Agroinsumos del Norte S.R.L.",
        Domicilio = "Av. Fascio 820",
        Localidad = "San Salvador de Jujuy - JUJUY",
        NombreFirmante = "Mariela Andrea Correa",
        CargoFirmante = "Socia gerente"
    };

    private async Task<Cliente> Crear(Cliente datos)
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return await new ServicioClientes(db).CrearAsync(datos);
    }

    /// <summary>
    /// Lee el cliente dado de alta con un contexto nuevo, por el CUIT: si el
    /// código viejo hubiera forzado otro Id, buscarlo por Id escondería el problema.
    /// </summary>
    private Cliente LeerAlta()
    {
        using var db = new RetencionesRG830DbContext(_opciones);
        return db.Clientes.Single(c => c.Cuit == CuitAlta);
    }

    [Fact]
    public async Task Alta_con_Usuarios_no_crea_ningun_usuario()
    {
        var datos = AltaRecibida();
        // Como podría venir en un POST armado a mano con Usuarios[0].Email=...,
        // Usuarios[0].PasswordHash=..., Usuarios[0].Rol=Estudio.
        datos.Usuarios.Add(new Usuario
        {
            Email = "colado@ejemplo.com",
            PasswordHash = "hash-inventado",
            Rol = RolUsuario.Estudio
        });

        await Crear(datos);

        using var db = new RetencionesRG830DbContext(_opciones);
        Assert.Equal(0, db.Usuarios.Count());
        // El cliente sí se dio de alta, con los datos del formulario.
        var creado = LeerAlta();
        Assert.Equal("Agroinsumos del Norte S.R.L.", creado.RazonSocial);
        Assert.Equal("Mariela Andrea Correa", creado.NombreFirmante);
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

    // --- Textos en null -----------------------------------------------------
    // ASP.NET convierte un campo de texto enviado vacío en null, aunque la
    // propiedad sea string y no string?. Si ese null llegaba a la base, la columna
    // NOT NULL hacía fallar el guardado con DbUpdateException, y el controlador,
    // que interpreta esa excepción como índice único violado, mostraba "Ya existe
    // un cliente con ese CUIT": un error falso. Hoy [Required] impide que un campo
    // vacío llegue hasta acá, pero el servicio no depende de eso.

    [Fact]
    public async Task Alta_con_textos_en_null_guarda_vacio_en_lugar_de_fallar()
    {
        var datos = AltaRecibida();
        datos.Domicilio = null!;
        datos.Localidad = null!;
        datos.NombreFirmante = null!;
        datos.CargoFirmante = null!;

        await Crear(datos);

        var creado = LeerAlta();
        Assert.Equal(string.Empty, creado.Domicilio);
        Assert.Equal(string.Empty, creado.Localidad);
        Assert.Equal(string.Empty, creado.NombreFirmante);
        Assert.Equal(string.Empty, creado.CargoFirmante);
    }

    [Fact]
    public async Task Editar_con_textos_en_null_guarda_vacio_en_lugar_de_fallar()
    {
        var datos = EdicionRecibida();
        datos.Domicilio = null!;
        datos.Localidad = null!;
        datos.NombreFirmante = null!;
        datos.CargoFirmante = null!;

        using (var db = new RetencionesRG830DbContext(_opciones))
        {
            await new ServicioClientes(db).ActualizarAsync(datos);
        }

        var guardado = LeerDeLaBase();
        Assert.Equal(string.Empty, guardado.Domicilio);
        Assert.Equal(string.Empty, guardado.Localidad);
        Assert.Equal(string.Empty, guardado.NombreFirmante);
        Assert.Equal(string.Empty, guardado.CargoFirmante);
    }
}
