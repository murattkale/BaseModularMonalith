using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;
using User.Application.Queries;

namespace User.Application.Commands;

/// <summary>
/// Giriş yapma isteği (Login Command).
/// </summary>
/// <param name="Email">Kullanıcının e-posta adresi.</param>
/// <param name="Password">Kullanıcının şifresi.</param>
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResultDto>;

/// <summary>
/// Login command handler.
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResultDto>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IUserMetrics _metrics;

    /// <summary>
    /// LoginCommandHandler sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="repository">Kullanıcı veri erişim katmanı.</param>
    /// <param name="unitOfWork">İş birimi (transaction) yönetimi.</param>
    /// <param name="tokenGenerator">JWT token oluşturma servisi.</param>
    /// <param name="metrics">Performans metriklerini toplar.</param>
    public LoginCommandHandler(
        IUserRepository repository,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator tokenGenerator,
        IUserMetrics metrics)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _metrics = metrics;
    }

    /// <summary>
    /// Giriş işlemini yürütür ve JWT token döner.
    /// </summary>
    public async Task<Result<LoginResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Email ile bul (EF Core - tracking gerekli çünkü update yapacağız)
        var user = await _repository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _metrics.LoginFailure();
            return Result.Failure<LoginResultDto>(UserErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            _metrics.LoginFailure();
            return Result.Failure<LoginResultDto>(UserErrors.Deactivated);
        }

        // Password doğrula (async offloading)
        if (!await user.VerifyPasswordAsync(request.Password, cancellationToken))
        {
            _metrics.LoginFailure();
            return Result.Failure<LoginResultDto>(UserErrors.InvalidCredentials);
        }

        // Login kaydet
        user.RecordLogin();
        _repository.Update(user);
        // SaveChanges TransactionBehavior tarafından yönetilir

        _metrics.LoginSuccess();

        // JWT token oluştur
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email.Value, user.GetFullName(), (long)user.Roles);

        return Result.Success(new LoginResultDto(
            user.Id,
            user.Email.Value,
            user.GetFullName(),
            token));
    }
}

/// <summary>
/// JWT token üretimi için servis arayüzü.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Belirtilen kullanıcı bilgileri ile bir JWT token oluşturur.
    /// </summary>
    string GenerateToken(Guid userId, string email, string fullName, long roles);
}
