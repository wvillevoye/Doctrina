using Doctrina.Modules.Beta.Contracts;

namespace Doctrina.Modules.Beta;

public static class BetaService
{
    public static BetaCreated Create(string name) => new(name);
}
