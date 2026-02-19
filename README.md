# Kept You Waiting, Huh? 🎵

> A lightweight RimWorld 1.6 mod that plays a custom sound effect when a pawn
> **joins your colony from outside the map**.

[![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-blue)](https://store.steampowered.com/app/294100/RimWorld/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## ✨ Features

| Event | Sound plays? | Configurable? |
|---|---|---|
| Wanderer joins | ✅ Yes (on by default) | Volume slider |
| Refugee / event join | ✅ Yes (on by default) | Volume slider |
| Prisoner recruited | ❌ Never | — |
| Slave emancipated | ❌ Never | — |
| Orbital / comms trader | ⚙️ Optional (off by default) | Toggle + volume |
| Walk-in trader caravan | ⚙️ Optional (off by default) | Toggle + volume |

All settings are in **Options → Mod Settings → Kept You Waiting, Huh?**

---

## 📥 Installation

### From Steam Workshop (recommended)
> *Coming soon — see [Publishing to Steam](#publishing-to-steam) below.*

### Manual installation
1. Download or clone this repository.
2. Place (or symlink) the folder into your RimWorld `Mods/` directory:
   ```
   <RimWorld install>/Mods/kept_you_waiting_huh/
   ```
3. Add your own sound file (see [Audio Setup](#audio-setup)).
4. Launch RimWorld and enable the mod.

---

## 🔊 Audio Setup

The mod ships with **no audio file** — you must provide your own `.ogg` file.

1. Encode your audio as **OGG Vorbis** (`.ogg`).  
   Recommended: mono or stereo, 44 100 Hz, 128–192 kbps, ≤ 3 seconds.
2. Rename it to `pawn_joined.ogg`.
3. Place it at:
   ```
   Sounds/JoinSound/pawn_joined.ogg
   ```

**Optional — separate trader sound:**  
Name the file `trader_arrived.ogg`, place it in the same folder, then edit
`Defs/SoundDefs/JoinSound_Sounds.xml` and change the trader `<clipPath>` from
`JoinSound/pawn_joined` to `JoinSound/trader_arrived`.

**Quick conversion with ffmpeg:**
```bash
ffmpeg -i your_sound.mp3 -c:a libvorbis -q:a 5 pawn_joined.ogg
```

---

## 🔨 Building from Source

### Prerequisites
| Tool | Version | Notes |
|---|---|---|
| Visual Studio 2022 | Any edition | Community is free |
| .NET Framework 4.7.2 SDK | Included with VS | Target framework |
| RimWorld | 1.6 | For DLL references |

### Steps

1. **Clone the repository:**
   ```powershell
   git clone https://github.com/YourName/kept_you_waiting_huh.git
   cd kept_you_waiting_huh
   ```

2. **Open the project in Visual Studio:**
   ```
   Source/JoinSoundMod/JoinSoundMod.csproj
   ```
   Or open it directly in Visual Studio via *File → Open → Project/Solution*.

3. **Set your RimWorld path** (one-time):  
   Edit `Source/JoinSoundMod/JoinSoundMod.csproj` and update:
   ```xml
   <RimWorldPath>C:\Program Files (x86)\Steam\steamapps\common\RimWorld</RimWorldPath>
   ```
   Or pass it on the command line (see below).

4. **Build (Release):**
   ```powershell
   # From the Source/JoinSoundMod/ directory:
   msbuild JoinSoundMod.csproj /p:Configuration=Release

   # Or override RimWorldPath inline:
   msbuild JoinSoundMod.csproj /p:Configuration=Release /p:RimWorldPath="D:\Games\RimWorld"
   ```
   The compiled `JoinSoundMod.dll` is automatically placed in:
   ```
   Assemblies/JoinSoundMod.dll
   ```

5. **Build via Visual Studio:**  
   Select **Release** configuration → *Build → Build Solution*.  
   The DLL is output to `Assemblies/` automatically (see `<OutputPath>` in the csproj).

> **Note:** Never copy `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, or
> `0Harmony.dll` into the `Assemblies/` folder.  These are RimWorld's files and
> must NOT be distributed with the mod.

---

## 🗂️ Project Structure

```
kept_you_waiting_huh/
├── About/
│   └── About.xml               ← Mod metadata (name, packageId, version)
├── Assemblies/
│   └── JoinSoundMod.dll        ← Compiled output (build it yourself)
├── Defs/
│   └── SoundDefs/
│       └── JoinSound_Sounds.xml ← SoundDef declarations
├── Sounds/
│   └── JoinSound/
│       ├── PLACEHOLDER.txt     ← Instructions for audio files
│       └── pawn_joined.ogg     ← YOUR audio file goes here (not included)
├── Source/
│   └── JoinSoundMod/
│       ├── JoinSoundMod.csproj ← Build project
│       ├── JoinSoundMod.cs     ← Mod entry point + settings UI
│       ├── JoinSoundPatch.cs   ← Harmony patches
│       └── JoinSoundSettings.cs ← Persistent settings
├── .gitignore
├── LICENSE
├── PublisherPlus.xml           ← Steam Workshop upload config
└── README.md
```

---

## ⚙️ Technical Details

### How the patch works

The mod hooks `Pawn.SetFaction` via **Harmony Postfix**.  
A `Prefix` captures the pawn's state before the call:

```csharp
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
// Prefix captures: wasPrisoner, wasSlave, hadPlayerFaction
// Postfix checks: new faction is player + was not prisoner/slave + game is playing
```

**Why `SetFaction` and not an incident worker?**  
`SetFaction` is the single authoritative location where *any* faction change
happens, regardless of which incident, mod, or internal system triggered it.
Patching individual incident workers would miss modded events.

**Prisoner/slave exclusion:**  
The `Prefix` checks `IsPrisoner` and `IsSlave` before the change.  
If either was `true`, the pawn was already on the map being recruited —
not arriving from outside — so the sound is suppressed.

**Game-state guard:**  
`Current.ProgramState != ProgramState.Playing` prevents the sound firing
during map load or the initial game setup when colonists receive their faction.

### Trader patches

Two additional patches target:
- `IncidentWorker_OrbitalTraderArrival.TryExecuteWorker` (comms traders)
- `IncidentWorker_TraderCaravanArrival.TryExecuteWorker` (walk-in caravans)

Both are **disabled by default** and controlled by mod settings.

---

## 🌐 Publishing to Steam Workshop

RimWorld uses Steam's built-in Workshop upload system via the game's Dev Mode.

### Prerequisites
- RimWorld must be installed via Steam (not GOG or DRM-free).
- You must be logged into Steam.
- The mod must be in your RimWorld `Mods/` folder.

### Steps

1. **Enable Dev Mode:**  
   `Options → Dev Mode` (tick the checkbox).

2. **Open the Mod Manager:**  
   Main Menu → *Mods* → find *Kept You Waiting, Huh?* → enable it.

3. **Upload via Dev Mode toolbar:**  
   In-game, press `` ` `` (tilde) or click *Dev Mode → Package and upload mod*.  
   Select this mod, fill in the description and tags, and click *Upload*.

4. **Steam will create a Workshop item.** Note the Workshop ID (a long number
   in the URL after the upload).

5. **Add preview image:**  
   On your Workshop item's Steam page, click *Edit* and add a preview image
   (`.jpg` or `.png`, ≤ 1 MB, at least 512×512).

### Using Publisher Plus (optional)

[Publisher Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=1574011459)
is a community mod that gives finer control over Workshop uploads.
A `PublisherPlus.xml` is included in this repo for convenience.

```
Options → Publisher Plus → Upload "Kept You Waiting, Huh?"
```

---

## 🤝 Contributing

Pull requests are welcome! If you'd like to contribute:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-thing`
3. Commit your changes with a clear message.
4. Push and open a Pull Request.

Please report bugs via [GitHub Issues](https://github.com/YourName/kept_you_waiting_huh/issues).

---

## 📜 License

[MIT License](LICENSE) — free to use, modify, and redistribute with attribution.

---

## 🙏 Credits

- **Harmony** by Andreas Pardeike ([@pardeike](https://github.com/pardeike)) — used for patching.
- Inspired by the original *Metal Gear Solid* codec call tone.  
  *"Kept you waiting, huh?"* — Solid Snake
