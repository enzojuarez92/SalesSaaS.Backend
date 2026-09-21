using System.Text.Encodings.Web;
using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Notifications;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private static readonly IReadOnlyDictionary<string, string> Templates = new Dictionary<string, string>
    {
        ["Welcome"] = "<h1>¡Bienvenido/a a KloverCloud, {{Name}}!</h1><p>Tu negocio <strong>{{TenantName}}</strong> ya está listo para comenzar.</p><p>Para dar tus primeros pasos podés cargar productos y stock, abrir la caja y configurar la facturación ARCA cuando la necesites.</p><p>Gracias por elegirnos.<br/><strong>Equipo KloverCloud</strong></p>",
        ["LowStock"] = "<h2>KloverCloud · Alerta de stock bajo</h2><p>El producto <strong>{{ProductName}}</strong> alcanzó el stock mínimo. Stock actual: {{CurrentStock}}.</p>",
        ["Invoice"] = "<h2>KloverCloud · Comprobante electrónico</h2><p>Adjuntamos el detalle de tu comprobante {{InvoiceNumber}}. CAE: {{Cae}}.</p>",
        ["AccountReminder"] = "<h2>KloverCloud · Recordatorio de saldo pendiente</h2><p>Hola {{CustomerName}}, registramos un saldo pendiente de {{Balance}}.</p>",
        ["SubscriptionExpiring"] = "<h2>KloverCloud · Tu suscripción está por vencer</h2><p>El plan {{PlanName}} vence el {{ExpirationDate}}. Regularizalo para evitar interrupciones.</p>"
    };

    public string Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        if (!Templates.TryGetValue(templateName, out var template)) throw new InvalidOperationException("La plantilla de correo solicitada no existe.");
        foreach (var (key, value) in values) template = template.Replace($"{{{{{key}}}}}", HtmlEncoder.Default.Encode(value), StringComparison.Ordinal);
        return template;
    }
}
