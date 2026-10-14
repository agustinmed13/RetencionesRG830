using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RetencionesRG830.Infrastructure.Servicios;
using QuestPDF.Infrastructure;
using RetencionesRG830.Web.Infraestructura;

var builder = WebApplication.CreateBuilder(args);
// Fijamos el formato de números, fechas y moneda al de Argentina, para que
// la aplicación se vea igual sin importar la configuración regional de la
// computadora donde se ejecute -el mismo tipo de problema que te contó
// Daniel que tenían con el archivo de SICORE.
var culturaArgentina = new System.Globalization.CultureInfo("es-AR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culturaArgentina;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culturaArgentina;
// QuestPDF exige declarar bajo qué licencia se lo usa. La Community es
// gratuita para proyectos académicos y empresas chicas.
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.AddControllersWithViews(opciones =>
{
    opciones.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());

    // Exige el token antifalsificación en TODOS los POST (también PUT y DELETE), sin
    // tener que acordarse de poner [ValidateAntiForgeryToken] acción por acción. Los
    // GET, que no modifican nada, quedan afuera. Sin esto, una página de otro sitio
    // podría hacer que un usuario con la sesión abierta registre o anule operaciones
    // sin saberlo (CSRF). El token lo agrega solo el tag helper de <form> en las vistas.
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    // Con <Nullable>enable</Nullable>, ASP.NET trata como obligatoria toda
    // propiedad string que no sea string?, aunque no tenga [Required], y avisa en
    // inglés ("The Domicilio field is required."). Eso hacía obligatorios campos
    // sin que nadie lo decidiera, y sin asterisco en el formulario. Se apaga: un
    // texto es obligatorio sólo si lo declara su [Required], con mensaje propio.
    opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    // Red de seguridad: los mensajes que ASP.NET genera solo al leer el formulario,
    // por ejemplo "The value '' is invalid." cuando un número o una fecha llegan
    // vacíos. Lo normal es que cada pantalla dé su propio mensaje; éstos aparecen
    // sólo si algún campo se escapa, y así al menos aparecen en español.
    var mensajes = opciones.ModelBindingMessageProvider;
    mensajes.SetValueMustNotBeNullAccessor(_ => "Completá este campo.");
    mensajes.SetMissingBindRequiredValueAccessor(campo => $"Falta el dato {campo}.");
    mensajes.SetMissingKeyOrValueAccessor(() => "Falta un dato.");
    mensajes.SetMissingRequestBodyRequiredValueAccessor(() => "No llegaron los datos del formulario.");
    mensajes.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"\"{valor}\" no es un valor válido para {campo}.");
    mensajes.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"\"{valor}\" no es un valor válido.");
    mensajes.SetUnknownValueIsInvalidAccessor(campo => $"El valor ingresado no es válido para {campo}.");
    mensajes.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor ingresado no es válido.");
    mensajes.SetValueIsInvalidAccessor(valor => $"\"{valor}\" no es un valor válido.");
    mensajes.SetValueMustBeANumberAccessor(campo => $"{campo} tiene que ser un número.");
    mensajes.SetNonPropertyValueMustBeANumberAccessor(() => "Tiene que ser un número.");
});

// Conectamos el DbContext a SQL Server (hasta octubre de 2026 era SQLite).
//
// La cadena de conexión NO está en appsettings.json: el repositorio es público, y
// además cada máquina tiene su propio servidor (SQL Server Express en una, LocalDB
// en otra). En desarrollo cada máquina la guarda en User Secrets, fuera del
// repositorio:
//   dotnet user-secrets set ConnectionStrings:DefaultConnection "<cadena>" --project src/RetencionesRG830.Web
// En producción llega por la variable de entorno ConnectionStrings__DefaultConnection.
//
// Si falta, la aplicación no arranca y dice cómo configurarla: nunca usa otra base
// en silencio.
var cadenaDeConexion = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(cadenaDeConexion))
{
    throw new InvalidOperationException(
        "Falta la cadena de conexión a SQL Server (ConnectionStrings:DefaultConnection). "
        + "En desarrollo se configura con: dotnet user-secrets set ConnectionStrings:DefaultConnection "
        + "\"Server=.\\SQLEXPRESS;Database=SIGERE;Trusted_Connection=True;TrustServerCertificate=True\" "
        + "--project src/RetencionesRG830.Web (en una máquina con LocalDB, Server=(localdb)\\MSSQLLocalDB). "
        + "Ver CLAUDE.md, sección de SQL Server.");
}

builder.Services.AddDbContext<RetencionesRG830DbContext>(options =>
    options.UseSqlServer(cadenaDeConexion));

builder.Services.AddScoped<ServicioRegistroOperaciones>();
builder.Services.AddScoped<ServicioProveedores>();
builder.Services.AddScoped<ServicioClientes>();
builder.Services.AddScoped<ServicioUsuarios>();
builder.Services.AddScoped<GeneradorCertificadoPdf>();
builder.Services.AddScoped<GeneradorArchivoSicore>();
// Registramos el sistema de autenticación por cookies: cuando alguien haga
// login, se le va a crear una cookie que dice quién es y qué rol tiene, para
// no pedirle usuario y contraseña en cada página que visite.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Cuenta/Login";
        options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    });

builder.Services.AddAuthorization();    

var app = builder.Build();
// Al arrancar, nos aseguramos de que la base de datos esté actualizada
// (aplica migraciones pendientes) y cargamos los datos iniciales si hace falta.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RetencionesRG830DbContext>();
    db.Database.Migrate();
    DbInitializer.Seed(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
