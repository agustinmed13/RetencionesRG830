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
});

// Conectamos el DbContext a una base de datos SQLite: un solo archivo
// (retenciones.db) que se crea en la carpeta del proyecto Web. La cadena de
// conexión ("dónde está el archivo") vive en appsettings.json, no acá, para
// poder cambiarla sin tocar código (por ejemplo, el día de mañana, para
// apuntar a un SQL Server en vez de a SQLite).
builder.Services.AddDbContext<RetencionesRG830DbContext>(options => 
options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ServicioRegistroOperaciones>();
builder.Services.AddScoped<ServicioProveedores>();
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
