using System.ComponentModel.DataAnnotations;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Un Proveedor es un tercero al que un Cliente (agente de retención) le
/// puede llegar a retener Ganancias. Equivale a una fila de la hoja
/// "Proveedores" del Excel, pero acá siempre pertenece a un Cliente
/// puntual (ClienteId): dos clientes distintos del estudio pueden tener,
/// cada uno, un proveedor con el mismo CUIT sin pisarse.
///
/// InscriptoEnGanancias y TipoPersona son los datos que en el Excel había
/// que tipear a mano en cada cálculo (celdas C15 y C16 de la hoja Cálculo).
/// Acá se guardan una sola vez en el proveedor, porque salvo casos raros no
/// cambian de una operación a la siguiente.
/// </summary>
public class Proveedor
{
    public int Id { get; set; }

    [Display(Name = "Cliente")]
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

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

    [Display(Name = "Inscripto en Ganancias")]
    public bool InscriptoEnGanancias { get; set; } = true;

    [Display(Name = "Tipo de persona")]
    public TipoPersona TipoPersona { get; set; } = TipoPersona.HumanaYSucesionIndivisa;

    /// <summary>
    /// Permite dar de baja un proveedor sin borrar su historial de
    /// operaciones (igual que Activo en Cliente).
    /// </summary>
    public bool Activo { get; set; } = true;

    public ICollection<Operacion> Operaciones { get; set; } = new List<Operacion>();
}