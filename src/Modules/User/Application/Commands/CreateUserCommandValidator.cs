using FluentValidation;

namespace User.Application.Commands;

/// <summary>
/// CreateUserCommand validator.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email zorunludur.")
            .MaximumLength(254).WithMessage("Email 254 karakterden uzun olamaz.")
            .EmailAddress().WithMessage("Geçersiz email formatı.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalı.")
            .MaximumLength(128).WithMessage("Şifre 128 karakterden uzun olamaz.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermeli.")
            .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermeli.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermeli.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100).WithMessage("Ad 100 karakterden uzun olamaz.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100).WithMessage("Soyad 100 karakterden uzun olamaz.");
    }
}
