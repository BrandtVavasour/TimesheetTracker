using System.Runtime.CompilerServices;

public static class VerifyGlobalSettings
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyEntityFramework.Initialize();
    }
}
