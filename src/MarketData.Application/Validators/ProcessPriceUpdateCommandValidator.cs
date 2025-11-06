using FluentValidation;
using MarketData.Application.Commands;

namespace MarketData.Application.Validators;

/// <summary>
/// Validator for ProcessPriceUpdateCommand
/// Ensures data integrity before processing
/// </summary>
public class ProcessPriceUpdateCommandValidator : AbstractValidator<ProcessPriceUpdateCommand>
{
    public ProcessPriceUpdateCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required")
            .MaximumLength(10)
            .WithMessage("Symbol must not exceed 10 characters")
            .Matches("^[A-Z]+$")
            .WithMessage("Symbol must contain only uppercase letters");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero")
            .LessThan(1_000_000)
            .WithMessage("Price seems unrealistic");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be in the future");
    }
}
