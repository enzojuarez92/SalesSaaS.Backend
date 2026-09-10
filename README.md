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

## Plataforma y stack

SalesSaaS es un SaaS multi-tenant para ventas POS, facturación electrónica
AFIP, caja diaria, clientes con cuenta corriente, inventario multi-depósito,
reportes y suscripciones.

| Capa | Tecnología |
| --- | --- |
| API | .NET 10, ASP.NET Core Web API, EF Core, MediatR y FluentValidation |
| UI | Vue 3, TypeScript, Vite, Tailwind CSS, Pinia, Vue Router, Axios y Lucide |
| Persistencia | SQL Server (contenedor incluido) |
| Documentos | ClosedXML para Excel y QuestPDF para comprobantes/PDF |
| Despliegue | Docker Compose, Nginx y health checks |

## Inicio rápido completo

En dos terminales, desde las carpetas hermanas:

```powershell
# API: SalesSaaS.Backend
dotnet restore
dotnet run --launch-profile http

# UI: SalesSaaS.Frontend
npm ci
npm run dev
```

La UI de desarrollo queda en `http://localhost:5173`. Copiá
`SalesSaaS.Frontend/.env.example` a `.env.local` si necesitás cambiar la URL
de la API.

Para ejecutar el stack integrado de producción local:

```powershell
Copy-Item .env.example .env
# Editar .env con secretos locales propios
docker compose up --build -d
docker compose ps
```

El frontend queda en `http://localhost:8080`; Nginx redirige `/api` al
contenedor de la API. Para detenerlo: `docker compose down`.

## Variables de entorno

Las claves pueden configurarse como variables de entorno con `__` en vez de
`:` (por ejemplo, `Jwt__Key`). No versionar `.env`, certificados ni claves.

| Variable | Propósito |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Cadena de conexión SQL Server |
| `Jwt__Key` | Secreto JWT de al menos 32 caracteres |
| `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia del JWT |
| `Database__MigrateOnStartup` | `true` para aplicar migraciones al arrancar un contenedor API |
| `Afip__Environment` | `Homologation` o `Production` |
| `Afip__Cuit`, `Afip__Certificate`, `Afip__PrivateKey` | Credenciales AFIP por ambiente; almacenar secretas |
| `MercadoPago__AccessToken` | Access token de Mercado Pago; ausente en Development habilita el flujo simulado |
| `MercadoPago__SuccessUrl`, `MercadoPago__FailureUrl`, `MercadoPago__WebhookUrl` | URLs públicas del checkout y webhook |
| `Email__Host`, `Email__Port`, `Email__User`, `Email__Password` | SMTP para envío de comprobantes |

## Salud y datos iniciales

- `GET /health` verifica la disponibilidad general.
- `GET /health/ready` incorpora base de datos, AFIP y cola de correo para el
  orquestador.
- Al iniciar, se aplican migraciones en Development o cuando
  `Database__MigrateOnStartup=true`.
- `SubscriptionPlanSeeder` garantiza los planes Starter, Pro y Enterprise.
- `DefaultWarehouseSeeder` crea **Depósito Principal** para cada tenant que no
  posea un depósito activo. El alta de un tenant también crea su depósito y su
  suscripción de prueba.

## Multi-depósito

El catálogo (`Product`, `Category`) es global al tenant. En cambio, las ventas,
caja y reportes de ventas se consultan por `WarehouseId` seleccionado. El
stock operativo se calcula a partir de `StockMovement` del depósito activo:
el alta de un producto registra su stock inicial en ese depósito, los ajustes
validan el saldo local y el POS no permite vender unidades que estén en otra
sucursal.

`Product.Stock` se conserva como total denormalizado del tenant por
compatibilidad. Los datos antiguos cargados antes de este criterio y que no
tengan movimientos iniciales deben regularizarse mediante un ajuste positivo
en su depósito real antes de venderlos.

## Cuenta corriente y auditoría por sucursal

Los movimientos nuevos de cuenta corriente requieren `WarehouseId`: tanto los
fiados facturados como los cobros se consultan y se imputan al depósito activo.
El límite de crédito del cliente se conserva global para evitar que una venta
en otra sucursal exceda su crédito total. Los registros anteriores a la
migración permanecen con sucursal nula para no alterar su trazabilidad.

`AuditLog.WarehouseId` también es opcional: se completa automáticamente para
operaciones que contienen depósito, pedido o sesión de caja. Las acciones de
configuración global se mantienen sin sucursal y no aparecen al filtrar el
historial de una sede concreta.
