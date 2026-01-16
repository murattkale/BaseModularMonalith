using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;

namespace User.Application.Commands;

/// <summary>
/// UpdateUserCommand handler.
/// </summary>
public sealed class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(IUserRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.UpdateProfile(request.FirstName, request.LastName);
        _repository.Update(user);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            return Result.Failure(new Error("User.ConcurrencyConflict", 
                "Kayıt başka bir kullanıcı tarafından değiştirilmiş."));
        }

        return Result.Success();
    }
}
