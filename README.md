# Advanced K9 v0.20-beta for LSPDFR

AdvancedK9 v0.20-beta is a persistent LSPDFR/RAGE Plugin Hook police-dog partner with push-to-talk voice control, per-dog progression, health and a compact EUP-style LemonUI command/profile interface. All published builds are beta builds while gameplay and model compatibility continue to be tested.

AdvancedK9 runs as an RPH plugin and starts its controller directly. It avoids a reflection-based LSPDFR duty gate because RPH plugins run in isolated AppDomains. Instead, it follows LSPDFR's own duty-state messages from the active RAGEPluginHook log. GTA's native cop flag is diagnostic only because LSPDFR may set it while the player is still off duty. While off duty, the K9, HUD, menus, shortcuts and voice capture remain disabled. Going off duty closes the UI, stops voice capture and dismisses the active dog.

## Drag-and-drop installation

Use the compiled release ZIP, not the source ZIP. Close GTA V, then drag everything in the ZIP into the folder containing `GTA5.exe`; this installs `LemonUI.RagePluginHook.dll` in the GTA V root and merges the `Plugins` folder. In RAGE Plugin Hook settings, enable **Load all plugins on startup**. Start Story Mode, load LSPDFR, and go on duty.

The compiled release includes AdvancedK9, its configuration, and the MIT-licensed LemonUI RAGE Plugin Hook runtime. It does not redistribute NAudio support assemblies, Microsoft.Win32.Registry, System.Security assemblies, ScriptHookV, GTA V, RAGE Plugin Hook, LSPDFR, Policing Redefined, Common Data Framework, or third-party dog models.

## Features

- Deploy/dismiss and follow/heel
- Sit, lie down, pet, feed, physical rope leash with handler-follow movement, and dog-mounted camera
- Automatic operational leash release when search, tracking, fetch or apprehension is deployed
- Pedestrian and full-perimeter vehicle odor searches; silent sit means clear, sit plus repeated bark means positive
- Track a nearby suspect or missing person for up to two minutes
- Non-lethal apprehension with automatic recall, a configurable health floor and hands-up surrender
- Fetch minigame
- Dedicated off-street K9 training ground with five persistent gated levels; every level must reach 100% before the next unlocks
- Certification-specific OB, AGI, DET, TRK and APP courses with five scored exercises per level
- Independent optional narcotics (NAR), explosives/bomb (BOMB), and weapons/firearm (WPN) detection specialties, each with its own 0–100 progress and five-station academy setup
- Separate field and voice commands for narcotics, explosives and weapons searches; a dog may hold any combination or all specialty certifications
- Weapon-aim target identification before apprehension, with officer rejection and automatic non-lethal recall
- Push-to-talk voice recognition using OpenAI or Groq transcription; the microphone opens only while `V` is held
- Persistent command and profile menus that remain open until explicitly closed
- Runtime command registry shared by menu and voice recognition, with multiple natural verbal alternatives for every action
- Movable, scalable compact/expanded K9 status HUD
- Per-dog XP, certifications, health, injuries, stamina, trust and statistics
- Optional runtime bridge to Policing Redefined/Common Data Framework search records
- Persistent 0–100 handler trust with obedience, response-time, detection and deployment effects
- Handler-safety interlock: friendly relationship group, attack prevention and automatic emergency recall
- Persistent kennel profile for breed, coat variation, vest style/color and custom K9 name

## Build

Prerequisites: Visual Studio 2022 with **.NET Framework 4.8 Developer Pack**, LSPDFR 0.4.9+, RAGE Plugin Hook, and GTA V Story Mode.

From a Developer PowerShell:

```powershell
msbuild AdvancedK9.csproj /p:Configuration=Release /p:GtaVDir="D:\Games\Grand Theft Auto V"
```

The build copies the RAGE Plugin Hook entry-point assembly `AdvancedK9.dll` to `Plugins` and its configuration files to `Plugins\LSPDFR\AdvancedK9`. If `GtaVDir` is omitted, copy those files manually. Do not place the DLL in `Plugins\LSPDFR`; that folder is reserved for assemblies implementing the LSPDFR plugin API.

## Controls

- `Left Ctrl + K`: deploy/dismiss
- `Left Ctrl + J`: command menu, then `0`–`9`
- `Left Ctrl + C`: dog camera
- `Left Ctrl + L`: leash
- `Left Ctrl + U`: open the persistent K9 profile/kennel menu
- `Left Ctrl + J`: open the persistent command menu
- `V`: hold to open the microphone and speak a command

Voice phrases begin with the configured dog name or “K nine”: “Rex, sit”, “Rex, search”, “K nine, recall”, and so on. Commands without the wake name are ignored.
Wake-word variants `K9`, `K-9`, `K nine`, `kay nine` and `canine` are accepted. Natural phrases such as “K9 sit down,” “K9 fetch the ball,” and “K9 search the vehicle” are normalized before matching.
Specialty examples include “K9 search for narcotics,” “K9 bomb search,” “K9 weapons search,” “K9 narcotics training,” “K9 explosives training,” and “K9 weapons training.”

## Dog profile and appearance

Set any dog name in `AdvancedK9.ini` with `Name=Rex`. The AI prompt learns that name when the plugin starts, notifications use it, and voice commands require it as the wake word. Restart the plugin after renaming.

Open the kennel with `Left Ctrl + U` to edit the dog name, cycle registered breeds/models, independently select skin and equipment drawable/texture, run training, inspect the dog, and adjust the HUD. Variation counts are queried from the spawned GTA model and every appearance change previews live. The complete per-dog profile persists in `profile.dat`.

GTA's standard animal models have limited clothing components, so vest and coat slots display only where the selected model provides those variations. For a fully modeled vest, install an add-on K9 model and set `CustomModel`, `VestComponent`, and the Custom vest option in the kennel.

### Malinois and Dalmatian compatibility

- [German Shepherd / Malinois K9 Dog](https://www.lcpdfr.com/downloads/gta5mods/character/19996-german-shepherd-malinois-k9-dog/) replaces `a_c_shepherd`. Select **Belgian Malinois** in the kennel. AdvancedK9 recognizes the model's published Malinois, Dutch Shepherd and black Shepherd texture slots and safely limits selections to the variations actually installed.
- [Dalmatian Ped / Fire K9](https://www.lcpdfr.com/downloads/gta5mods/character/48014-dalmatian-ped-add-on-replace-fire-k9/) supports add-on model `a_c_dalmatian` and a single-player `a_c_husky` replacement. Select **Dalmatian**; AdvancedK9 tries the add-on first and falls back to the husky replacement. Its published BCSO, police, trooper, fire, off-duty, service and medic vest choices are represented in the kennel.
- The compatible [protective vest texture collection](https://www.lcpdfr.com/downloads/gta5mods/character/43951-improved-k-9-protective-vests-for-your-furry-friend/) adds multicam, tan, black, green, grey, blue, yellow, red and orange textures for the martinct model. Use the vest-color option to cycle the installed textures.

Third-party model and texture files are not redistributed. Install the chosen model from its author first. `CompatibilityPresets.json` records model names, fallback behavior, published variants and source links. Runtime bounds checks prevent an unavailable drawable or texture index from being applied.

Fetch attaches the ball to the dog head/mouth bone. Custom models with different bone orientation can be fine-tuned under `[Fetch]` using `BallOffsetX`, `BallOffsetY`, and `BallOffsetZ` in `AdvancedK9.ini`.

### Doberman and Cane Corso compatibility

- The free [PopcornRP Doberman add-on](https://github.com/alberttheprince/popcornrp-pets/tree/main/stream/doberman) publishes spawn name `doberman` and three head/coat textures. AdvancedK9 uses that add-on first and falls back to the available retriever-replacement installation.
- The free [Cane Corso 4K replacement](https://gta5mod.net/gta-5-mods/player/cane-corso-4k-replace-v1-0/) replaces `a_c_chop`, which AdvancedK9 uses for the Cane Corso kennel preset.
- These free packages do not publish separate LSPDFR vest components. AdvancedK9 detects any component supplied by the installed model; otherwise it leaves the dog unvested instead of applying an invalid drawable.

## AI voice setup

The default provider is Groq with `whisper-large-v3-turbo`. Put the key directly in `Plugins\LSPDFR\AdvancedK9\AdvancedK9.ini`:

```ini
[Voice]
Enabled=true
ContinuousListening=false
Provider=Groq
Model=whisper-large-v3-turbo
Language=en
ApiKey=gsk_your_key_here
ApiKeyEnvironmentVariable=GROQ_API_KEY
```

Do not add quotes around the key. Save the INI and restart RAGE Plugin Hook because voice configuration is loaded when the plugin starts. The key is never displayed or logged. If `ApiKey` is blank, the configured environment variable is used as an optional fallback.

For OpenAI, change the voice section to:

```ini
Provider=OpenAI
Model=gpt-4o-mini-transcribe
ApiKey=sk_your_key_here
ApiKeyEnvironmentVariable=OPENAI_API_KEY
```

AI voice requires internet access and may incur provider charges. Audio is sent only while push-to-talk is held. `ContinuousListening` is retained as a legacy setting but is intentionally ignored. Set `Enabled=false` for keyboard-only play; all commands remain accessible through `Left Ctrl + J`.

## Trust and handler safety

Trust persists in `Plugins\LSPDFR\AdvancedK9\trust.dat`. Petting, feeding, successful searches/tracks, controlled apprehensions and academy work raise it. Low trust causes slower responses, hesitation and less reliable indications; safe apprehension is locked below 25. At high trust the dog responds quickly and reliably.

The dog uses the handler's friendly relationship group and has friendly attacks disabled. A continuous safety interlock also checks for any invalid combat state involving the handler, clears it immediately and recalls the dog. Trust never overrides this protection.

## Search semantics

The closest ped or vehicle in range is searched. If an installed Policing Redefined release exposes a compatible public K9/contraband query, its result is used. Otherwise the fallback probability in `AdvancedK9.ini` is used. This prevents a hard dependency on prerelease API signatures while keeping the mod loadable by itself.

Vehicle searches visit eight exterior checkpoints around both sides, front and rear before the result is determined. For apprehension, aim a taser or firearm directly at the intended suspect and keep that person in your sights while issuing the command; proximity alone never chooses the target.

## Safety and limitations

- This is a single-player LSPDFR mod; never load RAGE Plugin Hook in GTA Online.
- “Non-lethal” is enforced by recalling the K9 at the configured threshold and restoring that health floor if a damage tick crosses it. Other mods, weapons, traffic and game physics can still harm an NPC.
- Native/API compatibility can only be fully verified inside a matching GTA V + RPH + LSPDFR installation. Keep the game SDK assemblies out of source control.
- AI voice uses the default microphone, the configured provider and the `NAudio.dll` installed by the build. Set `Enabled=false` for fully offline keyboard-only play.

## Developer layout

- `Plugin.cs` — LSPDFR duty lifecycle
- `K9Controller.cs` — commands and gameplay state machine
- `PolicingRedefinedBridge.cs` — optional compatibility boundary
- `VoiceCommandService.cs` — push-to-talk AI transcription and named wake-word mapping
- `K9Profile.cs` — persistent breed, coat, vest and name profile
- `DogCamera.cs` — scripted K9 camera
- `AcademySession.cs` — guided training evaluation
