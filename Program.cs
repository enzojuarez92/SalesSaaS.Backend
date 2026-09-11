using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalesSaaS.Application.Exceptions;
using SalesSaaS.Application.Behaviors;
using SalesSaaS.Features.Customers.Commands;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Infrastructure.Security;
using SalesSaaS.Infrastructure.Afip;
using SalesSaaS.Application.Afip;
using SalesSaaS.Application.Reporting;
using SalesSaaS.Infrastructure.Reporting;
using SalesSaaS.Application.Billing;
using SalesSaaS.Infrastructure.Billing;
using SalesSaaS.Application.Notifications;
using SalesSaaS.Infrastructure.Notifications;
using SalesSaaS.Infrastructure.Health;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

// 1. Conectamos el DbContext. SQL Server local en Docker usa un certificado
// autogenerado, por lo que Development debe confiar explícitamente en él.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configurá ConnectionStrings:DefaultConnection.");
if (builder.Environment.IsDevelopment())
{
    var sqlConnection = new SqlConnectionStringBuilder(connectionString)
    {
        Encrypt = false,
        TrustServerCertificate = true
    };
    connectionString = sqlConnection.ConnectionString;
}
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException("Configurá una clave Jwt:Key de al menos 32 caracteres mediante User Secrets o variables de entorno.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RefreshTokenOptions>(builder.Configuration.GetSection(RefreshTokenOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("Afip", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddHttpClient<IPaymentGatewayService, MercadoPagoService>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<AfipHealthCheck>("afip", tags: ["ready", "external"])
    .AddCheck<EmailQueueHealthCheck>("email-queue", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("billing", httpContext => RateLimitPartition.GetSlidingWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new SlidingWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IFiscalProfileSecretProtector, FiscalProfileSecretProtector>();
builder.Services.AddScoped<IAfipService, AfipService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();
builder.Services.AddScoped<ISubscriptionGatekeeper, SubscriptionGatekeeper>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddHostedService<EmailBackgroundService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy => policy.RequireClaim("platform_admin", "true"));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 2. Registramos MediatR Y el ValidationBehavior (Versión MediatR 12+)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TenantAuthorizationBehavior<,>));
});

// 3. Registramos los Controllers y los Validadores de FluentValidation
builder.Services.AddControllers(options => options.Filters.Add<RequestScopeFilter>());
builder.Services.AddValidatorsFromAssembly(typeof(CreateCustomerCommandValidator).Assembly);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ⚡ 4. AGREGAMOS LOS SERVICIOS DE SWAGGER AQUÍ ⚡
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

// ⚡ 5. HABILITAMOS SWAGGER EN EL PIPELINE (Ideal para desarrollo) ⚡
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline
//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<ActiveContextMiddleware>();
app.UseMiddleware<SubscriptionGatekeeperMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

// Aplicar migraciones pendientes solamente en desarrollo. En producción,
// las migraciones deben ejecutarse como parte del despliegue.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await context.Database.MigrateAsync();
    }
    await DefaultWarehouseSeeder.EnsureActiveWarehouseForEveryTenantAsync(context);
    await SubscriptionPlanSeeder.EnsurePlansAsync(context);
}

app.Run();
