using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Tests;

/// <summary>
/// La base de datos de un test. Cada test que la usa crea una nueva, vacía, y la
/// descarta al terminar.
///
/// Hay dos motores posibles, y el mismo test corre en cualquiera de los dos:
///
/// - Por defecto, SQLite en memoria. Es rápido y anda en cualquier máquina, pero
///   NO detecta los problemas propios de SQL Server: SQLite guarda los decimales
///   como texto con todos sus decimales, y nunca recorta nada.
/// - SQL Server, si existe la variable de entorno SIGERE_PRUEBAS_SQLSERVER con una
///   cadena de conexión al servidor (sin base: la base la crea cada test). Por
///   ejemplo, en PowerShell:
///     $env:SIGERE_PRUEBAS_SQLSERVER = "Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True"
///   Cada test crea una base con nombre único, le aplica LA MIGRACIÓN REAL
///   (Migrate, no EnsureCreated, que arma el esquema desde el modelo y no probaría
///   el archivo de migración) y la borra al terminar.
///
/// Regla de trabajo: antes de commitear algo que toque la persistencia, correr la
/// suite con la variable puesta.
/// </summary>
public sealed class BaseDePrueba : IDisposable
{
    public const string VariableSqlServer = "SIGERE_PRUEBAS_SQLSERVER";

    /// <summary>La cadena del servidor de pruebas, o null si se usa SQLite.</summary>
    public static string? ServidorDePruebas =>
        Environment.GetEnvironmentVariable(VariableSqlServer) is { Length: > 0 } valor ? valor : null;

    public static bool UsaSqlServer => ServidorDePruebas is not null;

    private readonly SqliteConnection? _conexionSqlite;

    public DbContextOptions<RetencionesRG830DbContext> Opciones { get; }

    public BaseDePrueba()
    {
        if (ServidorDePruebas is string servidor)
        {
            var cadena = new SqlConnectionStringBuilder(servidor)
            {
                InitialCatalog = $"SIGERE_Pruebas_{Guid.NewGuid():N}"
            }.ConnectionString;

            Opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>()
                .UseSqlServer(cadena)
                .Options;

            using var db = NuevoContexto();
            db.Database.Migrate();
        }
        else
        {
            // La base en memoria vive mientras la conexión esté abierta: por eso la
            // conexión se guarda y se cierra recién al terminar el test.
            _conexionSqlite = new SqliteConnection("DataSource=:memory:");
            _conexionSqlite.Open();

            Opciones = new DbContextOptionsBuilder<RetencionesRG830DbContext>()
                .UseSqlite(_conexionSqlite)
                .Options;

            using var db = NuevoContexto();
            db.Database.EnsureCreated();
        }
    }

    /// <summary>
    /// Un contexto nuevo, sin nada en memoria. Para mirar lo que de verdad quedó
    /// guardado hay que leer con un contexto nuevo: uno que ya tiene una entidad
    /// cargada la devuelve tal como la tiene en memoria, sin mirar la base.
    /// </summary>
    public RetencionesRG830DbContext NuevoContexto() => new(Opciones);

    public void Dispose()
    {
        if (_conexionSqlite is not null)
        {
            _conexionSqlite.Dispose();
            return;
        }

        using var db = NuevoContexto();
        db.Database.EnsureDeleted();
    }
}

/// <summary>
/// Un test que sólo tiene sentido contra SQL Server (por ejemplo, el que mira el
/// catálogo del servidor). Sin la variable SIGERE_PRUEBAS_SQLSERVER figura como
/// "omitido", con el motivo, y no como aprobado: así no se confunde con algo que
/// se probó.
/// </summary>
public sealed class HechoEnSqlServerAttribute : FactAttribute
{
    public HechoEnSqlServerAttribute()
    {
        if (!BaseDePrueba.UsaSqlServer)
            Skip = $"Sólo contra SQL Server: definir {BaseDePrueba.VariableSqlServer} para correrlo.";
    }
}
