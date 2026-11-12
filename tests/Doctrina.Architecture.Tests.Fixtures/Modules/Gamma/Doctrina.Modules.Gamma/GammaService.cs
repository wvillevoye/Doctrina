using Doctrina.Modules.Beta.Contracts;

namespace Doctrina.Modules.Gamma;

public static class GammaService
{
    public static string Describe(BetaCreated created) => created.Name;
}
