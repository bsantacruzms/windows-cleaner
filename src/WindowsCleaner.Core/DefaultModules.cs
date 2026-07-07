using WindowsCleaner.Core.Abstractions;
using WindowsCleaner.Core.Modules.Disk;
using WindowsCleaner.Core.Modules.Drivers;
using WindowsCleaner.Core.Modules.Privacy;
using WindowsCleaner.Core.Modules.Startup;
using WindowsCleaner.Core.Modules.StoreAppx;
using WindowsCleaner.Core.Modules.SystemIntegrity;
using WindowsCleaner.Core.Modules.TempCleanup;
using WindowsCleaner.Core.Modules.WindowsUpdate;
using WindowsCleaner.Core.Safety;

namespace WindowsCleaner.Core;

/// <summary>Convenience factory that wires up all built-in health modules.</summary>
public static class DefaultModules
{
    public static IReadOnlyList<IHealthModule> CreateAll(ISafetyService? safety = null)
    {
        safety ??= new SafetyService();

        return new IHealthModule[]
        {
            new StoreAppxModule(safety),
            new TempCleanupModule(),
            new WindowsUpdateResetModule(safety),
            new SystemIntegrityModule(),
            new StartupModule(safety),
            new PrivacyModule(safety),
            new DriversModule(),
            new DiskHealthModule()
        };
    }
}
