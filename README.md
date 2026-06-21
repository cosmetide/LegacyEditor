<div align="center">
  <img src="src/LegacyEditor/LegacyEditor.ico" width="64" alt="LegacyEditor">
  <h1>LegacyEditor</h1>
  <p>A WPF tool for editing Minecraft: Legacy Console Edition world files (<code>.ms</code> archives).<br>
  Designed for use with the <b>Minecraft Legacy Console Leak</b>.</p>
</div>

---

## Features

- **XUID Player Management** — view, search, sort, and delete players by XUID
- **Wipe Empty Players** — remove players with zero items, XP, and ender chest contents
- **Import XUID Lists** — bulk-remove players not in an imported list (text file or Authy DB)
- **Entity Wiping** — strip entities/tile entities from region files (`.mcr`/`.mca`) with whitelist/blacklist mode
- **MS Archive Support** — reads and writes decompressed/recompressed `.ms` archives
- **Dark Theme** — full dark UI throughout

## Download

Grab the latest build from [Releases](https://github.com/Cosmetide/LegacyEditor/releases).

## Usage

1. Launch LegacyEditor and drop a `.ms` world file onto the welcome window
2. Switch to the **XUID** tab to manage players
3. Switch to the **Entity** tab to configure entity removal
4. Click **Process World** to write the cleaned archive

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/Cosmetide/LegacyEditor.git
cd LegacyEditor
./publish.ps1
```

The version is read automatically from the `.csproj`. The output will be in `LegacyEditor-v{version}-Windows\`.
