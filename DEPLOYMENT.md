# Despliegue de SalesSaaS

Esta guía separa secretos de configuración pública y define una ruta de despliegue de bajo costo. Nunca subas credenciales, certificados AFIP ni claves privadas al repositorio.

## Arquitectura recomendada

Para iniciar con bajo costo, desplegá el frontend estático en **Cloudflare Pages** o **Netlify** y la API + SQL Server en un VPS con Docker Compose. Un VPS de 2 vCPU y 4 GB de RAM es el mínimo práctico para API y SQL Server; para mayor disponibilidad, usá una base de datos administrada y al menos dos réplicas de la API detrás de un proxy HTTPS.

- Frontend: Cloudflare Pages o Netlify, con CDN, HTTPS y fallback SPA.
- API: VPS con Docker Compose, Caddy o Nginx como reverse proxy HTTPS.
- Base de datos: SQL Server con volumen persistente, backups diarios verificados y almacenamiento fuera del VPS.

## Variables de entorno

### Backend

| Variable | Obligatoria | Uso |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Sí | Activa el perfil productivo. |
| `ConnectionStrings__DefaultConnection` | Sí | Cadena SQL Server productiva, con cifrado y contraseña robusta. |
| `Jwt__Key` | Sí | Secreto aleatorio de al menos 32 bytes. |
| `Jwt__Issuer` | Sí | Emisor de los JWT. |
| `Jwt__Audience` | Sí | Audiencia de los JWT. |
| `MercadoPago__AccessToken` | Producción | Token privado de Mercado Pago. |
| `MercadoPago__WebhookSecret` | Producción | Secreto usado para validar `x-signature`. |
| `Afip__Environment` | Sí | `Homologation` antes de pasar a `Production`. |
| `Afip__CertificatePath` / `Afip__PrivateKeyPath` | AFIP real | Rutas montadas como secretos, nunca dentro de la imagen. |
| `DataProtection__...` | Recomendado | Persistí las claves de Data Protection para conservar sesiones y datos cifrados después de reiniciar. |
| `Database__MigrateOnStartup` | Solo despliegue controlado | `true` únicamente durante una ventana de migración. |

### Frontend

| Variable de build | Obligatoria | Uso |
| --- | --- | --- |
| `VITE_API_BASE_URL` | Sí | URL HTTPS pública de la API, por ejemplo `https://api.tudominio.com/api`. |

`VITE_*` queda embebida en el bundle y no debe contener secretos.

## Despliegue del frontend

### Cloudflare Pages / Netlify

1. Importá el repositorio `SalesSaaS.Frontend`.
2. Indicá `npm ci && npm run build` como comando de build y `dist` como directorio publicado.
3. Configurá `VITE_API_BASE_URL` con la URL pública HTTPS de la API.
4. Configurá fallback SPA para que cualquier ruta responda `index.html`.
5. Restringí CORS en la API al dominio final del frontend antes de abrir el servicio.

Para una publicación Docker local:

```powershell
docker build -t salessaas-frontend:latest C:\Users\Enzo\source\repos\SalesSaaS.Frontend
docker run --rm -p 8080:80 -e VITE_API_BASE_URL=https://api.tudominio.com/api salessaas-frontend:latest
```

> `VITE_API_BASE_URL` se aplica durante el build; para Docker definila mediante `--build-arg` si el Dockerfile lo requiere.

## Despliegue de API y base de datos

1. Copiá el repositorio backend y un `.env` fuera del control de versiones al VPS.
2. Guardá certificados AFIP y claves en un directorio con permisos restringidos y montalo como volumen de solo lectura.
3. Creá y verificá un backup antes de migrar.
4. Construí y levantá los servicios:

```bash
docker compose build
docker compose up -d sqlserver
# Esperar que /health de SQL Server esté listo
docker compose run --rm api dotnet ef database update
docker compose up -d api frontend
docker compose ps
```

Si la imagen de producción no incluye `dotnet-ef`, aplicá la migración desde un job temporal con SDK o habilitá `Database__MigrateOnStartup=true` solo durante el despliegue y volvelo a `false` al terminar.

Validá:

```bash
curl -fsS https://api.tudominio.com/health
curl -fsS https://api.tudominio.com/health/ready
```

## Lista de salida a producción

- Ejecutar migraciones sobre una copia restaurada de producción.
- Cambiar JWT, Mercado Pago, AFIP y credenciales SQL por secretos reales.
- Configurar backups automáticos diarios, prueba mensual de restauración y retención definida.
- Mantener AFIP en homologación hasta aprobar comprobantes A/B/C y notas de crédito reales.
- Configurar HTTPS, CORS restrictivo, monitoreo de `/health/ready`, alertas y logs centralizados.
- Verificar webhook de Mercado Pago con el secreto real y una notificación repetida.
- Correr los workflows de CI en verde antes de cada release.
