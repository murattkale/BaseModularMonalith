using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;
using User.Domain.Entities;

namespace User.Application.Commands;

/// <summary>
/// CreateUserCommand handler.
/// </summary>
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _repository;
    private readonly IUserReadService _readService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserMetrics _metrics;

    /// <summary>
    /// CreateUserCommandHandler sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="repository">Kullanıcı veri erişim katmanı.</param>
    /// <param name="readService">Hızlı okuma işlemleri için servis.</param>
    /// <param name="unitOfWork">İş birimi (transaction) yönetimi.</param>
    /// <param name="metrics">Performans metriklerini toplar.</param>
    public CreateUserCommandHandler(
        IUserRepository repository,
        IUserReadService readService,
        IUnitOfWork unitOfWork,
        IUserMetrics metrics)
    {
        _repository = repository;
        _readService = readService;
        _unitOfWork = unitOfWork;
        _metrics = metrics;
    }

    /// <summary>
    /// Kullanıcı oluşturma işlemini yürütür.
    /// </summary>
    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Email benzersizlik kontrolü (Dapper - hızlı)
        if (await _readService.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists);
        }

        // Entity oluştur
        var user = await UserEntity.CreateAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        await _repository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _metrics.UserCreated();

        return Result.Success(user.Id);
    }
}

/// <summary>
/// User hata kodları.
/// </summary>
public static class UserErrors
{
    public static readonly Error EmailAlreadyExists = new("User.EmailAlreadyExists", "Bu email adresi zaten kullanımda.");
    public static readonly Error NotFound = new("User.NotFound", "Kullanıcı bulunamadı.");
    public static readonly Error InvalidCredentials = new("User.InvalidCredentials", "Geçersiz email veya şifre.");
    public static readonly Error Deactivated = new("User.Deactivated", "Hesap deaktif edilmiş.");
}
