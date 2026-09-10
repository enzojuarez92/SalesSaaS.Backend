# GAP analysis funcional

Revisión realizada entre `Domain/`, `Controllers/` y las vistas de
`SalesSaaS.Frontend/src/views`.

## Cubierto con interfaz

| Dominio / API | Interfaz |
| --- | --- |
| Autenticación y tenant | Landing, Login y Registro |
| Productos, categorías y movimientos manuales | Productos y Categorías |
| Ventas, comprobantes y AFIP | Ventas/POS y Facturación AFIP |
| Clientes y cuentas corrientes | Cuentas corrientes |
| Caja | Caja |
| Depósitos | Depósitos |
| Suscripciones | Suscripción |
| Dashboard, reportes y auditoría | Resumen y Reportes |
| Empresa, usuarios y certificado AFIP | Configuración |

## Backend existente sin pantalla dedicada

| API / entidad | Estado actual | Próximo paso |
| --- | --- | --- |
| `BrandsController` / `Brand` | No hay ABM visual | Agregar una pestaña Marcas junto a Categorías. |
| `SuppliersController` / `Supplier` | Sin ABM visual | Crear módulo Proveedores y vincularlo a Compras. |
| `PurchasesController`, `PurchaseOrder`, `PurchaseInvoice` | Sidebar muestra Compras, pero la vista es genérica | Implementar orden de compra, recepción y factura de proveedor. |
| `StockMovementsController` | Se usa desde el ajuste de producto, sin historial general | Incorporar kardex por producto y depósito. |
| `CashRegistersController` | La operación se resuelve desde `CashController` | Consolidar o retirar el controlador duplicado. |
| `SalesController` | POS usa `OrdersController` | Consolidar la API de ventas en una única ruta pública. |
| `Quote` / `QuoteItem` | Entidades sin controlador ni pantalla | Implementar presupuestos y conversión a venta. |
| `SaaSInvoice` | Persistencia interna, sin interfaz de facturas SaaS | Agregar historial de pagos para el dueño de plataforma. |
| `WeatherForecastController` | Plantilla de proyecto sin uso | Eliminarlo antes de producción. |

## Dependencias de modelo pendientes

- Productos y categorías son globales por tenant; el saldo disponible se
  calcula por depósito a partir de `StockMovement`.
- Ventas, caja, dashboard y exportación de ventas ya reciben `WarehouseId`.
- `CustomerAccountEntry` y `AuditLog` ya tienen `WarehouseId` opcional. Los
  nuevos movimientos operativos se asignan a la sucursal activa; los eventos
  globales y los registros históricos se conservan sin sucursal para preservar
  su significado original.
