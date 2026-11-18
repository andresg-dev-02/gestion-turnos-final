using Asp.Versioning;
using GestionTurnos.Api.Application.Extensions;
using GestionTurnos.Api.Infrastructure.Extensions;
using GestionTurnos.Api.Infrastructure.Logging;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using GestionTurnos.Api.Infrastructure.Persistence.Seed;
using GestionTurnos.Api.Presentation.Extensions;
using GestionTurnos.Api.Presentation.Middlewares;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging con Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.With(new SensitiveDataEnricher()));

// 2. Inyección de dependencias modular
builder.Services.AddPresentation(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddNotificacionesCorreo(builder.Configuration); // Después de AddApplication (orden de behaviors)
builder.Services.AddSecurity(builder.Configuration);

// 3. Data Protection con persistencia en SQL Server
builder.Services.AddDataProtection()
    .SetApplicationName("GestionTurnos")
    .PersistKeysToDbContext<DataProtectionKeyContext>()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// 4. API Versioning
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// 5. Swagger
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCustomSwagger();
}

// 6. Health Checks
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<GestionTurnosContext>();

var app = builder.Build();

// Datos iniciales por línea de comandos (sin endpoint): dotnet run -- seed-admin
if (args.Contains(AdministradorInicialSeeder.ComandoCli, StringComparer.OrdinalIgnoreCase))
{
    Environment.ExitCode = await AdministradorInicialSeeder.RunAsync(app.Services);
    await Log.CloseAndFlushAsync();
    return;
}

// 7. Pipeline HTTP
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GestionTurnos.Api v1");
        options.RoutePrefix = string.Empty;
    });
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AngularPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LogContextMiddleware>();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
