from pathlib import Path
from reportlab.lib.colors import HexColor
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf" / "SalesSaaS-Roadmap-Profesional.pdf"
OUTPUT.parent.mkdir(parents=True, exist_ok=True)

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name="CoverTitle", parent=styles["Title"], fontName="Helvetica-Bold", fontSize=24, leading=30, textColor=HexColor("#16324F"), alignment=TA_CENTER, spaceAfter=18))
styles.add(ParagraphStyle(name="CoverSub", parent=styles["Normal"], fontSize=12, leading=18, textColor=HexColor("#486581"), alignment=TA_CENTER))
styles.add(ParagraphStyle(name="Section", parent=styles["Heading2"], fontName="Helvetica-Bold", fontSize=15, leading=20, textColor=HexColor("#16324F"), spaceBefore=14, spaceAfter=8))
styles.add(ParagraphStyle(name="BodyCustom", parent=styles["BodyText"], fontSize=10.5, leading=15, textColor=HexColor("#243B53"), spaceAfter=7))
styles.add(ParagraphStyle(name="Small", parent=styles["BodyText"], fontSize=9, leading=12, textColor=HexColor("#486581")))

def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(HexColor("#D9E2EC"))
    canvas.line(2 * cm, 1.6 * cm, A4[0] - 2 * cm, 1.6 * cm)
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(HexColor("#627D98"))
    canvas.drawString(2 * cm, 1.1 * cm, "SalesSaaS - Documento de arquitectura evolutiva")
    canvas.drawRightString(A4[0] - 2 * cm, 1.1 * cm, f"Pagina {doc.page}")
    canvas.restoreState()

story = [Spacer(1, 4.5 * cm), Paragraph("SalesSaaS", styles["CoverTitle"]), Paragraph("Hoja de ruta para una plataforma de ventas SaaS profesional", styles["CoverSub"]), Spacer(1, 1.2 * cm), Paragraph("Version inicial - Agosto de 2026", styles["CoverSub"]), PageBreak()]

story += [Paragraph("1. Estado actual", styles["Section"]), Paragraph("El backend cuenta con una base limpia en .NET 10: vertical slices, CQRS con MediatR, validaciones centralizadas, SQL Server, migraciones de EF Core, ProblemDetails y concurrencia optimista de stock. La siguiente etapa debe convertir esa base tecnica en una plataforma segura y auditable.", styles["BodyCustom"]), Paragraph("2. Fase prioritaria: identidad, roles y tenant seguro", styles["Section"]), Paragraph("El TenantId no debe venir como fuente de verdad desde query string o body. El login emitira un JWT con el usuario, su negocio activo y sus roles. Los endpoints se protegeran con autorizacion y el DbContext aplicara filtros globales para que ninguna consulta olvide el aislamiento por tenant.", styles["BodyCustom"])]

roles = [["Rol", "Responsabilidad inicial"], ["Owner", "Cuenta, membresias y configuracion del negocio"], ["Admin", "Clientes, productos, ventas y reportes"], ["Seller", "Clientes y ventas"], ["Warehouse", "Catalogo e inventario"]]
table = Table(roles, colWidths=[3.1 * cm, 11.4 * cm])
table.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, 0), HexColor("#16324F")), ("TEXTCOLOR", (0, 0), (-1, 0), HexColor("#FFFFFF")), ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"), ("FONTSIZE", (0, 0), (-1, -1), 9), ("LEADING", (0, 0), (-1, -1), 12), ("BACKGROUND", (0, 1), (-1, -1), HexColor("#F0F4F8")), ("GRID", (0, 0), (-1, -1), 0.35, HexColor("#BCCCDC")), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("TOPPADDING", (0, 0), (-1, -1), 7), ("BOTTOMPADDING", (0, 0), (-1, -1), 7)]))
story += [Spacer(1, 6), table, PageBreak(), Paragraph("Modelo inicial: User, TenantMembership y Role. Una membresia relaciona a un usuario con un negocio; asi el sistema puede admitir varios negocios por usuario sin redisenar la seguridad.", styles["Small"])]

story += [Paragraph("3. Dominio comercial", styles["Section"]), Paragraph("La venta debe ser un agregado con ciclo de vida controlado: Draft, Confirmed y Cancelled. Al confirmarla, se fijan precio y costo historicos en cada linea. Al cancelarla, nunca se borra la operacion: se registra una reversa y se devuelve el inventario.", styles["BodyCustom"]), Paragraph("El inventario evolucionara a un libro mayor de movimientos. StockMovement registrara entrada, salida por venta, devolucion, ajuste y reserva. El campo Stock de Product puede seguir existiendo como saldo de lectura rapida, pero cada cambio tendra trazabilidad.", styles["BodyCustom"]), Paragraph("4. Orden de implementacion", styles["Section"])]

steps = [["1", "Usuarios, registro, login, JWT y hashing de passwords"], ["2", "Membresias, roles, policies y CurrentUser"], ["3", "Filtro global de tenant y migracion gradual de endpoints"], ["4", "Estados de venta, cancelacion y StockMovement"], ["5", "Tests de negocio, health checks, logs y CI"]]
steps_table = Table(steps, colWidths=[1 * cm, 13.5 * cm])
steps_table.setStyle(TableStyle([("BACKGROUND", (0, 0), (0, -1), HexColor("#2F80ED")), ("TEXTCOLOR", (0, 0), (0, -1), HexColor("#FFFFFF")), ("ALIGN", (0, 0), (0, -1), "CENTER"), ("FONTNAME", (0, 0), (0, -1), "Helvetica-Bold"), ("BACKGROUND", (1, 0), (1, -1), HexColor("#F0F4F8")), ("GRID", (0, 0), (-1, -1), 0.35, HexColor("#BCCCDC")), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("TOPPADDING", (0, 0), (-1, -1), 8), ("BOTTOMPADDING", (0, 0), (-1, -1), 8)]))
story += [steps_table, Paragraph("Principios: no confiar en datos de tenant enviados por el cliente, no borrar operaciones comerciales, mantener precios historicos, manejar concurrencia y testear las reglas antes de ampliar endpoints.", styles["BodyCustom"]), Paragraph("Este documento se actualizara al cerrar cada fase para conservar decisiones, cambios de modelo y endpoints relevantes.", styles["Small"])]

doc = BaseDocTemplate(str(OUTPUT), pagesize=A4, leftMargin=2 * cm, rightMargin=2 * cm, topMargin=2 * cm, bottomMargin=2.2 * cm, title="SalesSaaS - Hoja de ruta profesional", author="SalesSaaS")
frame = Frame(doc.leftMargin, doc.bottomMargin, doc.width, doc.height, id="content")
doc.addPageTemplates([PageTemplate(id="document", frames=[frame], onPage=footer)])
doc.build(story)
print(OUTPUT)
