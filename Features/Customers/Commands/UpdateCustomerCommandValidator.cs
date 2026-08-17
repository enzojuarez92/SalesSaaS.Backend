using FluentValidation;

namespace SalesSaaS.Features.Customers.Commands;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.DocumentType).NotEmpty().MaximumLength(20);
        RuleFor(command => command.DocumentNumber).NotEmpty().MaximumLength(15).Matches(@"^[0-9\-]+$");
        RuleFor(command => command.TaxCondition).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
