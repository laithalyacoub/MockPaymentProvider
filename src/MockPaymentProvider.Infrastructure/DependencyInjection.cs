using MockPaymentProvider.Infrastructure.Security;
using MockPaymentProvider.Application.Payments;
using MockPaymentProvider.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MockPaymentProvider.Infrastructure.Persistence;

namespace MockPaymentProvider.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProviderDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }
}