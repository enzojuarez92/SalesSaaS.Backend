# Auditoría SalesSaaS — 10/09/2026

No equivale a una certificación fiscal o de seguridad comercial. SQL Server es el proveedor implementado; PostgreSQL no está implementado.

## Cambios implementados

| Área | Resultado |
| --- | --- |
| Auth | Validación de JWT, membresía y usuario activo en cada petición; versión de token revocable. Se corrigió el bucle de las rutas públicas autenticadas. |
| Perfil | GET/PUT /api/profile, contraseña actual obligatoria y revocación de sesiones de todos los tenants al cambiar datos. Pantalla Mi perfil. |
| Sucursal | X-Warehouse-Id verificado contra tenant y depósito activo. Los usuarios Seller/Warehouse se limitan además a sus asignaciones `UserWarehouse`; Owner/Admin mantienen acceso total. Body/query no pueden cambiar ese alcance. |
| POS | Caja abierta obligatoria, existencias locales, RequestId para reintentos sin duplicar ventas. Fallar la facturación no vuelve a cobrar una venta ya guardada. |
| Stock | Catálogo global y existencias por movimientos. Editar la ficha no altera stock. Kardex con anterior/resultante y responsable de movimientos nuevos; transferencias desde el historial. |
| Cuentas | El fiado crea el débito al vender; facturar no duplica deuda. Pagos parciales y crédito global con saldo visible por sucursal. |
| Excel | Productos, inventario, cuentas y ventas. Plantilla e importación atómica de hasta 1000 filas/5 MB, validación y errores por fila. |
| Compras | Orden multilínea, recepción y factura de proveedor. Alta/listado de proveedores y marcas desde la misma pantalla. |
| Presupuestos | Alta desde POS y consulta global del tenant. No cobran ni reservan stock. |
| UX | Moneda argentina centralizada, notificaciones, estados de carga y menú por roles. |
| Suscripción | Simulación solo en Development. Se valida `x-signature` mediante HMAC SHA-256 antes de consultar Mercado Pago y los eventos verificados son idempotentes. El checkout pendiente no desplaza un plan vigente. Crear planes globales requiere un claim de plataforma, no el rol Owner de un negocio. |
| PDF | QR PNG con QRCoder cuando hay CAE; documentos sin autorización claramente identificados. Numeración fiscal del comprobante autorizado. |
| Docker | Respaldo SQL previo y claves Data Protection preservadas en salessaas_api_keys. |

## Evidencia

- Backend Release: cero errores/advertencias de compilación; EF sin cambios de modelo pendientes.
- scripts/Test-OperationalIsolation.ps1: 52 verificaciones con API real y SQL Server separado. Acceso cruzado, caja cerrada, stock, Kardex, reintentos, facturas repetidas, Excel, fiado/pagos y perfil.
- Frontend: 5 tests unitarios y 8 Playwright, incluido POS móvil y regresión del bucle de navegación. Playwright simula respuestas HTTP; no reemplaza los tests SQL.
- No se probaron pagos bancarios reales, AFIP real, toda combinación de concurrencia ni carga sostenida.

## Pendientes para cobertura comercial universal

1. **Fiscal:** la matriz A/B/C, alícuotas 0/10,5/21 y notas de crédito asociadas están implementadas. Siguen pendientes percepciones, exportación, Factura de Crédito MiPyME y validación formal con un asesor fiscal por rubro.
2. **Homologación:** probar A/B/C, rechazos y reintentos inciertos con credenciales ARCA. Revisar el contrato WSFE vigente, incluyendo condición IVA del receptor. Validar representación impresa y escaneo QR con comprobantes autorizados.
3. **Datos históricos:** conciliar Product.Stock contra movimientos y CurrentBalance contra asientos. No asignar históricos arbitrariamente a sucursales ni sumar ajustes que dupliquen stock. Determinar su origen físico/contable primero.
4. **Mercado Pago real:** ejecutar Sandbox con la clave secreta de webhook, URLs HTTPS definitivas y callbacks repetidos. La firma entrante, importe/moneda e idempotencia ya se validan; la caja registra devoluciones contables, pero no devuelve dinero automáticamente a tarjetas/bancos.
5. **Operación:** HTTPS/dominio, permisos mínimos SQL, rotación de secretos, backup externo y ensayo de restauración, monitorización/carga. Confirmar licencias/elegibilidad de QuestPDF Community y MediatR antes de explotación comercial.

## Cobertura de UI / extensiones

| API o entidad | Situación |
| --- | --- |
| CashRegistersController, SalesController legacy, TenantMembersController | Alias previos conservados; Caja, POS y Configuración cubren las operaciones. |
| StockMovementsController | Ajustes y transferencias desde Productos/Kardex. |
| Quote | Alta/consulta presentes; conversión directa a venta y seguimiento comercial pendientes. |
| Brand / Supplier | Alta/consulta expuestas; no existían endpoints de edición/baja. |
| SaaSInvoice | Persistencia interna; falta historial de facturas SaaS para cliente/operador. |
| Webhooks, health, refresh y planes globales | Contratos técnicos sin pantalla operativa. Ningún JWT de tenant recibe platform_admin. |

Referencias: [comprobantes régimen general](https://www.arca.gob.ar/facturacion/regimen-general/comprobantes.asp), [monotributo](https://www.arca.gob.ar/facturacion/monotributo/comprobantes.asp), [especificación QR](https://arca.gob.ar/fe/qr/documentos/QRespecificaciones.pdf).
