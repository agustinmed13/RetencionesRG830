using System.ComponentModel.DataAnnotations;

namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Un Cliente es un agente de retención: una empresa cliente del estudio
/// contable que está obligada a retener Ganancias a sus proveedores bajo la
/// RG 830 (por ejemplo, GIMI S.A.). Es el equivalente a "una copia de la
/// planilla Excel personalizada para un cliente puntual", pero acá es una
/// fila más en la tabla Cliente en vez de un archivo separado.
///
/// Los campos NombreFirmante y CargoFirmante son los datos de la persona que
/// firma el certificado de retención (P22 y P23 en la hoja Cálculo del Excel).
/// </summary>
public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El CUIT es obligatorio.")]
    [StringLength(13, ErrorMessage = "El CUIT no puede tener más de 13 caracteres.")]
    [Display(Name = "CUIT")]
    public string Cuit { get; set; } = string.Empty;

    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200)]
    [Display(Name = "Razón social")]
    public string RazonSocial { get; set; } = string.Empty;

    [StringLength(200)]
    public string Domicilio { get; set; } = string.Empty;

    [StringLength(100)]
    public string Localidad { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Nombre del firmante")]
    public string NombreFirmante { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Cargo del firmante")]
    public string CargoFirmante { get; set; } = string.Empty;

    /// <summary>
    /// Permite dar de baja un cliente sin borrar su historial de operaciones
    /// (nunca conviene borrar filas que ya tienen certificados emitidos).
    /// </summary>
    public bool Activo { get; set; } = true;

    // --- Relaciones (listas de "los muchos" del lado "uno" del diagrama) ---
    public ICollection<Proveedor> Proveedores { get; set; } = new List<Proveedor>();
    public ICollection<Operacion> Operaciones { get; set; } = new List<Operacion>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}