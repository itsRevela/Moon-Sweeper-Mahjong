# Mod loader: BepInEx 6 on Unity 6000.6 / IL2CPP metadata v108

Moon Sweepers is Unity **6000.6.0f1**, IL2CPP, with `global-metadata.dat` **version 108**. As of 2026-10-25 no
released mod loader handles that. This project builds a working BepInEx 6 from three upstream clones plus two
small patches (kept in `patches/`). Verified 2026-10-25: interop generation completes, the chainloader starts,
a plugin loads, class injection and Harmony patches work.

## Why stock BepInEx fails, and what we changed

| Layer | Stock BepInEx BE #791 | Problem on this game | Our fix |
|---|---|---|---|
| Metadata reader | Cpp2IL.Core `development.1452` | accepts metadata 23–106 only | run Cpp2IL `development` **b5ad444** (reads v108; support landed in 01c17484, 2026-07-19) **out of process** |
| Interop generator | Il2CppInterop 1.5.3, built on AsmResolver 6.0.0-beta.5 | Cpp2IL ≥ v108 is built on AsmResolver 6.0.1; in one process the beta.5 code hits `TypeLoadException: ProbeAssemblyFilePath ... does not have an implementation` | keep everything in-process on beta.5; exchange Cpp2IL's dummy assemblies through files (`patches/bepinex-external-cpp2il.patch`) |
| Interop runtime | Il2CppInterop 1.5.3 struct handlers end at "2023.1" | Unity 6000.6 changed `Il2CppClass` (merged interface table, reordered pointers) and `Il2CppAssembly` (`moduleToken`); class injection fails with `Base class Il2CppSystem.Object is value type` | new `Class_108_0` / `Assembly_108_0` handlers and a merged-table builder in `ClassInjector` (`patches/il2cppinterop-unity-6000.6.patch`), on Il2CppInterop master 81a6f78 (which also has two Unity 6 fixes newer than 1.5.3) |

The 6000.6 struct layouts were read from the libil2cpp headers of the installed **6000.6.7f1** editor
(`Editor/Data/il2cpp/libil2cpp/il2cpp-class-internals.h`); its `GlobalMetadata.cpp` asserts metadata version 108,
the same as the game. Interface-table semantics follow `vm/Class.cpp` `SetupInterfacesLocked`.

Expected harmless warning in the log: `Class::Init signatures have been exhausted, using a substitute!`
(Il2CppInterop falls back to `il2cpp_class_has_references`, which calls `Class::Init`).

## Rebuild from scratch

Tools are clones under `tools/` (git-ignored). SDK: .NET 10. Il2CppInterop's `global.json` pins SDK 8, so build its
projects **from `tools/`**, not from inside the repo.

```bash
cd tools
# 1. Cpp2IL (metadata reader CLI, net472 so it runs on Windows' built-in .NET Framework)
git clone -b development https://github.com/SamboyCoding/Cpp2IL.git Cpp2IL && git -C Cpp2IL checkout b5ad444
dotnet build Cpp2IL/Cpp2IL/Cpp2IL.csproj -c Release -f net472

# 2. Il2CppInterop with the 6000.6 handlers, packed into a local feed
git clone https://github.com/BepInEx/Il2CppInterop.git Il2CppInterop && git -C Il2CppInterop checkout 81a6f78
git -C Il2CppInterop am ../../patches/il2cppinterop-unity-6000.6.patch
mkdir -p localfeed
for p in Il2CppInterop.Common Il2CppInterop.Runtime Il2CppInterop.Generator Il2CppInterop.HarmonySupport; do
  dotnet build Il2CppInterop/$p/$p.csproj -c Release -p:VersionPrefix=1.5.4 -p:VersionSuffix=moonsweepers.2
  dotnet pack  Il2CppInterop/$p/$p.csproj -c Release --no-build -p:VersionPrefix=1.5.4 -p:VersionSuffix=moonsweepers.2 -o localfeed
done

# 3. BepInEx with the external-Cpp2IL patch (references 1.5.4-moonsweepers.2 from ../localfeed)
git clone https://github.com/BepInEx/BepInEx.git BepInEx && git -C BepInEx checkout b782da4
git -C BepInEx am ../../patches/bepinex-external-cpp2il.patch
(cd BepInEx && dotnet run --project build/Build.csproj -c Release -- --target MakeDist)
cd ..
```

When changing the Il2CppInterop patch, bump `VersionSuffix` (and the three references in BepInEx's
`BepInEx.Unity.IL2CPP.csproj`); NuGet caches packages by version, so re-packing the same version is silently ignored.

## Install / uninstall

```bash
python -m scripts.loader stage --dist tools/BepInEx/bin/dist/Unity.IL2CPP-win-x64 \
    --cpp2il tools/Cpp2IL/Cpp2IL/bin/Release/net472 --out .run/loader-dist
python -m scripts.loader install --dist .run/loader-dist     # refuses to overwrite anything
python -m scripts.loader status
python -m scripts.loader uninstall [--purge]                 # --purge also removes generated interop/cache/logs
```

The game must not be running during install/uninstall (its files are locked). The first launch after install runs
Cpp2IL and the interop generator (about 30 s) before the game window appears. The log is
`<game>/BepInEx/LogOutput.log`; success ends with `Chainloader startup complete`.

## When the game updates

If Unity is upgraded again, check (1) the metadata version (`global-metadata.dat` bytes 4–7) against Cpp2IL's
supported range, and (2) the `Il2CppClass`/`Il2CppAssembly` layouts against the matching editor's libil2cpp headers.
A same-Unity-version game patch only needs the interop regenerated, which BepInEx does automatically (it hashes
`GameAssembly.dll`).
