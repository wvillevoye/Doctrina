using Doctrina.Modules.Beta;

namespace Doctrina.Modules.Alpha;

// OPZETTELIJK FOUT: gebruikt de implementatie van module Beta.
public static class AlphaService
{
    public static string Run() => BetaService.Create("alpha").Name;
}
