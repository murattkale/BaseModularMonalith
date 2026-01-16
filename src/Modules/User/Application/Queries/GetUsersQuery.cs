using BuildingBlocks.CQRS;
using SharedKernel;
using User.Application.Contracts;

namespace User.Application.Queries;

/// <summary>
/// Sayfalanmış user listesi query'si.
/// </summary>
public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    bool? IsActive = null,
    DateTime? AfterCreatedAt = null,
    Guid? AfterId = null) : IQuery<PagedResult<UserListItemDto>>;

/// <summary>
/// GetUsersQuery handler.
/// </summary>
public sealed class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, PagedResult<UserListItemDto>>
{
    private readonly IUserReadService _readService;

    public GetUsersQueryHandler(IUserReadService readService)
    {
        _readService = readService;
    }

    public async Task<Result<PagedResult<UserListItemDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        // Paralel çalıştır (performans)
        var usersTask = _readService.GetAllAsync(
            request.Page,
            request.PageSize,
            request.IsActive,
            request.AfterCreatedAt,
            request.AfterId,
            cancellationToken);

        var countTask = _readService.GetCountAsync(request.IsActive, cancellationToken);

        await Task.WhenAll(usersTask.AsTask(), countTask.AsTask());

        var result = new PagedResult<UserListItemDto>(
            await usersTask,
            await countTask,
            request.Page,
            request.PageSize);

        return Result.Success(result);
    }
}
