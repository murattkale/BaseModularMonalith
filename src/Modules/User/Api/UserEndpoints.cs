using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using User.Application.Commands;
using User.Application.Queries;

namespace User.Api;

/// <summary>
/// User endpoints - high-performance minimal APIs.
/// Tam CRUD + Login/Register desteği.
/// </summary>
public static class UserEndpoints
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        // ==========================================
        // PUBLIC ENDPOINTS (No Auth Required)
        // ==========================================
        
        group.MapPost("/register", Register)
            .WithName("RegisterUser")
            .WithSummary("Yeni kullanıcı kaydı")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapPost("/login", Login)
            .WithName("LoginUser")
            .WithSummary("Kullanıcı girişi")
            .Produces<LoginResultDto>()
            .Produces<ApiError>(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();

        // ==========================================
        // PROTECTED ENDPOINTS (Auth Required)
        // ==========================================
        
        // LIST - GET /api/users
        group.MapGet("/", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Kullanıcı listesi")
            .Produces<PagedResult<UserListItemDto>>()
            .AllowAnonymous();

        // GET BY ID - GET /api/users/{id}
        group.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Kullanıcı detayı")
            .Produces<UserDto>()
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        // GET CURRENT - GET /api/users/me
        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Mevcut kullanıcı bilgileri")
            .Produces<UserDto>()
            .RequireAuthorization();

        // UPDATE - PUT /api/users/{id}
        group.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Kullanıcı profil güncelleme")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        // DELETE - DELETE /api/users/{id}
        group.MapDelete("/{id:guid}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Kullanıcı silme (soft delete)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .RequireAuthorization("Admin");

        // CHANGE PASSWORD - POST /api/users/change-password
        group.MapPost("/change-password", ChangePassword)
            .WithName("ChangePassword")
            .WithSummary("Şifre değiştirme")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return app;
    }

    // ==========================================
    // HANDLER METHODS
    // ==========================================

    private static async Task<IResult> Register(
        ISender sender,
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateUserCommand(
            request.RequestId == Guid.Empty ? Guid.CreateVersion7() : request.RequestId,
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/users/{result.Value}", new RegisterResponse(result.Value))
            : Results.BadRequest(new ApiError(result.Error.Code, result.Error.Message));
    }

    private static async Task<IResult> Login(
        ISender sender,
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "User.InvalidCredentials" => Results.Json(
                    new ApiError(result.Error.Code, result.Error.Message),
                    statusCode: StatusCodes.Status401Unauthorized),
                "User.Deactivated" => Results.Json(
                    new ApiError(result.Error.Code, result.Error.Message),
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new ApiError(result.Error.Code, result.Error.Message))
            };
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetUsers(
        ISender sender,
        int page = 1,
        int pageSize = DefaultPageSize,
        bool? isActive = null,
        DateTime? afterCreatedAt = null,
        Guid? afterId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = new GetUsersQuery(page, pageSize, isActive, afterCreatedAt, afterId);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new ApiError(result.Error.Code, result.Error.Message));
    }

    private static async Task<IResult> GetUserById(
        ISender sender,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new ApiError(result.Error.Code, result.Error.Message));
    }

    private static async Task<IResult> GetCurrentUser(
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = context.User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetUserByIdQuery(userId);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new ApiError(result.Error.Code, result.Error.Message));
    }

    private static async Task<IResult> UpdateUser(
        ISender sender,
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateUserCommand(id, request.FirstName, request.LastName);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == "User.NotFound"
                ? Results.NotFound(new ApiError(result.Error.Code, result.Error.Message))
                : Results.BadRequest(new ApiError(result.Error.Code, result.Error.Message));
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteUser(
        ISender sender,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteUserCommand(id);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : Results.NotFound(new ApiError(result.Error.Code, result.Error.Message));
    }

    private static async Task<IResult> ChangePassword(
        ISender sender,
        HttpContext context,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = context.User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == "User.InvalidCredentials"
                ? Results.BadRequest(new ApiError("InvalidPassword", "Mevcut şifre yanlış."))
                : Results.BadRequest(new ApiError(result.Error.Code, result.Error.Message));
        }

        return Results.NoContent();
    }
}

// ==========================================
// REQUEST/RESPONSE DTOs
// ==========================================

/// <summary>
/// Kayıt isteği.
/// </summary>
public sealed record RegisterRequest(
    Guid RequestId,
    string Email, 
    string Password, 
    string FirstName, 
    string LastName);

/// <summary>
/// Giriş isteği.
/// </summary>
public sealed record LoginRequest(
    string Email, 
    string Password);

/// <summary>
/// Profil güncelleme isteği.
/// </summary>
public sealed record UpdateUserRequest(
    string FirstName, 
    string LastName);

/// <summary>
/// Şifre değiştirme isteği.
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword, 
    string NewPassword);

/// <summary>
/// Kayıt yanıtı.
/// </summary>
public sealed record RegisterResponse(Guid UserId);
