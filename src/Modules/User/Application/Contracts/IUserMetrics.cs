namespace User.Application.Contracts;

/// <summary>
/// User modülü metrikleri arayüzü.
/// </summary>
public interface IUserMetrics
{
    void UserCreated();
    void LoginSuccess();
    void LoginFailure();
}
