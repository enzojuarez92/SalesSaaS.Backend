namespace SalesSaaS.Application.Notifications;

public interface IEmailTemplateService
{
    string Render(string templateName, IReadOnlyDictionary<string, string> values);
}
