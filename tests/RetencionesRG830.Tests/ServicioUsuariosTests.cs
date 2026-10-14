using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// El correo se guarda y se busca en minúsculas y sin espacios alrededor, así que
/// el inicio de sesión no distingue mayúsculas EN NINGÚN MOTOR.
///
/// Antes dependía de la base: SQLite compara textos distinguiendo mayúsculas, y SQL
/// Server con la intercalación Modern_Spanish_CI_AS no las distingue. Al pasar a SQL
/// Server, "Estudio@Ejemplo.com" empezaba a entrar sin que nadie lo decidiera, y si
/// mañana se despliega en un servidor con otra intercalación, la autenticación
/// cambiaría sola. Estos tests corren por defecto contra SQLite, que distingue
/// mayúsculas: si pasan ahí, no es gracias a la intercalación.
/// </summary>
public class ServicioUsuariosTests : IDisposable
{
    private readonly BaseDePrueba _base = new();

    public ServicioUsuariosTests()
    {
        using var db = _base.NuevoContexto();
        db.Usuarios.Add(new Usuario { Email = "estudio@ejemplo.com", PasswordHash = "x", Rol = RolUsuario.Estudio });
        db.SaveChanges();
    }

    public void Dispose() => _base.Dispose();

    [Theory]
    [InlineData("estudio@ejemplo.com")]
    [InlineData("Estudio@Ejemplo.com")]
    [InlineData("ESTUDIO@EJEMPLO.COM")]
    [InlineData("  estudio@ejemplo.com  ")]
    public async Task Encuentra_al_usuario_sin_importar_mayusculas_ni_espacios(string escrito)
    {
        using var db = _base.NuevoContexto();

        var usuario = await new ServicioUsuarios(db).BuscarPorEmailAsync(escrito);

        Assert.NotNull(usuario);
        Assert.Equal("estudio@ejemplo.com", usuario!.Email);
    }

    [Fact]
    public async Task Un_correo_que_no_existe_no_encuentra_a_nadie()
    {
        using var db = _base.NuevoContexto();

        Assert.Null(await new ServicioUsuarios(db).BuscarPorEmailAsync("otro@ejemplo.com"));
    }

    /// <summary>
    /// La normalización está en la entidad, así que vale para cualquier forma de
    /// crear un usuario (hoy el seed; mañana una pantalla de alta).
    /// </summary>
    [Fact]
    public async Task El_correo_se_guarda_en_minusculas_y_sin_espacios()
    {
        using (var db = _base.NuevoContexto())
        {
            db.Usuarios.Add(new Usuario { Email = "  Operador@Cliente.COM ", PasswordHash = "x", Rol = RolUsuario.Cliente });
            db.SaveChanges();
        }

        using var lectura = _base.NuevoContexto();
        Assert.True(await lectura.Usuarios.AnyAsync(u => u.Email == "operador@cliente.com"));
    }

    /// <summary>
    /// Como se guarda normalizado, el índice único del correo rechaza el mismo correo
    /// escrito con otras mayúsculas, también en un motor que las distingue.
    /// </summary>
    [Fact]
    public void No_se_puede_crear_el_mismo_correo_con_otras_mayusculas()
    {
        using var db = _base.NuevoContexto();
        db.Usuarios.Add(new Usuario { Email = "Estudio@Ejemplo.com", PasswordHash = "x", Rol = RolUsuario.Estudio });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
