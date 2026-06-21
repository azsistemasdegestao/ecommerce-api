using System.Diagnostics;

namespace Ecommerce.Application.Common.Observability;

public static class ApplicationActivitySource
{
    public const string Name = "Ecommerce.Application";

    public static readonly ActivitySource Instance = new(Name);
}
