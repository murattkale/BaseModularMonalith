using System.Diagnostics.Metrics;

namespace User.Infrastructure.Diagnostics;

/// <summary>
/// User modülü özel metrikler.
/// Sıfır allocation metrik takibi.
/// </summary>
public sealed class UserMetrics : User.Application.Contracts.IUserMetrics
{
    private readonly Counter<long> _userCreatedCounter;
    private readonly Counter<long> _loginSuccessCounter;
    private readonly Counter<long> _loginFailureCounter;

    public UserMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("User.Module");
        
        _userCreatedCounter = meter.CreateCounter<long>("user.created.count", description: "Toplam oluşturulan kullanıcı sayısı");
        _loginSuccessCounter = meter.CreateCounter<long>("user.login.success.count", description: "Başarılı giriş sayısı");
        _loginFailureCounter = meter.CreateCounter<long>("user.login.failure.count", description: "Hatalı giriş sayısı");
    }

    public void UserCreated() => _userCreatedCounter.Add(1);
    public void LoginSuccess() => _loginSuccessCounter.Add(1);
    public void LoginFailure() => _loginFailureCounter.Add(1);
}
