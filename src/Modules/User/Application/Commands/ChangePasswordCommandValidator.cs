using FluentValidation;

namespace User.Application.Commands;

/// <summary>
/// ChangePasswordCommand validator.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Kullanıcı ID zorunludur.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut şifre zorunludur.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalı.")
            .MaximumLength(128).WithMessage("Şifre 128 karakterden uzun olamaz.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermeli.")
            .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermeli.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermeli.");
    }
}

/// <summary>
/// LoginCommand validator.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email zorunludur.")
            .EmailAddress().WithMessage("Geçersiz email formatı.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.");
    }
}
