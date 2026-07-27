using FluentValidation; // 👈 Indispensable para los validadores
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Behaviors;
using SalesSaaS.Features.Customers.Commands;
using SalesSaaS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Conectamos nuestro DbContext con la cadena de conexión
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Registramos MediatR Y el ValidationBehavior (Versión MediatR 12+)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    // 🪄 Esta es la forma oficial en MediatR 12+ de conectar el Pipeline Behavior
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// 3. Registramos los Controllers y los Validadores de FluentValidation
builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssembly(typeof(CreateCustomerCommandValidator).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Asegurar base de datos creada al iniciar (desarrollo)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();