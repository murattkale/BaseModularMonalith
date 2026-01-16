using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;

namespace User.Application.Queries;

/// <summary>
/// ID ile user getirme query'si.
/// </summary>
public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;

/// <summary>
/// GetUserByIdQuery handler.
/// </summary>
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUserReadService _readService;

    public GetUserByIdQueryHandler(IUserReadService readService)
    {
        _readService = readService;
    }

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _readService.GetByIdAsync(request.Id, cancellationToken);

        return user.HasValue
            ? Result.Success(user.Value)
            : Result.Failure<UserDto>(Commands.UserErrors.NotFound);
    }
}
