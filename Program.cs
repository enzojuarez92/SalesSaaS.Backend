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

var builder = WebApplication.CreateBuilder(args);

// 1. Conectamos nuestro DbContext con la cadena de conexión
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IFiscalProfileSecretProtector, FiscalProfileSecretProtector>();
builder.Services.AddScoped<IAfipService, AfipService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();
builder.Services.AddScoped<IPaymentGatewayService, DevelopmentPaymentGatewayService>();
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
builder.Services.AddControllers();
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
app.UseMiddleware<SubscriptionGatekeeperMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Aplicar migraciones pendientes solamente en desarrollo. En producción,
// las migraciones deben ejecutarse como parte del despliegue.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await context.Database.MigrateAsync();
    }
}

app.Run();
