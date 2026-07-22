using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Application.Persistence;
using ParcelTrack.NotificationService.Application.Persistence.Repositories;
using ParcelTrack.NotificationService.Application.Services;

namespace ParcelTrack.NotificationService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("NotificationDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:NotificationDb is not configured"),
                npgsql => npgsql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName)));

        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));

        // Concrete senders...
        services.AddScoped<ConsoleNotificationSender>();
        services.AddScoped<SmtpNotificationSender>();
        services.AddScoped<SmsNotificationSender>();

        // ...exposed through a single channel-aware sender that routes by Notification.Channel.
        services.AddScoped<INotificationSender>(sp =>
        {
            var smtpHost = configuration["Notification:Smtp:Host"];
            INotificationSender emailSender = !string.IsNullOrEmpty(smtpHost)
                ? sp.GetRequiredService<SmtpNotificationSender>()
                : sp.GetRequiredService<ConsoleNotificationSender>();

            return new ChannelNotificationSender(emailSender, sp.GetRequiredService<SmsNotificationSender>());
        });

        return services;
    }
}
