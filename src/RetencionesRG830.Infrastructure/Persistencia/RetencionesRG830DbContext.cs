using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Infrastructure.Persistencia;

/// <summary>
/// Esta clase es el "traductor" entre nuestras clases de C# (Cliente,
/// Proveedor, etc.) y la base de datos real. Entity Framework Core (EF Core)
/// es la herramienta de .NET que, a partir de esta clase, sabe qué tablas
/// tiene que crear y cómo guardar y leer objetos como filas.
///
/// Cada propiedad "DbSet&lt;X&gt;" de acá abajo representa una tabla: por
/// ejemplo, DbSet&lt;Cliente&gt; Clientes es la tabla "Clientes". El método
/// OnModelCreating es donde le damos detalles finos que EF Core no puede
/// adivinar solo: qué campos son obligatorios, qué relaciones hay entre
/// tablas, y qué pasa si se intenta borrar una fila que tiene datos
/// relacionados.
/// </summary>
public class RetencionesRG830DbContext : DbContext
{
    public RetencionesRG830DbContext(DbContextOptions<RetencionesRG830DbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Regimen> Regimenes => Set<Regimen>();
    public DbSet<TramoEscala> TramosEscala => Set<TramoEscala>();
    public DbSet<Operacion> Operaciones => Set<Operacion>();

    // ---------- Precisión de los decimales ----------
    // SQL Server guarda un decimal con una cantidad fija de decimales (la "escala"),
    // y si el valor trae más, los recorta SIN AVISAR. SQLite, en cambio, los guardaba
    // como texto con todos sus decimales: por eso este riesgo apareció recién al pasar
    // a SQL Server. Cada columna tiene que poder guardar exactamente el valor que
    // produce el sistema:
    //
    // - Pesos que escribe el operador o fija la norma: dos decimales, que es lo que
    //   garantiza LectorImporte. 18 dígitos dejan 16 enteros, de sobra.
    // - Alícuotas y porcentajes de escala, como fracción (0,02 = 2 %): seis
    //   decimales. Hoy tienen dos; la norma usa a lo sumo dos decimales en porcentaje
    //   (0,25 % = 0,0025), y se deja margen porque un recorte acá no se vería.
    // - El monto retenido sale del motor sin redondear: alícuota por importe. Hoy da
    //   cuatro decimales ($ 3.800,9918) y el pago siguiente del mes lo descuenta tal
    //   cual (el golden test de Gómez lo exige).
    //
    // REGLA: EscalaRetencion >= EscalaAlicuota + EscalaPesos. Si se sube la escala de
    // las alícuotas, hay que subir la del monto retenido. Lo controla un test.
    public const int PrecisionPesos = 18, EscalaPesos = 2;
    public const int PrecisionAlicuota = 9, EscalaAlicuota = 6;
    public const int PrecisionRetencion = 20, EscalaRetencion = 8;

    /// <summary>
    /// Un decimal sin precisión declarada es un error, no una advertencia: SQL Server
    /// le pondría decimal(18,2) y recortaría en silencio lo que no entre. Así, si
    /// mañana se agrega una propiedad decimal y se olvida declararla, la aplicación
    /// no arranca y los tests fallan, en lugar de perder decimales sin que nadie lo
    /// note.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(advertencias =>
            advertencias.Throw(SqlServerEventId.DecimalTypeDefaultWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Intercalación de la base: cómo se comparan y ordenan los textos. Se fija
        // acá para que sea la misma en todas las máquinas y en producción, sin
        // depender de con qué idioma se instaló cada SQL Server (LocalDB, por
        // ejemplo, viene con otra). Español, sin distinguir mayúsculas (CI) y
        // distinguiendo acentos (AS). El login no depende de esto: el correo se
        // normaliza a minúsculas en el código.
        modelBuilder.UseCollation("Modern_Spanish_CI_AS");

        // ---------- Cliente ----------
        modelBuilder.Entity<Cliente>(cliente =>
        {
            cliente.Property(c => c.Cuit).IsRequired().HasMaxLength(13);
            cliente.Property(c => c.RazonSocial).IsRequired().HasMaxLength(200);
            // Dos Clientes no pueden tener el mismo CUIT: evita cargar la
            // misma empresa dos veces por error.
            cliente.HasIndex(c => c.Cuit).IsUnique();
        });

        // ---------- Proveedor ----------
        modelBuilder.Entity<Proveedor>(proveedor =>
        {
            proveedor.Property(p => p.Cuit).IsRequired().HasMaxLength(13);
            proveedor.Property(p => p.RazonSocial).IsRequired().HasMaxLength(200);

            // Los proveedores que ya existían antes de agregar esta columna
            // quedan marcados como activos por defecto.
            proveedor.Property(p => p.Activo).HasDefaultValue(true);

            // Un mismo CUIT no puede estar dos veces como proveedor del
            // MISMO cliente, pero sí puede ser proveedor de dos clientes
            // distintos del estudio (por eso el índice único combina
            // ClienteId + Cuit, no sólo Cuit).
            proveedor.HasIndex(p => new { p.ClienteId, p.Cuit }).IsUnique();

            proveedor.HasOne(p => p.Cliente)
                .WithMany(c => c.Proveedores)
                .HasForeignKey(p => p.ClienteId)
                // Restrict = no se puede borrar un Cliente si todavía tiene
                // Proveedores cargados. Preferimos un error claro antes que
                // borrar en cascada y perder datos por accidente.
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Usuario ----------
        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.Email).IsRequired().HasMaxLength(200);
            usuario.HasIndex(u => u.Email).IsUnique();

            usuario.HasOne(u => u.Cliente)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(u => u.ClienteId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false); // ClienteId es null para usuarios del Estudio.
        });

        // ---------- Regimen ----------
        modelBuilder.Entity<Regimen>(regimen =>
        {
            regimen.Property(r => r.Descripcion).IsRequired().HasMaxLength(300);

            regimen.Property(r => r.TasaInscripto).HasPrecision(PrecisionAlicuota, EscalaAlicuota);
            regimen.Property(r => r.TasaNoInscriptoHumana).HasPrecision(PrecisionAlicuota, EscalaAlicuota);
            regimen.Property(r => r.TasaNoInscriptoResto).HasPrecision(PrecisionAlicuota, EscalaAlicuota);
            regimen.Property(r => r.MontoNoSujetoARetencion).HasPrecision(PrecisionPesos, EscalaPesos);
            regimen.Property(r => r.MinimoRetencion).HasPrecision(PrecisionPesos, EscalaPesos);
            regimen.Property(r => r.MinimoRetencionNoInscripto).HasPrecision(PrecisionPesos, EscalaPesos);

            // Los regímenes ya cargados quedan con el mínimo general.
            regimen.Property(r => r.MinimoRetencionNoInscripto).HasDefaultValue(240m);

            // Puede haber varias filas con el mismo Codigo a lo largo del
            // tiempo (una por cada vez que cambió la norma), pero no dos
            // con el mismo Codigo Y la misma fecha de vigencia.
            regimen.HasIndex(r => new { r.Codigo, r.VigenciaDesde }).IsUnique();
        });

        // ---------- TramoEscala ----------
        modelBuilder.Entity<TramoEscala>(tramo =>
        {
            tramo.Property(t => t.Desde).HasPrecision(PrecisionPesos, EscalaPesos);
            tramo.Property(t => t.Hasta).HasPrecision(PrecisionPesos, EscalaPesos);
            tramo.Property(t => t.MontoFijo).HasPrecision(PrecisionPesos, EscalaPesos);
            // En la escala, monto fijo + porcentaje × (neto − desde): mismo criterio
            // que las alícuotas, y la misma regla para el monto retenido.
            tramo.Property(t => t.Porcentaje).HasPrecision(PrecisionAlicuota, EscalaAlicuota);

            tramo.HasOne(t => t.Regimen)
                .WithMany(r => r.Tramos)
                .HasForeignKey(t => t.RegimenId)
                .OnDelete(DeleteBehavior.Cascade); // si se borra el Régimen (poco común), sus tramos no quedan huérfanos.
        });

        // ---------- Operacion ----------
        modelBuilder.Entity<Operacion>(operacion =>
        {
            operacion.Property(o => o.ImporteComprobante).HasPrecision(PrecisionPesos, EscalaPesos);
            operacion.Property(o => o.ImporteGravado).HasPrecision(PrecisionPesos, EscalaPesos);
            // Suma de importes gravados: dos decimales, como ellos.
            operacion.Property(o => o.BaseCalculoAcumulada).HasPrecision(PrecisionPesos, EscalaPesos);
            // Sin redondear: el pago siguiente del mes lo descuenta tal cual (regla 3).
            operacion.Property(o => o.MontoRetenido).HasPrecision(PrecisionRetencion, EscalaRetencion);

            // Nunca se puede borrar un Cliente, Proveedor o Régimen que ya
            // tiene Operaciones cargadas: son la prueba de una retención
            // real ya practicada, y hay que conservarlas siempre.
            operacion.HasOne(o => o.Cliente)
                .WithMany(c => c.Operaciones)
                .HasForeignKey(o => o.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            operacion.HasOne(o => o.Proveedor)
                .WithMany(p => p.Operaciones)
                .HasForeignKey(o => o.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            operacion.HasOne(o => o.Regimen)
                .WithMany(r => r.Operaciones)
                .HasForeignKey(o => o.RegimenId)
                .OnDelete(DeleteBehavior.Restrict);

            // El número de certificado tiene que ser único DENTRO de cada
            // Cliente (dos clientes distintos sí pueden tener, cada uno,
            // un certificado número 1).
            //
            // El filtro deja afuera del índice las operaciones sin certificado
            // (regla 10, NumeroCertificado null). En SQL Server un índice único
            // común admite un solo NULL por cliente: sin el filtro, el sistema anda
            // bien y falla recién con la SEGUNDA operación que no llega al mínimo.
            // EF Core lo agrega solo por convención, pero se escribe explícito para
            // no depender de eso. SQLite (los tests) también lo entiende.
            operacion.HasIndex(o => new { o.ClienteId, o.NumeroCertificado })
                .IsUnique()
                .HasFilter("[NumeroCertificado] IS NOT NULL");
        });
    }
}
