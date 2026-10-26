using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace MoonMahjong.Plugin;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "itsrevela.moonsweepers.mahjong";
    public const string Name = "Moon Mahjong";
    public const string Version = "0.1.5.3";

    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static MahjongConfig Settings { get; private set; } = null!;

    public override void Load()
    {
        Logger = Log;
        Settings = new MahjongConfig(Config);
        ClassInjector.RegisterTypeInIl2Cpp<MahjongTable>();
        ClassInjector.RegisterTypeInIl2Cpp<DevLoop>();
        ClassInjector.RegisterTypeInIl2Cpp<CrewHub>();
        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        AddComponent<DevLoop>();
        AddComponent<CrewHub>();
        Logger.LogInfo($"{Name} {Version} loaded");
    }
}
