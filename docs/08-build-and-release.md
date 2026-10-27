# 8. Build, run and release

## 8.1 Requirements

| Need | Detail |
|---|---|
| **Visual Studio 2026 (version 18) or newer** | The project targets `net10.0-android`. **Visual Studio 2022 cannot build it**: it fails with `NETSDK1209` ("The current Visual Studio version does not support targeting .NET 10"), `NETSDK1139` and `NU1012`. |
| Workload | ".NET Multi-platform App UI development" (includes the Android SDK and the emulator tooling). |
| .NET SDK | .NET 10 (installed with VS 2026). |
| Android | An emulator image (the project has been run on a Pixel 7, API 36) or a phone with USB debugging. Minimum supported Android is 7.0 (API 24). |

If both Visual Studio 2022 and 2026 are installed, opening `PopBlocks.sln` by double-clicking may start 2022. Start Visual Studio 2026 first and open the solution from its recent list or via **File, Open**.

The engine and the tests only need the .NET 10 SDK and build from a command line:

```
dotnet test tests/PopBlocks.Tests
```

The MAUI app needs the Android workload and cannot be built on a machine without it.

## 8.2 Run on an emulator

1. Open `PopBlocks.sln` in Visual Studio 2026.
2. Set **PopBlocks** as the startup project (not `PopBlocks.Engine`).
3. Choose the configuration **Debug** and an Android emulator in the device dropdown (create one in *Android Device Manager* if needed).
4. Press **Run** (the green arrow). The first run starts the emulator, deploys, and launches.

Things that commonly happen:

- **"Build has been canceled" after a few minutes** while the emulator was still booting: wait until the emulator shows its home screen and press Run again.
- A **"System UI isn't responding"** dialog on a freshly booted emulator: tap *Wait*.
- The first launch of a debug build is slower than later ones.

## 8.3 Run on a phone (for example a Samsung S24)

1. On the phone: Settings, About phone, Software information, tap **Build number** seven times. Then Settings, Developer options, enable **USB debugging** (or Wireless debugging).
2. Connect it and accept "Allow USB debugging".
3. Choose the phone in the device dropdown and press Run.

Samsung phones may need the Samsung USB driver on Windows for the device to show up.

**A Debug build is not a standalone app.** It uses Fast Deployment (assemblies are pushed separately to the device) and carries the debugger, so it is only meant to be launched from Visual Studio. Do not give a Debug APK to anyone.

## 8.4 Make an APK to install on other phones

A standalone, installable APK needs a **Release** build:

1. Set the configuration to **Release**.
2. Make sure Fast Deployment is **not** used (it is a Debug feature: *Project properties, Android, Options*, untick "Use Fast Deployment" if it shows).
3. Build a publish: either right-click the project and use **Publish / Archive**, or from a command line in `src/PopBlocks`:
   ```
   dotnet publish -f net10.0-android -c Release
   ```
   The project is set to produce an `.apk` in Release (`AndroidPackageFormat`), not an `.aab`. Look under `bin/Release/net10.0-android/publish/` for the `.apk`.
4. **Increase `ApplicationVersion`** (an integer) in `PopBlocks.csproj` for every build that will be installed over an earlier one, and update `ApplicationDisplayVersion` (what the About page shows).
5. Check the permissions (below).
6. Copy the `.apk` to the other phone and open it. The phone has to allow installing from that source ("install unknown apps").

Signing: the steps above do not configure a release keystore. For sideloading to a few phones, the default signing is enough. If the game is ever published to a store, create a keystore, keep it **outside the repository** (the `.gitignore` excludes `*.keystore`, `*.jks` and similar), back it up somewhere safe, and never lose it: store updates must be signed with the same key forever. The `dotnet publish` command and output folder above have not been re-verified in this repository's history (the Debug run from Visual Studio has); confirm the exact output path on your machine, and run the permission check on whatever APK you produce.

## 8.5 The no-internet check

The product promise is that the game never touches the network. Release APKs must not request `INTERNET`. Run:

```
./scripts/Check-Permissions.ps1 -Apk path\to\the.apk
```

It locates `aapt2` in the Android SDK, dumps the APK's permissions and **fails (exit code 1)** if `INTERNET` is present. Debug builds get `INTERNET` added by the Android tooling so the debugger can attach, so only check Release builds. The manifest in the repository declares no permissions at all; if the check ever fails on a Release build, something (a new package, a manifest edit) added one, and that change must be reversed.

## 8.6 Versioning

- `ApplicationDisplayVersion` in `PopBlocks.csproj`: the version number shown to people.
- `ApplicationVersion` in `PopBlocks.csproj`: an integer that must go up with every installed build.
- The About page displays both, read from the installed package, so it can never disagree with the build.

Saves are versioned separately (`save_version`, see [Architecture](02-architecture.md#26-persistence)). An over-the-top install keeps every saved key; `allowBackup` is off, so a reinstall (uninstall, then install) starts empty unless the player restores a save code.

## 8.6a Developer menu

`DevMenu.Enabled` (`DevMenu.cs`) is true for Debug builds and for any build made with `-p:DevMenu=true` (which adds the `DEVMENU` constant); it is a `const`, so in a normal Release build the menu button and the pause-card shortcuts never show. To test the whole campaign without playing it, run a Release build with `-p:DevMenu=true` and use the menu to jump to a level, add gold, max the shop, fill the helpers or unlock every look. Never ship a build made that way.

## 8.7 Logs and debugging

Logs go to the device log under the tag `PopBlocks` and nowhere else:

```
adb logcat -s PopBlocks
```

`adb` is in the Android SDK's `platform-tools` folder (for example `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe`). Start-up breadcrumbs (`App ctor`, `App.CreateWindow`, `MainActivity.OnCreate`, `MainPage.OnAppearing`) and any unhandled exception appear there. If the app closes on start, the last breadcrumb before the error says how far it got.

## 8.8 Repository hygiene

- `.gitignore` excludes build output, IDE files, test results, packages, signing material, local config and OS files. Read its comments before adding an exception.
- Never commit a keystore, a password, or an API key. (The game has none of the last two; keep it that way.)
- Do not commit APKs. Share them through a release page.
- `_to_delete/` is an ignored parking folder for files that have been removed from the solution but not yet deleted.
