# 🚀 SalesSaaS - Multi-Tenant ERP & Sales System

Un sistema de gestión de ventas y stock SaaS Multi-Tenant desarrollado con **.NET 8** aplicando principios de **Clean Architecture**, **CQRS** y patrones enterprise.

---

## 🏗️ Arquitectura y Patrones Aplicados

* **Clean Architecture / Vertical Slices:** Separación clara entre Dominio, Infraestructura y Aplicación.
* **CQRS con MediatR:** Desacoplamiento de comandos y consultas.
* **Pipeline Behaviors:** Validaciones transversales centralizadas con **FluentValidation**.
* **Persistence:** Entity Framework Core con **Fluent API** (`IEntityTypeConfiguration`) para un `DbContext` ultra limpio.
* **Database:** SQL Server con soporte Multi-Tenancy por aislamiento lógico (`TenantId`).

---

## 🛠️ Tecnologías Usadas

* **Framework:** .NET 8 / C#
* **ORM:** Entity Framework Core
* **Validación:** FluentValidation
* **Patrón de Mediacions:** MediatR

---

## 🚀 Cómo Ejecutar el Proyecto Localmente

1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/enzojuarez92/SalesSaaS.Backend.git