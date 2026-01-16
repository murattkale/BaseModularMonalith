using FluentValidation;

namespace User.Application.Commands;

/// <summary>
/// UpdateUserCommand validator.
/// </summary>
public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Kullanıcı ID zorunludur.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100).WithMessage("Ad 100 karakterden uzun olamaz.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100).WithMessage("Soyad 100 karakterden uzun olamaz.");
    }
}
