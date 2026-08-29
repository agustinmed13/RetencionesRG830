using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
               // ---------- Regimen ----------
        modelBuilder.Entity<Regimen>(regimen =>
        {
            regimen.Property(r => r.Descripcion).IsRequired().HasMaxLength(300);

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
            tramo.HasOne(t => t.Regimen)
                .WithMany(r => r.Tramos)
                .HasForeignKey(t => t.RegimenId)
                .OnDelete(DeleteBehavior.Cascade); // si se borra el Régimen (poco común), sus tramos no quedan huérfanos.
        });

        // ---------- Operacion ----------
        modelBuilder.Entity<Operacion>(operacion =>
        {
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
            operacion.HasIndex(o => new { o.ClienteId, o.NumeroCertificado }).IsUnique();
        });
    }
}
