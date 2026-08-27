# Advanced K9 v0.22.8-beta for LSPDFR

AdvancedK9 v0.22.8-beta is a persistent LSPDFR/RAGE Plugin Hook police-dog partner with push-to-talk voice control, per-dog progression, recoverable field injuries, accessible exterior station kennels and a compact Glass Tactical HUD with live search states. All published builds are beta builds while gameplay and model compatibility continue to be tested.

## Glass Tactical HUD

The default HUD is one compact lower-right card. Open the kennel/profile menu, then choose **HUD & Display** to move or resize it, adjust opacity, preview searches and alerts, switch distance units, reset its position, or independently hide any displayed field. Inactive, kenneled and vehicle-secured states can collapse automatically.

Custom profile portraits are cached and never loaded every frame. Set `PortraitFile=` in `AdvancedK9.ini` or `profile.dat`, for example `Plugins\LSPDFR\AdvancedK9\Portraits\Rex.png`. If unavailable, AdvancedK9 checks model and breed filenames in the Portraits folder and then uses a safe breed badge. Example filenames are `a_c_shepherd.png` and `german_shepherd.png`.

AdvancedK9 runs as an RPH plugin and starts its controller directly. It avoids a reflection-based LSPDFR duty gate because RPH plugins run in isolated AppDomains. Instead, it follows LSPDFR's own duty-state messages from the active RAGEPluginHook log. GTA's native cop flag is diagnostic only because LSPDFR may set it while the player is still off duty. While off duty, the K9, HUD, menus, shortcuts and voice capture remain disabled. Going off duty closes the UI, stops voice capture and dismisses the active dog.

## Drag-and-drop installation

Use the compiled release ZIP, not the source ZIP. Close GTA V, then drag everything in the ZIP into the folder containing `GTA5.exe`; this installs `LemonUI.RagePluginHook.dll` in the GTA V root and merges the `Plugins` folder. In RAGE Plugin Hook settings, enable **Load all plugins on startup**. Start Story Mode, load LSPDFR, and go on duty.

The compiled release includes AdvancedK9, its configuration, and the MIT-licensed LemonUI RAGE Plugin Hook runtime. It does not redistribute NAudio support assemblies, Microsoft.Win32.Registry, System.Security assemblies, ScriptHookV, GTA V, RAGE Plugin Hook, LSPDFR, Policing Redefined, Common Data Framework, or third-party dog models.

## Features

- Deploy/dismiss and follow/heel
- Sit, lie down, pet, feed, physical rope leash with handler-follow movement, and dog-mounted camera
- Persistent food and water care HUD, low-need warnings, separate feed/drink interactions and working-dog consumption
- Kneeling handler petting interaction with a synchronized K9 response
- Automatic operational leash release when search, tracking, fetch or apprehension is deployed
- Pedestrian and four-corner vehicle odor searches; silent sit means clear, sit plus repeated bark after the completed sweep means positive
- General searches identify narcotics, explosives or weapons whenever the deployed dog holds the corresponding specialty certification
- Automatic natural bladder and bowel relief during safe idle periods, with urine effects and temporary dog-waste props
- Physical ground-snapped doghouse kennels at seventeen accessible police, sheriff, highway patrol, ranger, port, airport, corrections and state locations, with labeled blue dog-icon map blips and required pickup/return
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
- Fully optional K9 status HUD with five designs, icon sets, color themes and text styles plus individual data toggles
- Per-dog XP, certifications, health, injuries, stamina, trust and statistics
- Optional runtime bridge to Policing Redefined/Common Data Framework search records
- Persistent 0–100 handler trust with obedience, response-time, detection and deployment effects
- Handler-safety interlock: friendly relationship group, officer-target rejection and automatic emergency recall
- Immediate aimed-target K9 deployment up to 250 meters; no traffic stop, detention or close approach is required
- Persistent kennel profile for breed, coat variation, vest style/color and custom K9 name
- Rear-door kennel staging and remote door-pop deployment from a stopped patrol vehicle
- Heat, rain, fatigue, injury/limp, rest and veterinary-care simulation
- Bagged scent articles with age, rain, distance and vehicle-travel degradation
- Track completion is alert-only: the K9 barks, sits and holds without attacking until the handler separately aims and commands Apprehend
- Surrender, freeze, flee or fight suspect reactions plus whistle and silent hand-signal recall
- Persistent duty equipment, patrol-vehicle restocking and CSV incident records in `Plugins/LSPDFR/AdvancedK9/K9IncidentLog.csv`
- GPS K9-camera overlay with heading, handler distance, state and live condition telemetry
- Grouped, scrollable command categories; academy and specialty training appear only inside Training & Certifications.
- Live per-vehicle rear-seat calibration with saved model profiles and an audit log.
- Automatic compatibility modes for Policing Redefined/CommonDataFramework, Stop The Ped, or standalone operation.
- PR/STP active traffic-stop vehicle and interaction-ped targeting, restrained-ped safety, pursuit suspect acquisition, inventory-aware odor classification and best-effort K9 result sharing.
- Five-minute recorded suspect scent trails with realistic trail loss and a handler-commanded reacquisition cast.
- Six-sector building searches that end with an alert bark and hold, never an automatic bite.
- Automatic PR/STP pursuit and vehicle-bailout scent assignment without requiring a ped or traffic stop.
- Tactical K9 warning with surrender, freeze, flee and fight outcomes before optional separate apprehension.
- Detailed CSV deployment reports containing warning, scent source, track distance/time, bite duration, injuries and disposition.

## Build

Prerequisites: Visual Studio 2022 with **.NET Framework 4.8 Developer Pack**, LSPDFR 0.4.9+, RAGE Plugin Hook, and GTA V Story Mode.

From a Developer PowerShell:

```powershell
msbuild AdvancedK9.csproj /p:Configuration=Release /p:GtaVDir="D:\Games\Grand Theft Auto V"
```

The build copies the RAGE Plugin Hook entry-point assembly `AdvancedK9.dll` to `Plugins` and its configuration files to `Plugins\LSPDFR\AdvancedK9`. If `GtaVDir` is omitted, copy those files manually. Do not place the DLL in `Plugins\LSPDFR`; that folder is reserved for assemblies implementing the LSPDFR plugin API.

## Controls

- `Left Ctrl + K`: pick up/return K9 while standing at a station doghouse
- `Left Ctrl + J`: command menu, then `0`–`9`
- `Left Ctrl + C`: dog camera
- `Left Ctrl + L`: leash
- `Left Ctrl + U`: open the persistent K9 profile/kennel menu
- `Left Ctrl + J`: open the persistent command menu
- `V`: hold to open the microphone and speak a command

Voice phrases begin with the configured dog name or “K nine”: “Rex, sit”, “Rex, search”, “K nine, recall”, and so on. Commands without the wake name are ignored.
Wake-word variants `K9`, `K-9`, `K nine`, `kay nine` and `canine` are accepted. Natural phrases such as “K9 sit down,” “K9 fetch the ball,” and “K9 search the vehicle” are normalized before matching.
Specialty examples include “K9 search for narcotics,” “K9 bomb search,” “K9 weapons search,” “K9 narcotics training,” “K9 explosives training,” and “K9 weapons training.”

## Complete verbal command reference

Hold `V`, begin with the configured dog name or `K9`, and then say one of the phrases below. Example: `K9, search the vehicle`. Punctuation and capitalization do not matter.

| Action | Accepted spoken phrases after the wake word |
| --- | --- |
| Deploy / dismiss | deploy K9; deploy the dog; bring out the dog; partner up; send out the dog; dismiss K9; kennel up; end shift; dismiss |
| Follow | follow me; stay with me; move with me; on me; with me; follow |
| Heel | come to heel; heel up; get to heel; by my side; at heel; heel; heal |
| Sit | sit down; take a seat; park it; sit |
| Lie down | lie down; lay down; get low; down on the ground; go down; down |
| Stay | stay there; hold position; do not move; remain; stand fast; stay; hold |
| Recall | return to me; back to me; come here; come back; return; recall; disengage and return; come |
| Whistle recall | whistle recall; recall whistle; come on whistle |
| Hand signal | hand signal; signal recall; silent recall |
| Fetch | fetch the ball; retrieve the ball; get the ball; bring the ball; go fetch; play fetch; play ball; retrieve; fetch |
| Search area | search the area; clear the area; sweep the area; check the area; area search; search around; find the odor; search |
| Search building | search the building; clear the building; building search; clear the rooms; search inside; check the building |
| Search vehicle | search the vehicle; search this vehicle; search the car; check the vehicle; check the car; sniff the vehicle; sweep the vehicle; vehicle search; search vehicle |
| Narcotics search | search for narcotics; narcotics search; search for drugs; drug search; find the drugs; check for narcotics; narcotics sweep; find dope |
| Explosives search | search for explosives; explosives search; bomb search; search for a bomb; find the bomb; check for explosives; explosive sweep; bomb sweep |
| Weapons search | search for weapons; weapons search; gun search; search for a gun; find the weapon; check for firearms; firearm sweep; weapons sweep |
| Collect scent article | collect scent article; bag the scent; take scent sample; collect scent — aim at the person, or aim at/stand beside the vehicle they fled from |
| Track | start tracking; pick up the scent; follow the scent; find the trail; track the suspect; locate them; find him; find her; find them; track |
| Reacquire trail | reacquire the trail; find the trail again; pick the trail back up; recover the scent; find scent; reacquire scent |
| K9 warning | give K9 warning; give the warning; police K9 warning; announce the dog; warn the suspect; K9 warning |
| Apprehend | apprehend the suspect; engage the suspect; take the suspect; send the dog; attack; bite; get him; get her; take him; take her; apprehend; engage |
| PR/STP arrest handoff | handoff arrest; start arrest handoff; give suspect to policing menu; process suspect; arrest handoff |
| Request perimeter | request perimeter; set a perimeter; call perimeter units; containment units |
| Request transport | request prisoner transport; call transport; prisoner transport; transport suspect |
| Request medical | request EMS; call EMS; request medical; medical assistance |
| Request bomb squad | request bomb squad; call bomb squad; request explosive unit; bomb disposal |
| Door pop | door pop; deploy from vehicle; release from car; pop the door |
| Release | release the suspect; stop the dog; stop apprehension; disengage; break contact; leave it; let go; release; out |
| Guard | guard the suspect; watch the suspect; cover him; cover her; hold the suspect; watch him; watch her; stand guard; guard; watch |
| Bark | give an alert; sound off; make noise; bark; alert; speak |
| Enter vehicle | enter the vehicle; enter vehicle; load into the car; load up; mount up; get in the vehicle; get in the car; get inside; get in |
| Exit vehicle | exit the vehicle; exit vehicle; unload from the car; dismount; come out; get out of the vehicle; get out of the car; unload; get out |
| Pet | pet the dog; praise the dog; reward him; reward her; show affection; good dog; pet |
| Feed / treat | give the dog a treat; give a treat; reward with a treat; give food; feed the dog; treat; feed |
| Give water | give the dog water; give water; water the dog; get a drink; drink water; water break; hydrate; drink |
| Rest | rest the dog; take a rest; sleep; rest |
| Inspect | inspect the dog; check the dog; check status; check injury; check health; medical check; inspect |
| First aid | give first aid; apply first aid; provide treatment; field treatment; treat the injury; treat injury; first aid |
| Veterinary care | go to the vet; veterinary care; vet treatment; visit veterinarian; vet |
| Restock equipment | restock equipment; reload K9 gear; replenish supplies; restock |
| Toggle leash | attach the leash; put on the leash; take off the leash; remove the leash; leash on; leash off; attach leash; remove leash; leash |
| K9 camera | activate dog camera; turn on K9 camera; disable dog camera; turn off K9 camera; dog camera; K9 camera; body camera; camera |
| Core training | go to training; start core training; training ground; begin academy; academy training; core certification course; core training; academy; certification |
| Narcotics training | start narcotics training; narcotics certification; drug detection training; train for drugs; narcotics academy |
| Explosives training | start explosives training; bomb certification; bomb detection training; train for explosives; explosives academy |
| Weapons training | start weapons training; weapons certification; gun detection training; firearm training; weapons academy |

Accepted wake words are the configured dog name, `K9`, `K-9`, `K nine`, `K 9`, `kay nine`, and `canine`.

## Vehicle seat calibration

Load the K9 into a stopped vehicle, open the profile menu with `Left Ctrl + U`, and choose **Vehicle Seat Configuration**. Use left/right on the X, Y and Z rows to position the dog live, then select **Save for this vehicle model**. Each GTA vehicle model keeps its own position in `Plugins/LSPDFR/AdvancedK9/VehicleSeatConfigurations.ini`. Every save is recorded in `VehicleSeatConfigurationLog.csv` and `RagePluginHook.log`.

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

[Notifications]
ShowVoiceStatusText=false
ShowActionNotifications=false
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

Holding V is silent by default. Voice errors remain visible even when status text is disabled. Routine command acknowledgement popups are also disabled by default; K9 search/detection results, failures and safety warnings remain visible.

Updates preserve the live AdvancedK9.ini. The release package supplies AdvancedK9.default.ini and copies it to the live name only when no user configuration exists.

## Trust and handler safety

Trust persists in `Plugins\LSPDFR\AdvancedK9\trust.dat`. Petting, feeding, successful searches/tracks, controlled apprehensions and academy work raise it. Low trust causes slower responses, hesitation and less reliable indications; safe apprehension is locked below 25. At high trust the dog responds quickly and reliably.

The dog uses the handler's friendly relationship group and has friendly attacks disabled. A continuous safety interlock also checks for any invalid combat state involving the handler, clears it immediately and recalls the dog. Trust never overrides this protection.

## Compatibility and search semantics

`Compatibility.Mode=Auto` selects Policing Redefined/CommonDataFramework first, Stop The Ped second, and standalone fallback otherwise. Never run Policing Redefined and Stop The Ped together. `Plugins\LSPDFR\AdvancedK9.LSPDFRBridge.dll` runs inside LSPDFR's AppDomain and publishes a guarded heartbeat snapshot to the isolated RPH controller, allowing PR/CDF detection and live target/search-state sharing without making either external plugin a hard dependency.

When supported by the detected plugin, AdvancedK9 prioritizes its active traffic-stop vehicle or selected pedestrian, reads exposed inventory/search records, classifies narcotics, explosives and weapons, shares K9 indications, recognizes pursuit suspects and vehicle bailouts, and rejects restrained, surrendered, arrested or transported peds from apprehension. Detailed adapter/version/API diagnostics are written to `RagePluginHook.log`. Tracking and apprehension never require a PR or STP stop.

When no compatible target or record API is exposed, the closest valid ped/vehicle and `AdvancedK9.ini` fallback probability are used. This prevents API changes from stopping the plugin from loading.

Vehicle searches visit all four exterior corners before the result is determined. For apprehension, aim a taser or firearm directly at the intended suspect and keep that person in your sights while issuing the command; proximity alone never chooses the target.

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
