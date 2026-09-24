using AulaInnova.API.Authentication;
using AulaInnova.API.Configuration;
using AulaInnova.API.Data;
using AulaInnova.API.Middleware;
using AulaInnova.API.Models;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Base de datos
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión 'DefaultConnection'.");

builder.Services.AddDbContext<AulaInnovaDbContext>(options =>
    options.UseSqlServer(connectionString));

// Identity
builder.Services.Configure<IdentityOptions>(options =>
{
    // Bloqueo por fuerza bruta: 5 intentos fallidos -> 15 min de bloqueo.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AulaInnovaDbContext>()
    .AddSignInManager();

// JWT
var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "No se encontró la configuración 'Jwt'.");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
    throw new InvalidOperationException(
        "No se encontró el secreto JWT.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    })
    // Esquema aparte para estaciones físicas (ai.estacion_secreto), no
    // para personas. No es el esquema por defecto: solo se usa cuando
    // un endpoint lo pide explícitamente con
    // [Authorize(AuthenticationSchemes = "Estacion")].
    .AddScheme<AuthenticationSchemeOptions, EstacionAuthenticationHandler>(
        EstacionAuthenticationHandler.SchemeName,
        options => { });

// Autorización
builder.Services.AddAuthorization();

// Servicios
builder.Services
    .Configure<BootstrapAdminSettings>(
        builder.Configuration.GetSection("BootstrapAdmin"));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminBootstrapService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<MovementService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<CoverageService>();
builder.Services.AddScoped<VisualService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<AdminPersonService>();
builder.Services.AddScoped<AdminClassroomService>();
builder.Services.AddScoped<AdminGroupService>();
builder.Services.AddScoped<AdminSubjectService>();
builder.Services.AddScoped<AdminJornadaService>();
builder.Services.AddScoped<AdminHorarioService>();
builder.Services.AddScoped<AdminMatriculaService>();
builder.Services.AddScoped<AdminAsignacionService>();
builder.Services.AddScoped<AdminNfcService>();
builder.Services.AddScoped<AdminEstacionService>();
builder.Services.AddScoped<AdminFallaEstacionService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<AuditService>();

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Bootstrap del administrador
using (var scope = app.Services.CreateScope())
{
    var adminBootstrap = scope.ServiceProvider
        .GetRequiredService<AdminBootstrapService>();

    await adminBootstrap.EnsureAdminAsync();
}

// OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS
// app.UseHttpsRedirection();

// Authentication
app.UseAuthentication();

// Request context middleware
app.UseMiddleware<RequestContextMiddleware>();

// Authorization
app.UseAuthorization();

// Controllers
app.MapControllers();

app.Run();