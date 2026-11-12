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
        SeedOperacionesDeMetalurgica(context);
        SeedOperadorDeAgroinsumos(context);
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
            Cuit = "30612345674",
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

        var gimi = context.Clientes.FirstOrDefault(c => c.Cuit == "30612345674");
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
                Cuit = "20314567898",
                RazonSocial = "Gómez, Carlos Alberto",
                Domicilio = "Belgrano 340",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.HumanaYSucesionIndivisa
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "30645678911",
                RazonSocial = "Servicios del Sur S.H.",
                Domicilio = "Rivadavia 88",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.Resto
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "27325678947",
                RazonSocial = "Ramírez, Laura Beatriz",
                Domicilio = "Sarmiento 512",
                Localidad = localidad,
                InscriptoEnGanancias = true,
                TipoPersona = TipoPersona.HumanaYSucesionIndivisa
            },
            new Proveedor
            {
                ClienteId = gimi.Id,
                Cuit = "27336789120",
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
        const string cuitCliente = "30718452364";
        if (context.Clientes.Any(c => c.Cuit == cuitCliente))
        {
            return; // ya está cargado, no hacemos nada.
        }

        var estudio = context.Usuarios.FirstOrDefault(u => u.Rol == RolUsuario.Estudio);
        if (estudio is null || !context.Regimenes.Any(r => r.Codigo == 78) || !context.Regimenes.Any(r => r.Codigo == 94)) return;

        // La versión de cada régimen se resuelve con la fecha de cada operación
        // (regla 9), con la misma regla que el cálculo. Antes se tomaba
        // FirstOrDefault(r => r.Codigo == 78): con dos versiones cargadas, la que la
        // base devolviera primero, sin mirar la fecha.
        Regimen Vigente(int codigo, DateOnly fecha) =>
            VigenciaRegimen.VersionVigente(context.Regimenes.Where(r => r.Codigo == codigo).ToList(), fecha)
            ?? throw new InvalidOperationException($"El régimen {codigo} no tiene valores vigentes al {fecha:dd/MM/yyyy}.");

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
            Cuit = "30709275182",
            RazonSocial = "Transportes Altiplano S.A.",
            Domicilio = "Ruta 9 km 1650",
            Localidad = localidad,
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.Resto
        };
        var ruiz = new Proveedor
        {
            ClienteId = agroinsumos.Id,
            Cuit = "20287654317",
            RazonSocial = "Ruiz, Héctor Damián",
            Domicilio = "Lavalle 145",
            Localidad = localidad,
            InscriptoEnGanancias = true,
            TipoPersona = TipoPersona.HumanaYSucesionIndivisa
        };
        var paz = new Proveedor
        {
            ClienteId = agroinsumos.Id,
            Cuit = "27349812067",
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
            NuevaOperacion(agroinsumos, transportes, Vigente(94, new DateOnly(2026, 9, 10)), 1, 4, 561, new DateOnly(2026, 9, 10), 726_000m, 600_000m, estudio),
            NuevaOperacion(agroinsumos, ruiz, Vigente(78, new DateOnly(2026, 9, 15)), 1, 2, 3310, new DateOnly(2026, 9, 15), 484_000m, 400_000m, estudio),
            NuevaOperacion(agroinsumos, paz, Vigente(94, new DateOnly(2026, 9, 18)), 2, 1, 45, new DateOnly(2026, 9, 18), 250_000m, 250_000m, estudio),
            NuevaOperacion(agroinsumos, ruiz, Vigente(78, new DateOnly(2026, 9, 25)), 1, 2, 3327, new DateOnly(2026, 9, 25), 363_000m, 300_000m, estudio),
        };
        foreach (var operacion in operaciones)
        {
            registro.RegistrarAsync(AlcanceEmpresa.Estudio, operacion).GetAwaiter().GetResult();
        }

        Console.WriteLine("Se cargó el cliente Agroinsumos del Norte S.R.L. con tres proveedores y cuatro operaciones.");
    }

    /// <summary>
    /// Operaciones para Metalúrgica del Valle, la empresa de operador@cliente.com. Sin
    /// ellas, ese usuario entraba a un listado vacío, que no demuestra la HU-15 (cada
    /// empresa ve sólo lo suyo). Datos inventados; los importes son redondos a propósito,
    /// para que no se confundan con los de la planilla real.
    ///
    /// Octubre de 2026, con los mismos casos que se quieren mostrar:
    /// - Gómez, régimen 78, dos pagos en el mes: el segundo acumula y descuenta lo ya
    ///   retenido. Retienen $ 5.920 y $ 3.600.
    /// - Servicios del Sur, régimen 94: retiene $ 5.656,60.
    /// - Ramírez, régimen 94, $ 70.000: queda debajo del mínimo y se registra sin
    ///   certificado (regla 10).
    /// Esas cifras valen para una base nueva: si Metalúrgica ya tenía operaciones de
    /// octubre de 2026, el acumulado las incluye.
    ///
    /// Tiene su propio control, como SeedSegundoClienteDePrueba, para llegar también a
    /// las bases ya creadas: no carga nada si ya existe la primera de estas operaciones
    /// (la factura 00007-00001201 de Gómez). Si falta algún dato del que depende
    /// (el cliente, uno de sus proveedores, el usuario Estudio, un régimen), no carga
    /// nada.
    /// </summary>
    private static void SeedOperacionesDeMetalurgica(RetencionesRG830DbContext context)
    {
        var metalurgica = context.Clientes.FirstOrDefault(c => c.Cuit == "30612345674");
        var estudio = context.Usuarios.FirstOrDefault(u => u.Rol == RolUsuario.Estudio);
        if (metalurgica is null || estudio is null) return;

        Proveedor? DeMetalurgica(string cuit) =>
            context.Proveedores.FirstOrDefault(p => p.ClienteId == metalurgica.Id && p.Cuit == cuit);
        var gomez = DeMetalurgica("20314567898");
        var servicios = DeMetalurgica("30645678911");
        var ramirez = DeMetalurgica("27325678947");
        if (gomez is null || servicios is null || ramirez is null) return;

        if (context.Operaciones.Any(o => o.ClienteId == metalurgica.Id && o.ProveedorId == gomez.Id
                                      && o.PuntoVenta == 7 && o.NumeroComprobante == 1201))
            return; // ya está cargado, no hacemos nada.

        var registro = new Servicios.ServicioRegistroOperaciones(context);
        Regimen? Vigente(int codigo, DateOnly fecha) =>
            registro.VersionVigenteAsync(codigo, fecha).GetAwaiter().GetResult();

        var operaciones = new (Proveedor Proveedor, int Codigo, int PuntoVenta, long Numero, DateOnly Fecha, decimal Total, decimal Gravado)[]
        {
            (gomez,     78, 7, 1201, new DateOnly(2026, 10, 6),  629_200m, 520_000m),
            (servicios, 94, 3,  845, new DateOnly(2026, 10, 9),  423_500m, 350_000m),
            (ramirez,   94, 1,   77, new DateOnly(2026, 10, 14),  70_000m,  70_000m),
            (gomez,     78, 7, 1236, new DateOnly(2026, 10, 20), 217_800m, 180_000m),
        };

        // Primero se controla que haya versión vigente para todas: así no queda la mitad
        // cargada si falta un régimen.
        var versiones = operaciones.Select(o => Vigente(o.Codigo, o.Fecha)).ToList();
        if (versiones.Any(v => v is null)) return;

        for (var i = 0; i < operaciones.Length; i++)
        {
            var o = operaciones[i];
            registro.RegistrarAsync(AlcanceEmpresa.Estudio, NuevaOperacion(
                metalurgica, o.Proveedor, versiones[i]!, 1, o.PuntoVenta, o.Numero, o.Fecha, o.Total, o.Gravado, estudio))
                .GetAwaiter().GetResult();
        }

        Console.WriteLine("Se cargaron cuatro operaciones de octubre de 2026 para Metalúrgica del Valle S.A.");
    }

    /// <summary>
    /// Un segundo usuario del rol Cliente, de Agroinsumos del Norte: con él y con
    /// operador@cliente.com (Metalúrgica del Valle) se muestra que cada empresa ve sólo
    /// lo suyo, en los dos sentidos (HU-15). Correo de un dominio de ejemplo, inventado.
    /// Con su propio control, para llegar también a las bases ya creadas.
    /// </summary>
    private static void SeedOperadorDeAgroinsumos(RetencionesRG830DbContext context)
    {
        const string email = "agroinsumos@ejemplo.com";
        var agroinsumos = context.Clientes.FirstOrDefault(c => c.Cuit == "30718452364");
        if (agroinsumos is null || context.Usuarios.Any(u => u.Email == email)) return;

        context.Usuarios.Add(new Usuario
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash("Cliente123!"),
            Rol = RolUsuario.Cliente,
            ClienteId = agroinsumos.Id
        });
        context.SaveChanges();

        Console.WriteLine($"Se creó el usuario {email} (rol Cliente) de Agroinsumos del Norte S.R.L.");
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