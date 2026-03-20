using FluentValidation;
using Witness.Application.Commands;

namespace Witness.Application.Validators;

public sealed class RecordInteractionValidator : AbstractValidator<RecordInteractionCommand>
{
    public RecordInteractionValidator()
    {
        RuleFor(x => x.Target)
            .NotEmpty().WithMessage("Target URL is required")
            .Must(BeValidUrl).WithMessage("Target must be a valid URL");

        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("HTTP method is required")
            .Must(BeValidHttpMethod).WithMessage("Invalid HTTP method");

        RuleFor(x => x.Path)
            .NotEmpty().WithMessage("Path is required");

        RuleFor(x => x.Options!.TimeoutMs)
            .GreaterThan(0).When(x => x.Options?.TimeoutMs.HasValue == true)
            .WithMessage("Timeout must be greater than 0");
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeValidHttpMethod(string method)
    {
        var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
        return validMethods.Contains(method.ToUpperInvariant());
    }
}
