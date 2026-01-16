using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;

namespace User.Application.Commands;

/// <summary>
/// ChangePasswordCommand handler.
/// </summary>
public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(IUserRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Mevcut şifreyi doğrula
        if (!user.VerifyPassword(request.CurrentPassword.AsSpan()))
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        // Yeni şifreyi ayarla
        user.ChangePassword(request.NewPassword.AsSpan());
        _repository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
