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
        SeedSegundoClienteDePrueba(context);
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

    /// <summary>
    /// Un segundo cliente, con sus proveedores y algunas operaciones, para poder
    /// probar las pantallas que muestran varios clientes a la vez (por ejemplo, la
    /// columna Cliente del listado de operaciones). Todos los datos son ficticios;
    /// los CUIT son matemáticamente válidos.
    ///
    /// A diferencia de los métodos anteriores, no pregunta si la tabla está vacía
    /// sino si este cliente ya existe: así se agrega también en las bases que ya
    /// estaban creadas, sin tener que borrarlas.
    /// </summary>
    private static void SeedSegundoClienteDePrueba(RetencionesRG830DbContext context)
    {
        const string cuitCliente = "30-71845236-4";
        if (context.Clientes.Any(c => c.Cuit == cuitCliente))
        {
            return; // ya está cargado, no hacemos nada.
        }

        var estudio = context.Usuarios.FirstOrDefault(u => u.Rol == RolUsuario.Estudio);
        var regimen78 = context.Regimenes.FirstOrDefault(r => r.Codigo == 78);
        var regimen94 = context.Regimenes.FirstOrDefault(r => r.Codigo == 94);
        if (estudio is null || regimen78 is null || regimen94 is null) return;

        const string localidad = "San Salvador de Jujuy - JUJUY";

        var agroinsumos = new Cliente
        {
            Cuit = cuitCliente,
            RazonSocial = "Agroinsumos del Norte S.R.L.",
            Domicilio = "Av. Fascio 820",
            Localidad = localidad,
            NombreFirmante = "Mariela Andrea Correa",
            CargoFirmante = "Socia gerente"
        };
        context.Clientes.Add(agroinsumos);
        context.SaveChanges(); // para que "agroinsumos.Id" quede asignado.

        var transportes = new Proveedor
        {
            ClienteId = agroinsumos.Id,
            Cuit = "30-70927518-2",
            RazonSocial = "Transportes Altiplano S.A.",
            Domicilio = "Ruta 9 km 1650",
            Localidad = localidad,
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.Resto
        };
        var ruiz = new Proveedor
        {
            ClienteId = agroinsumos.Id,
            Cuit = "20-28765431-7",
            RazonSocial = "Ruiz, Héctor Damián",
            Domicilio = "Lavalle 145",
            Localidad = localidad,
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        var paz = new Proveedor
        {
            ClienteId = agroinsumos.Id,
            Cuit = "27-34981206-7",
            RazonSocial = "Paz, Silvia Noemí",
            Domicilio = "Necochea 63",
            Localidad = localidad,
            InscriptoEnGanancias = false,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        context.Proveedores.AddRange(transportes, ruiz, paz);
        context.SaveChanges();

        // Las operaciones se registran con el mismo servicio que usa la pantalla de
        // confirmación: el monto retenido, el acumulado del mes y el número de
        // certificado los calcula el sistema, no se escriben acá. Así el seed no
        // puede contradecir al motor de cálculo ni a los regímenes cargados.
        // Las dos de Ruiz en el régimen 78 muestran el acumulado mensual.
        // El servicio es asincrónico y Seed no: al arrancar la aplicación no hay
        // contexto de sincronización, así que esperar el resultado es seguro.
        var registro = new Servicios.ServicioRegistroOperaciones(context);
        var operaciones = new[]
        {
            NuevaOperacion(agroinsumos, transportes, regimen94, 1, 4, 561, new DateOnly(2026, 9, 10), 726_000m, 600_000m, estudio),
            NuevaOperacion(agroinsumos, ruiz, regimen78, 1, 2, 3310, new DateOnly(2026, 9, 15), 484_000m, 400_000m, estudio),
            NuevaOperacion(agroinsumos, paz, regimen94, 4, 1, 45, new DateOnly(2026, 9, 18), 250_000m, 250_000m, estudio),
            NuevaOperacion(agroinsumos, ruiz, regimen78, 1, 2, 3327, new DateOnly(2026, 9, 25), 363_000m, 300_000m, estudio),
        };
        foreach (var operacion in operaciones)
        {
            registro.RegistrarAsync(operacion).GetAwaiter().GetResult();
        }

        Console.WriteLine("Se cargó el cliente Agroinsumos del Norte S.R.L. con tres proveedores y cuatro operaciones.");
    }

    private static Operacion NuevaOperacion(Cliente cliente, Proveedor proveedor, Regimen regimen,
        int tipoComprobante, int puntoVenta, long numero, DateOnly fecha,
        decimal importeTotal, decimal importeGravado, Usuario cargadaPor) => new()
    {
        ClienteId = cliente.Id,
        ProveedorId = proveedor.Id,
        RegimenId = regimen.Id,
        TipoComprobante = tipoComprobante,
        PuntoVenta = puntoVenta,
        NumeroComprobante = numero,
        FechaComprobante = fecha,
        FechaRetencion = fecha,
        ImporteComprobante = importeTotal,
        ImporteGravado = importeGravado,
        CreadoPorUsuarioId = cargadaPor.Id
    };
}