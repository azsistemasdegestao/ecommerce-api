namespace Ecommerce.API.Extensions;

public static class SecurityExtensions
{
    public const string CorsPolicyName = "default";

    // No origins configured -> CORS policy allows none, same-origin/non-browser clients (Scalar, server-to-server) are unaffected.
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");

            // Scalar's dev-only UI bootstraps via an inline <script type="module">
            // and injects inline <style> tags; relax script-src/style-src for that path only.
            var csp = context.Request.Path.StartsWithSegments("/scalar")
                ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'"
                : "default-src 'self'";
            context.Response.Headers.Append("Content-Security-Policy", csp);

            await next();
        });
}
