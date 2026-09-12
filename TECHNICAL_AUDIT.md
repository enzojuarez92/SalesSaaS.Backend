# Auditoría técnica final

Fecha: 12 de septiembre de 2026.

## Estado comprobado

- El modelo EF Core está sincronizado con la última migración `FinalizeFiscalWebhooksAndUserWarehouses` (`dotnet ef migrations has-pending-model-changes` no detectó cambios).
- Las dependencias explícitas incluyen ClosedXML, FluentValidation, MediatR, EF Core, QuestPDF, MailKit y los paquetes JWT requeridos. No se detectaron DTOs o clases marcadas como `TODO`, `FIXME` o `NotImplemented`.
- La API tiene autenticación JWT, `FallbackPolicy` autenticada, alcance de tenant/depósito, límites de tasa para autenticación y billing, conflictos de concurrencia de stock, middleware de suscripción y handler global que devuelve Problem Details sin stack traces al cliente.
- El frontend transmite `X-Warehouse-Id`, protege rutas y maneja errores API de forma centralizada.

## Bloqueantes a resolver antes del corte productivo

1. **Pruebas backend:** no existe aún un proyecto de tests .NET (`*.Tests`). Por eso el workflow ejecuta `dotnet test`, pero no cubre endpoints, autorización, AFIP ni webhooks. Agregar integración con `WebApplicationFactory` y una base SQL efímera es necesario antes de prometer cobertura de carga o regresión.
2. **Base local no disponible:** SQL Server no respondió durante la auditoría, por lo que no fue posible comprobar qué migraciones ya fueron aplicadas en la instancia local. Antes de desplegar, ejecutar `dotnet ef database update` sobre una base restaurable y verificar `dotnet ef migrations list`.
3. **Carga concurrente real:** existen protecciones de concurrencia y rate limiting, pero faltan pruebas de carga reproducibles y observabilidad centralizada. La arquitectura es apta para escalar horizontalmente la API cuando Data Protection, logs, caché y base se externalicen; no existe evidencia de benchmark todavía.
4. **Dependencias:** se deben activar Dependabot o un escaneo de vulnerabilidades en GitHub y definir una ventana mensual de actualización.

## Decisión de arquitectura

La separación Domain / Application / Infrastructure / Features / Controllers es coherente para el dominio actual. El siguiente refuerzo recomendado es extraer pruebas de integración y contratos de API, no agregar otra capa arquitectónica.
