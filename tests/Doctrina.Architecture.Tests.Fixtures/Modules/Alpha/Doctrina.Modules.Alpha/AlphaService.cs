using Doctrina.Modules.Beta;

namespace Doctrina.Modules.Alpha;

// DELIBERATELY WRONG: uses the implementation of module Beta.
public static class AlphaService
{
    public static string Run() => BetaService.Create("alpha").Name;
}
