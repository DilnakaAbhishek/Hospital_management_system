namespace HospitalManagementSystem.Api.Services;

public static class SmsServiceRegistration
{
    public static IServiceCollection AddSmsNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AppointmentSmsNotifier>();
        var provider = configuration["Sms:Provider"] ?? "Mock";
        if (provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<ISmsService, MockSmsService>();
        else if (provider.Equals("Infobip", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<ISmsService, InfobipSmsService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
                .RedactLoggedHeaders(["Authorization"]);
        else
            throw new InvalidOperationException("Sms:Provider must be Mock or Infobip.");
        return services;
    }
}
