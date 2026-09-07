# SalesSaaS - Hoja de ruta profesional

## Punto de partida

La API usa .NET 10, EF Core, MediatR, FluentValidation y SQL Server. Los
recursos de negocio ya estan separados por vertical slices y se aplicaron
migraciones, respuestas ProblemDetails, secretos locales y concurrencia para
el stock.

## Fase 1 - Identidad y aislamiento de tenants

- Crear usuarios con email y password hasheada.
- Modelar membresias usuario-negocio y roles: Owner, Admin, Seller y Warehouse.
- Emitir JWT en login y derivar usuario, tenant y rol desde sus claims.
- Proteger endpoints con autorizacion por rol.
- Aplicar filtros globales por tenant en EF Core; el cliente deja de enviar un
  TenantId como fuente de verdad.

## Fase 2 - Dominio comercial

- Mantener la venta como agregado: Draft, Confirmed y Cancelled.
- Crear lineas de venta con precio congelado al confirmar.
- Registrar cada entrada, reserva, salida o devolucion en StockMovement.
- Calcular y validar stock disponible al confirmar; cancelar revierte mediante
  un movimiento, sin editar el historial.
- Preparar pagos, comprobantes y auditoria como fases posteriores.

## Fase 3 - Calidad y operacion

- Tests unitarios de validadores y handlers; integracion para ventas y
  concurrencia.
- Health checks, logs estructurados, trazabilidad y rate limiting.
- Pipeline CI: restore, build, test y validacion de migraciones.
- Despliegue con secretos por entorno, backups y migraciones controladas.

## Decisiones iniciales

Cada usuario pertenece inicialmente a un negocio activo por token. El modelo
de membresias permitira varios negocios sin redisenar la base. El rol Owner
administra la cuenta, Admin administra el negocio, Seller vende y Warehouse
administra catalogo e inventario. Las acciones mutables se auditaran.
