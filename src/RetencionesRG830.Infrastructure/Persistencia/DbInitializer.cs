using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Infrastructure.Persistencia;

public static class DbInitializer
{
    public static void Seed(RetencionesRG830DbContext context)
    {
        SeedRegimenes(context);
        SeedUsuariosDePrueba(context);
        SeedProveedoresDePrueba(context);
    }

    private static void SeedRegimenes(RetencionesRG830DbContext context)
    {
        if (context.Regimenes.Any())
        {
            return; // ya están cargados, no hacemos nada.
        }

        var vigenciaDesde = new DateOnly(2025, 1, 1);

        var regimen78 = new Regimen
        {
            Codigo = 78,
            Descripcion = "Enajenación de bienes muebles y bienes de cambio",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.10m,
            TasaNoInscriptoResto = 0.10m,
            MontoNoSujetoARetencion = 224000m,
            MinimoRetencion = 240m,
            VigenciaDesde = vigenciaDesde
        };

        var regimen94 = new Regimen
        {
            Codigo = 94,
            Descripcion = "Locaciones de obra y/o servicios no ejecutados en relación de dependencia",
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.28m,
            TasaNoInscriptoResto = 0.25m,
            MontoNoSujetoARetencion = 67170m,
            MinimoRetencion = 240m,
            VigenciaDesde = vigenciaDesde
        };

        var regimen116 = new Regimen
        {
            Codigo = 116,
            Descripcion = "Honorarios de director de sociedades anónimas y cargos similares",
            TipoCalculo = TipoCalculoRegimen.EscalaProgresiva,
            TasaInscripto = 0m,
            TasaNoInscriptoHumana = 0.28m,
            TasaNoInscriptoResto = 0.28m,
            MontoNoSujetoARetencion = 67170m,
            MinimoRetencion = 240m,
            VigenciaDesde = vigenciaDesde,
            Tramos = new List<TramoEscala>
            {
                new TramoEscala { Desde = 0,     Hasta = 8000,  MontoFijo = 0,     Porcentaje = 0.05m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 8000,  Hasta = 16000, MontoFijo = 400,   Porcentaje = 0.09m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 16000, Hasta = 24000, MontoFijo = 1120,  Porcentaje = 0.12m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 24000, Hasta = 32000, MontoFijo = 2080,  Porcentaje = 0.15m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 32000, Hasta = 48000, MontoFijo = 3280,  Porcentaje = 0.19m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 48000, Hasta = 64000, MontoFijo = 6320,  Porcentaje = 0.23m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 64000, Hasta = 96000, MontoFijo = 10000, Porcentaje = 0.27m, VigenciaDesde = vigenciaDesde },
                new TramoEscala { Desde = 96000, Hasta = null,  MontoFijo = 18640, Porcentaje = 0.31m, VigenciaDesde = vigenciaDesde },
            }
        };

        context.Regimenes.AddRange(regimen78, regimen94, regimen116);
        context.SaveChanges();

        Console.WriteLine("Se cargaron los regímenes 78, 94 y 116 con sus datos reales.");
    }

    private static void SeedUsuariosDePrueba(RetencionesRG830DbContext context)
    {
        if (context.Usuarios.Any())
        {
            return; // ya hay usuarios cargados, no hacemos nada.
        }

        var gimi = new Cliente
        {
            Cuit = "30-61234567-4",
            RazonSocial = "Metalúrgica del Valle S.A.",
            Domicilio = "Av. San Martín 1250",
            Localidad = "Salta - SALTA",
            NombreFirmante = "Roberto Daniel Suárez",
            CargoFirmante = "Presidente"
        };
        context.Clientes.Add(gimi);
        context.SaveChanges(); // se guarda ya, para que "gimi.Id" quede asignado.

        context.Usuarios.AddRange(
            new Usuario
            {
                Email = "estudio@ejemplo.com",
                PasswordHash = PasswordHasher.Hash("Estudio123!"),
                Rol = RolUsuario.Estudio,
                ClienteId = null
            },
            new Usuario
            {
                Email = "operador@cliente.com",
                PasswordHash = PasswordHasher.Hash("Cliente123!"),
                Rol = RolUsuario.Cliente,
                ClienteId = gimi.Id
            }
        );
        context.SaveChanges();

        Console.WriteLine("Se creó el cliente Metalúrgica del Valle S.A. y dos usuarios de prueba: estudio@ejemplo.com (rol Estudio) y operador@cliente.com (rol Cliente).");
    }

    private static void SeedProveedoresDePrueba(RetencionesRG830DbContext context)
    {
        if (context.Proveedores.Any())
        {
            return; // ya hay proveedores cargados, no hacemos nada.
        }

        var gimi = context.Clientes.FirstOrDefault(c => c.Cuit == "30-61234567-4");
        if (gimi is null) return;

        const string localidad = "Salta - SALTA";

        // Los cuatro proveedores reales de la hoja "Proveedores" de la
        // planilla del estudio. Se cargan solos para poder reiniciar la base
        // de datos sin tener que tipearlos de nuevo antes de cada prueba
        // o demostración.
        context.Proveedores.AddRange(
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "20-31456789-8",
                RazonSocial = "Gómez, Carlos Alberto",
                Domicilio = "Belgrano 340",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.HumanaYSucesionIndivisa
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "30-64567891-1",
                RazonSocial = "Servicios del Sur S.H.",
                Domicilio = "Rivadavia 88",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.Resto
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "27-32567894-7",
                RazonSocial = "Ramírez, Laura Beatriz",
                Domicilio = "Sarmiento 512",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.HumanaYSucesionIndivisa
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "27-33678912-0",
                RazonSocial = "Sosa, Marta Elena",
                Domicilio = "Güemes 275",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.HumanaYSucesionIndivisa
            });

        context.SaveChanges();

        Console.WriteLine("Se cargaron los cuatro proveedores de Metalúrgica del Valle S.A.");
    }
}