# 🚀 SalesSaaS - Multi-Tenant ERP & Sales System

Un sistema de gestión de ventas y stock SaaS Multi-Tenant desarrollado con **.NET 10** aplicando principios de **Clean Architecture**, **CQRS** y patrones enterprise.

---

## 🏗️ Arquitectura y Patrones Aplicados

* **Clean Architecture / Vertical Slices:** Separación clara entre Dominio, Infraestructura y Aplicación.
* **CQRS con MediatR:** Desacoplamiento de comandos y consultas.
* **Pipeline Behaviors:** Validaciones transversales centralizadas con **FluentValidation**.
* **Persistence:** Entity Framework Core con **Fluent API** (`IEntityTypeConfiguration`) para un `DbContext` ultra limpio.
* **Database:** SQL Server con soporte Multi-Tenancy por aislamiento lógico (`TenantId`), migraciones de EF Core y control de concurrencia de stock.

---

## 🛠️ Tecnologías Usadas

* **Framework:** .NET 10 / C#
* **ORM:** Entity Framework Core
* **Validación:** FluentValidation
* **Patrón de Mediacions:** MediatR

---

## 🚀 Cómo Ejecutar el Proyecto Localmente

1. **Configurar el contenedor de SQL Server:**

   ```bash
   Copy-Item .env.example .env
   ```

   Elegí una contraseña segura en `.env` y levantá la base:

   ```bash
   docker compose up -d
   ```

2. **Configurar el secreto de conexión para la API:**

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=SalesSaaSDB;User Id=sa;Password=TU_MISMA_CONTRASENA;TrustServerCertificate=True;"
   ```

3. **Ejecutar la aplicación:**

   ```bash
   dotnet run
   ```

   En desarrollo, la API aplica automáticamente las migraciones pendientes. Swagger queda disponible en la URL que indique la consola.

> `.env` y los User Secrets no se versionan. Nunca agregues contraseñas reales a `appsettings.json`.

## Autenticación local

Antes de ejecutar la API, configurá una clave JWT local (mínimo 32 caracteres):

```powershell
dotnet user-secrets set "Jwt:Key" "una-clave-local-larga-y-segura-de-32-caracteres"
```

Usá `POST /api/auth/register` para crear el primer negocio y su usuario `Owner`.
Luego `POST /api/auth/login` devuelve un token Bearer con el tenant y el rol. El
`Owner` puede crear usuarios `Admin`, `Seller` o `Warehouse` mediante
`POST /api/tenant-members`.
