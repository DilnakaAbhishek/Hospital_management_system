namespace HospitalManagementSystem.Api.Services;

public interface ISmsService
{
    // True means accepted by the provider, not confirmed delivery to the handset.
    Task<bool> SendAsync(string recipient, string message, CancellationToken cancellationToken = default);
}
