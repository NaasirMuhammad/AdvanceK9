# Changelog

- Prevents handler gunfire from applying human ambient/gesture reactions to the K9. Gunfire suppression now clears only the secondary reaction animation and no longer resets animal movement clipsets or replaces active K9 tasks.
- Adds user-defined voice-command phrases through `[CommandPhrases]`; custom alternatives supplement built-in phrases and are supplied to transcription for improved recognition.
- Makes every AdvancedK9 shortcut configurable while preserving existing defaults, and accepts `Modifier=None`/`Off`/`Disabled` for optional modifier-free shortcuts.

All published AdvancedK9 versions are beta builds.

## 0.23.0-beta

- Completes article-based scent identification for aimed world objects, active-pursuit articles, nearby single-owner articles, people and recently occupied vehicles; ambiguous or ownerless articles are rejected instead of assigning the wrong track.
- Adds a physical track-direction indication before movement and after route reacquisition, including the K9's body line, cardinal direction, heading and incident-log entry.
- Adds separate K9 Hold Perimeter and Contain Suspect commands. The former patrols a handler-centered ring; the latter maintains a moving non-bite containment ring around an identified suspect, with both reducing radius while retaining an attached working leash.
- Extends automatic pursuit integration to native LSPDFR pursuits in addition to PR/STP, assigns vehicle-bailout scent and starts the recorded trail automatically when a deployed K9 is available.
- Adds handler-down protection: the K9 emergency-deploys from the vehicle, engages only an immediate non-officer attacker, otherwise guards and barks beside the handler, and requests medical service when a compatible provider is available.
- Keeps apprehension a separate aimed command during scent tracking and containment, and preserves restrained/surrendered/officer safety interlocks.

## 0.22.12-beta

- Services push-to-talk and recognized-command queues during interactive academy prompts, allowing the displayed SIT, DOWN, STAY, RECALL, SEARCH, TRACK, APPREHEND, GUARD and RELEASE commands to advance training without also pressing Y.
- Guarantees healthy, trained Elite Bond teams an immediate response from 80 confidence upward; a Level 5 handler at 100 trust and 83 confidence no longer rolls random hesitation.
- Decodes PR 1.0.0.5's generated `SearchItem.Value` field so Rex reads the populated inventory that the officer search later displays instead of reporting it unavailable.
- Preserves PR's structured `DrugType`/weapon metadata, covering catalog descriptions that do not name their odor directly (including burnt-spoon heroin, blotter-paper LSD, morphine, Adderall and Ritalin entries).
- Recognizes PCP/phencyclidine as a narcotics odor.
- Classifies multiple odors in the same PR inventory and expands matching across PR/GTA drugs, firearms, ammunition, melee weapons, launchers, explosives and explosive components while excluding replicas, permits and manuals.
- Keeps every normal field command callable while a leash exists, releases apprehension/fetch based on the rope itself, and stops rapid follow-task restarts that could leave the K9 stationary beside the handler.
- Accepts routine attached-leash commands deterministically and renders the hand-to-collar rope without entity-binding physics that could freeze K9 navigation.
- Advances leashed scent tracking in short handler-relative lead steps and retains the current trail point until the handler walks forward.
- Retains the complete v0.22.11 Glass Tactical HUD, portrait loader, kennel defaults/editor, CDF/Nexus bridge, and progressive training systems.
- Generates each target's PR search inventory once through a compatible public PR initializer when exposed, then retains that same PR-owned inventory for Rex and the officer search.
- Uses strict `ItemName`/`DisplayName` extraction from CDF when PR search items have not yet been generated, preventing pre-search drug/firearm false negatives without reintroducing schema/category explosive false positives.
- Treats unavailable inventory as inconclusive—not as a negative indication—and records neither a positive nor negative K9 result.
- Refreshes PR's authoritative item list after the officer physically searches, then publishes the discovered-item narrative to the active NexusMDT report.
- Keeps search, scent and tracking commands available on a longer working leash; apprehension and fetch still release it automatically.
- Pins the visible leash endpoint to the handler's left hand and the K9 collar while the rope is active.
- Exposes all 13 Belgian Malinois textures and bundles matching portraits 4–12 in the established `Plugins\\LSPDFR\\AdvancedK9\\Portraits` location.

## 0.22.11-beta

- Removes the human-skeleton animation from petting; the handler kneels while the K9 remains in a native animal seated idle, preventing body contortion.
- Adds selectable circular or square HUD portrait framing, with the approved circular portrait as the default.
- Removes the artificial HUD safe-zone gap and permits live dragging to the actual screen edges while retaining resolution-aware bounds.
- Separates handler bond from K9 confidence and persists confidence per profile.
- Makes elite-bond, high-confidence, properly certified K9s respond immediately and reliably to operational commands.
- Restricts meaningful hesitation to commands still being learned, poor bond/confidence, exhaustion, hunger, dehydration or injury.
- Replaces percentage training jumps with persistent requirements of 100, 250, 450, 800 and 1,200 XP for Levels 1–5.
- Requires 250 XP independently for narcotics, explosives and weapons certifications.
- Awards 0–10 XP at Levels 1–2, 0–20 XP at Levels 3–4 and 0–30 XP at Level 5, capped by session performance; specialty sessions award up to 20 XP.
- Randomizes obedience drill order, agility direction/spacing, scent placement, tracking turns/distances and apprehension-suspect placement.
- Adds instructional command wording and interactive handler prompts throughout academy sessions.
- Migrates existing percentage progress into the new XP requirements without discarding completed certifications.

## 0.22.10-beta

- Makes the approved compact Glass Tactical concept the exact default/reset HUD layout: smoked-charcoal card, cyan frame, portrait at left, name/status, real health and stamina meters, upper-right paw, divider and compact command/distance/behavior row.
- Bundles original 256×256 HUD portraits for every built-in dog breed plus separate German Shepherd, Belgian Malinois and Doberman coat choices, including the all-black Malinois.
- Standardizes every bundled portrait to the same centered tactical-vest composition, smoked background and single cyan Glass Tactical ring used by the approved Malinois design.
- Resolves portraits through custom profile override, profile name, exact breed/coat/vest/texture, breed/model coat, model, breed and generic fallback, refreshing the cached texture only after an appearance or profile change.
- Preserves all HUD editing, scaling, opacity, anchoring and per-field visibility controls so the approved design is the starting point rather than a forced layout.
- Corrects the CDF integration false positive that caused unrelated vehicles to indicate explosives.
- Reads the actual item names from PR `SearchItemsAPI`, matching the list later displayed by the regular officer vehicle/person search.
- Stops flattening CDF vehicle/person database records; empty category, permit, schema and metadata properties can no longer become K9 odors.
- Accepts CDF contents only when the record exposes an explicitly named search/inventory collection.
- Correctly supplies NexusMDT's active call/report number to `AppendIncidentNote` instead of passing note text as the number.
- Retains the exact PR item list after a K9 sniff, waits for PR to confirm the officer search, and then appends the discovered items to the active Nexus report.
- Keeps report reconciliation pending for up to 15 minutes when the report has not been opened yet, with a throttled two-second retry and no guessed report IDs.
- Links the exact ped or vehicle to the shared PR/CDF record and reads PR's generated search-item list as the authoritative contents exposed by the installed public APIs.
- Adds protocol v2 request/response provenance so logs identify `CDF.Items` or the PR search-items fallback.
- Publishes completed positive and negative K9 indications to a compatible public NexusMDT incident-note API for report writing.
- Keeps K9 indications observational: AdvancedK9 does not reveal item names, mutate inventories, recover evidence or mark an officer search complete.
- Leaves regular officer searches to Policing Redefined/CDF, allowing NexusMDT's existing search capture to log the same inventory.
- Adds safe configuration switches for CDF reads and NexusMDT sharing, defaulting on when the keys are absent from an existing INI.
- Adds one-time public API-surface diagnostics and explicit success/fallback logging without per-frame reflection or polling.
- Does not alter or redistribute CommonDataFramework, Policing Redefined, NPCI or NexusMDT files.
- Replaces the default positions and headings for Vespucci, Del Perro, Port of Los Santos, Davis, La Mesa, Mission Row, FIB, Vinewood, Beaver Bush Ranger, Great Ocean Highway, Fort Zancudo, Paleto Bay, Brook Trail and Sandy Shores with the finalized values exported from the in-game editor.
- Restores accessible FIB and Brook Trail kennels using the newly supplied locations.
- Uses the finalized saved Z and heading for all 14 edited doghouses without applying another automatic rotation or ground snap; each prop remains level.
- Leaves Rockford Hills, LSIA and Bolingbroke at their previously approved locations.
- Adds optional `[KennelLocations]` overrides to `AdvancedK9.ini`. Each station accepts `X,Y,Z,Heading`, allowing users to move kennels while preserving their customized INI during future updates.
- Adds a live in-game kennel editor with station selection, mouse/WASD dragging, height and heading controls, ground snapping, player-relative placement, reset/revert and explicit INI saving.
- Adds live HUD dragging while the HUD menu is open: hold the left mouse button or use W/A/S/D to move it, and use Q/E to resize it.
- Automatically migrates either previously shipped v0.22.10 default set to the finalized placements while leaving genuinely customized positions untouched.

## 0.22.9-beta

- Keeps the compact HUD in `SEARCHING` state for the entire asynchronous four-corner vehicle sweep while continuing to omit the redundant bottom `VEHICLE SEARCH` caption.
- Removes the legacy vehicle-search start notification and per-corner subtitles below the HUD.
- Displays both positive and negative vehicle-search outcomes only in the result panel attached above the Glass Tactical card.
- Makes negative searches completely silent: routine corner checks no longer play the indication animation, and the three-bark alert is authorized only after a confirmed positive result.
- Adds explicit result diagnostics to the RPH log showing whether a three-bark positive alert was authorized or a silent negative was returned.
- Simplifies the release archive name to `AdvancedK9-v0.22.9-beta.zip`; its install-ready internal layout is unchanged.

## 0.22.8-beta

- Removed the Brook Trail kennel and its map blip because the location remained unreliable and floating.
- Removed the FIB kennel and its map blip because the location remained inside or inaccessible around the building.
- Restored the Great Ocean Highway kennel to the station side of its previous area, then offset it in the opposite direction from the highway toward the left/bottom side of the police-station map symbol.
- Left every other station kennel unchanged from v0.22.7.

## 0.22.7-beta

- Restored the San Andreas/Vespucci kennel to its previous horizontal location and applies the corrected ground alignment there.
- Retained the approved Davis location while lifting the doghouse above the pavement to prevent ground clipping.
- Moved the FIB kennel away from the wall to the San Andreas Avenue/Elgin side of the building.
- Moved the Beaver Bush ranger kennel away from the tent and ranger vehicle parking position.
- Moved the Great Ocean Highway kennel off the dirt travel lane and onto the adjacent station area.
- Moved the Paleto Bay kennel out of the accessible parking bay.
- Ground correction is deliberately targeted: Davis receives a 0.12-metre anti-clipping lift and forced level rotation; Vinewood, Brook Trail and restored San Andreas receive precise terrain alignment. Unreported kennels retain their already-correct v0.22.6 placement behavior.

## 0.22.6-beta

- Moved the kennels reported inside buildings or over landscaping at Davis, Vespucci/San Andreas, Rockford Hills, Vinewood, La Mesa, Sandy Shores, Paleto Bay, Beaver Bush, Bolingbroke, FIB, Del Perro/Docks and Fort Zancudo onto exterior paved or parking areas.
- Preserved the confirmed-good horizontal locations for Mission Row, LSIA/Airport, Los Santos Port, Raton Canyon/Great Ocean Highway and Senora/Brook Trail.
- All doghouses, including those already in good locations, now wait for nearby collision and snap down to the locally loaded ground surface.
- Kept all K9 kennel map blips available while on duty, but now streams the physical doghouse only when the handler is within 350 metres.
- Requests local collision before creating a nearby kennel, allowing ground placement to use loaded pavement instead of freezing distant props in midair.
- Removes distant physical kennel props again to reduce world-object overhead while retaining their map locations.

## 0.22.5-beta

- Moved the Davis station kennel from inside the building to the exterior rear parking lot.
- Moved the Vinewood station kennel from the underground placement to its exterior parking lot.
- Disabled automatic ground snapping for these two verified multi-level station locations.
- Added a two-meter vertical safety limit to every other kennel ground snap and freezes placed doghouses so GTA cannot shift them underground.
- Preserved the confirmed-good Mission Row kennel without coordinate changes.
- Audits every spawned kennel against GTA's interior system and records its exterior/interior result in `RagePluginHook.log`.
- Replaced the human-skeleton vehicle-entry task with a visible Rottweiler jump through the open rear door into the saved calibrated seat, preventing the K9 body from twisting or contorting.
- Custom/add-on dogs that cannot load the animal jump animation use a brief hidden direct-seat fallback instead of the broken human animation.

## 0.22.4-beta

- Vehicle, area, specialty and building searches now run on a dedicated gameplay fiber so the controller and HUD remain responsive throughout the search.
- The Glass Tactical HUD now displays `SEARCHING` during the four-corner vehicle sweep instead of retaining the stale `FOLLOWING` snapshot.
- Removed the redundant bottom `VEHICLE SEARCH` wording from the vehicle-search HUD state.
- Positive specialty alerts are armed before the indication bark and appear in the amber panel attached above the K9 card.
- Added a duplicate-search guard so another search cannot begin while one is already active.

## 0.22.3-beta

- Replaced the dead-K9/kenneled-state collision with a recoverable `DOWNED` state.
- A critically injured K9 keeps its field position and red K9 blip instead of being reported as kenneled.
- Care & Medical > First Aid now revives and stabilizes a downed K9; veterinary care is still required for full recovery.
- Recreates a deployed K9 at its last known position as downed if another plugin removes the dead ped before treatment.
- Added the missing cyan paw indicator to the Glass Tactical HUD.
- Portrait lookup now checks the explicit override, K9 profile name, ped model, breed and default image in that order.
- Reduced HUD snapshot work to 20 updates per second, kennel proximity checks to four per second, and scent sampling to a capped 2.5-second pass within 200 meters to reduce intermittent frame hitches.

## 0.22.2-beta

- Replaces the legacy panel with one compact lower-right Glass Tactical card using smoked charcoal, a thin cyan frame, condensed white text, green health status and attached amber K9 alerts.
- Adds contextual normal, collapsed, search and alert states in the same card area, plus actual distance, profile health/stamina, current command and behavior data.
- Adds cached custom portraits through `PortraitFile`, followed by model, breed and safe badge fallbacks; missing or invalid images cannot stop the plugin.
- Adds in-game movement, scaling, opacity, preview/reset, automatic collapse, metric/imperial distance and individual visibility controls for every tracked HUD field.
- Reorganizes the profile UI into focused Identity & Appearance, HUD & Display, Vehicle Seat, Profile/Health/Certifications and Voice sections while keeping commands grouped and scrollable.
- Migrates older HUD profiles to the compact lower-right safe-zone default without changing gameplay, training, seating or certification data.

## 0.22.1-beta

- Prevents fabricated specialty alerts when PR/CDF is active but has not supplied an inventory record; the bridge now accepts explicit vehicle/ped record requests by entity handle, waits for the matching response, and maps handgun/firearm text to Weapons instead of using random certification selection.
- Expands physical station doghouses to seventeen accessible police, sheriff, highway patrol, ranger, port, airport, corrections and state locations; kennels snap to the exterior ground and receive labeled blue dog-icon map blips while on duty.
- Adds `AdvancedK9.LSPDFRBridge.dll` under `Plugins\LSPDFR` so Policing Redefined and CommonDataFramework are detected inside LSPDFR's AppDomain instead of being incorrectly reported as unloaded by the isolated RPH controller.
- Shares live PR/CDF active-vehicle, interaction-pedestrian, pursuit-suspect and exposed search-record state with the main controller through a guarded heartbeat snapshot.
- Adds physical doghouse kennels at nine police and sheriff stations. Normal deployment requires station pickup and normal dismissal requires station return.
- Replaces `Ped.Dismiss()` with task, seat, leash and combat cleanup followed by hard entity deletion, preventing a released K9 from becoming ambient AI or stealing a cruiser.
- Makes urination and bowel relief fully automatic during safe idle periods; removes the menu command, verbal phrases, HUD meters and routine bathroom notifications.
- Makes push-to-talk listening/transcribing text optional and disabled by default; microphone, API and transcription failures always remain visible.
- Makes routine K9 command/action notifications optional and disabled by default while preserving errors, safety warnings and actual K9 outcomes such as positive/negative detection results.
- Removes the gardening scenario/prop from petting and uses a prop-free handler interaction.
- Removes the handler bending/gardening animation from feeding and watering; a real bowl is placed in front of the K9 for the eating/drinking interaction.
- Adds five HUD designs, five icon treatments, five color themes and five text treatments.
- Makes state, health, stamina, food, water, certifications, trust, training, injury and voice HUD fields individually optional and persistent.
- Preserves an existing AdvancedK9.ini during updates. The package now ships AdvancedK9.default.ini, copied to the live filename only on a first installation.
- Keeps the latest LemonUI RPH package (2.2.0) while removing the direct MouseBehavior setter dependency that caused older runtime DLLs to throw MissingMethodException.

## 0.22-beta

- Replaces the one-Boolean Policing Redefined probe with automatic `PolicingRedefined`, `StopThePed`, and `Standalone` compatibility modes.
- Detects PR, CommonDataFramework and STP versions at runtime without adding hard DLL dependencies; logs the selected integration mode, API member used, search source and fallback reason.
- Prioritizes the active PR/STP traffic-stop vehicle and interaction pedestrian instead of accidentally selecting a nearby unrelated entity.
- Reads compatible PR/CDF/STP record, inventory and search results through a guarded adapter and classifies narcotics, explosives and weapons separately.
- Applies the deployed dog's certifications to integrated odor results and retains standalone fallback searches when an external API is absent or changes.
- Shares positive/negative K9 indications, located suspects and apprehensions back to compatible public PR/STP writer APIs when exposed.
- Recognizes PR/STP arrested, cuffed, handcuffed, surrendering, kneeling and transported states and blocks unsafe K9 contact.
- Keeps tracking and apprehension independent of PR/STP stops: an aimed target up to 250 meters or assigned pursuit suspect remains valid without detention or close approach.
- Adds automatic PR/STP pursuit-suspect recognition and assigns vehicle-bailout scent plus the recorded foot trail without initiating a stop.
- Records non-officer pedestrian trail points for up to ten minutes and follows five-minute route segments instead of continuously steering toward the suspect's live coordinates.
- Adds realistic scent-trail loss and an interactive Reacquire Trail command with a three-point casting pattern.
- Adds a six-sector Building Search command; located subjects receive an alert bark and hold only, while clear structures finish silently.
- Adds a separate tactical K9 Warning command with surrender, flee, freeze and fight outcomes. Surrendered suspects are protected from later bite deployment.
- Removes randomized surrender/freeze delays from a valid aimed Apprehend command so deployment remains immediate after the handler makes the decision.
- Adds `K9DeploymentReports.csv` with scent source, warning status, track distance/time, bite duration, suspect outcome, K9 injury and disposition.
- Adds compatibility settings for mode selection, active-target use, result sharing and restrained-ped protection.
- Updates the menu and voice registry with Search Building, Reacquire Trail and K9 Warning commands and their natural verbal alternatives.
- Adds best-effort PR/STP arrest handoff plus perimeter, prisoner transport, EMS and bomb-squad service requests, with safe fallback instructions when a compatible public action is unavailable.

## 0.21-beta

- Makes tracking target selection explicit: aim at a suspect while issuing Track, or collect scent from the suspect or their recently occupied vehicle; nearest-ped guessing is removed.
- Allows vehicle scent collection to resolve a unique recent non-officer occupant and assign that fleeing suspect as the K9 track target.
- Reworks tracking cadence into one brief scent acquisition followed by sustained 12–28 meter running segments, with realistic scent confirmation only every 18–28 seconds.
- Makes track completion non-contact: the K9 clears its task, barks, sits and holds until the handler separately aims at the located suspect and commands Apprehend.
- Changes vehicle searches to four deliberate alert checkpoints at all four corners; negative sweeps remain silent and positive sweeps bark only after the fourth corner.
- Adds bladder and bowel needs, automatic relief, pee effects and a temporary dog-waste prop.
- Makes General Search report any narcotics, explosives or weapons specialty for which the dog is certified, without requiring the dedicated specialty command.
- Reloads `VehicleSeatConfigurations.ini` before every vehicle entry and applies the saved offsets through the same forced live-calibration path, preventing GTA's seat task from restoring defaults.
- Keeps the active K9 rear door open throughout seat calibration and closes it when the user leaves the calibration menu, unloads the K9 or dismisses the partner.
- Removes handler-scent breadcrumb recovery because it interrupted and slowed direct Follow behavior.
- Allows immediate aimed-target apprehension out to 250 meters with a firearm or taser; the target does not need to be stopped, detained or approached first.
- Fixes the seat-configuration preview so GTA's active seat task no longer overrides X/Y/Z changes; the K9 now visibly moves and replays its sit pose after every adjustment.
- Matches Stop The Ped's K9 vehicle behavior by preferring GTA/RPH seat index 2 (right rear), with corrected passenger-side seat bone and rear-door mappings.
- Adds dedicated Bug's Mods Dalmatian/a_c_husky replacement handling so all seven `hand_diff_000_a_uni` through `hand_diff_000_h_uni` vest textures can be selected in game.
- Groups the command menu into Partner Control, Search & Detection, Tracking & Scent, Tactical Deployment, Vehicle & Equipment, Care & Medical, and Training & Certifications submenus.
- Removes all training access from the profile/kennel menu so academy commands exist only under the command menu.
- Rebuilds the status HUD with a colored agency header, accent rail and separate health, stamina, food and water progress bars.
- Slows normal hunger and thirst decay from every minute to every three minutes, with lighter idle and working consumption.
- Displays only certifications the dog has actually completed in inspection and the expanded HUD.
- Adds live X/Y/Z rear-seat positioning controls while the K9 is seated.
- Saves offsets independently by vehicle model in `VehicleSeatConfigurations.ini` and records every save in `VehicleSeatConfigurationLog.csv` and the RPH log.
- Documents every supported voice action and accepted phrase in README.md.

## 0.20-beta

- Adds twelve realism systems: rear kennel-door staging, weather/heat exposure, rest and fatigue recovery, veterinary treatment, scent articles, scent aging, remote door-pop, varied suspect reactions, whistle/hand signals, persistent duty equipment, CSV incident logging, and GPS K9-camera telemetry.
- Makes rain, trail age, subject distance and vehicle travel degrade scent quality; weak trails require a fresh bagged article.
- Adds vehicle heat warnings and extra water loss when a K9 is left in a stopped vehicle with the engine off during peak daytime heat.
- Adds meals, water bottles, first-aid kits, scent bags and treats to the persistent profile, with patrol-vehicle restocking.
- Adds persistent 0–100 food and water needs, working-dog consumption rates, low-care warnings and HUD readouts.
- Adds a dedicated Give Water command and bowl interaction; Feed and Drink restore their separate needs.
- Makes low food/water realistically reduce stamina and command reliability without silently killing the K9.
- Replaces standing pet feedback with a synchronized kneeling handler interaction and K9 petting response.
- Splits detection into independently trainable narcotics (NAR), explosives/bomb (BOMB), and weapons/firearm (WPN) specialties.
- Adds separate persistent 0–100 specialty progress and certifications; handlers may train any combination or earn all three.
- Adds dedicated five-station academy setups and distinct voice/field commands for each detection specialty.
- Keeps every checkpoint silent. Negative results sit without sound; barking happens only after the final result is positive.
- Locks the aimed suspect when push-to-talk begins and removes trust hesitation from a valid apprehension, preventing repeated voice commands while transcription completes.
- Records the handler's recent route so a distant following K9 works back along the handler's scent trail instead of becoming stranded.
- Teleports the handler and K9 to a dedicated off-street academy ground for training, then returns both to the original patrol location.
- Adds five persistent gated training levels. Each level contains five exercises worth 20%, must reach 100% before the next unlocks, and awards its own OB, AGI, DET, TRK or APP certification.
- Makes vehicle-search sniffing face the vehicle at every exterior checkpoint.
- Adds multiple natural verbal alternatives for every K9 command and expands the AI transcription vocabulary.
- Replaces the drawn leash line with GTA's physical rope system and continuously walks the leashed K9 beside the handler.
- Automatically detaches the leash and continues when search, tracking, fetch or apprehension is commanded.

## 1.7.9-beta

- Raises and moves the sitting K9 rearward from the rear-seat bone so it rests on the cushion instead of the floor or front seat.
- Adds configurable rear-seat X/Y/Z offsets for unusual custom vehicle interiors.
- Makes vehicle detection use eight ordered checkpoints around the entire exterior before determining the result.
- Expands the academy to five interactive levels: obedience, place/stay, distance recall, cone agility and a randomized blind scent lineup.
- Requires the handler to aim a taser or firearm directly at a non-officer target before apprehension can deploy.

## 1.7.8-beta

- Makes voice strictly push-to-talk; the microphone opens only while V is held and ignores ambient room/game audio.
- Seats and anchors the K9 at the vehicle's actual rear-seat bone with a sitting animation instead of allowing cabin walking.
- Protects the K9 while seated and safely detaches it beside a stopped vehicle while preserving live health.
- Makes a negative detection sit silently; only a positive detection sits and barks.
- Replaces text-only academy commands with visible obedience, distance recall and prop-based scent detection stages.

## 1.7.7-beta

- Normalizes punctuation and wake-word variants including K9, K-9, K nine, kay nine and canine.
- Adds natural command phrases and a transcription grammar prompt with zero temperature.
- Gives “sit down” explicit priority as the sit command instead of matching the generic lie-down phrase.
- Replaces the repeated high/jumping search animation with low-head sniff passes.
- Makes a negative search indication silent and returns the dog to the handler without barking.
- Moves the fetch ball from the side of the head to a configurable mouth offset for stock and custom dog models.

## 1.7.6-beta

- Forces TLS 1.2 for Groq/OpenAI transcription requests under the RPH .NET Framework host.
- Fixes the `Could not create SSL/TLS secure channel` failure confirmed in the runtime log.
- Synchronizes microphone writer, buffer and device cleanup to prevent the continuous-listening null-reference race.
- Removes GTA native cop state as an activation fallback because LSPDFR sets it before the player actually goes on duty.
- Makes LSPDFR's current-session duty message the sole activation authority and recognizes shutdown/off-duty variants.

## 1.7.5-beta

- Fixes the controller-ending `address cannot be zero` error caused by named native resolution in RPH.
- Reads LSPDFR's own current-session on-duty/off-duty messages as the authoritative cross-AppDomain duty signal.
- Uses a direct native hash and relationship group only as contained fallbacks.
- Prevents any duty-probe failure from terminating the AdvancedK9 controller.

## 1.7.4-beta

- Detects LSPDFR duty using GTA's native player cop flag, supporting EUP/freemode officers whose relationship group remains unchanged.
- Retains the `COP` relationship group as a fallback duty signal.
- Logs a throttled duty probe every fifteen seconds for direct troubleshooting without exposing sensitive configuration.

## 1.7.3-beta

- Prevents microphone and HTTP callback threads from invoking RAGE UI, natives or K9 commands directly; results are now marshalled onto the controller game fiber.
- Starts voice capture only after LSPDFR changes the player to the on-duty `COP` relationship group.
- Adds a ten-second retry delay after voice network/API failures instead of immediately reopening the microphone.
- Keeps the K9, HUD, LemonUI menus, voice capture and shortcuts disabled while off duty.
- Automatically closes the UI, stops listening and dismisses the K9 when the player goes off duty.

## 1.7.2-beta

- Reads the Groq or OpenAI voice key directly from `[Voice] ApiKey` in `AdvancedK9.ini`.
- Keeps `ApiKeyEnvironmentVariable` as an optional fallback when the INI key is blank.
- Never displays or logs the configured API key.
- Fits a fixed `ADVANCED K9` title to the compact LemonUI banner and moves the active section label into the matching header strip.
- Labels the release, installer, artifact and plugin metadata as beta.

## 1.7.1-beta

- Corrected LemonUI width units and added opaque EUP-style dark rows with a blue active selection.
- Reduced the menu to six visible rows with native scrolling.
- Detects vest components using both drawable and texture variation counts.
- Shows recognized vest texture names and queries texture counts against the resolved drawable.
- Breed preview replaces the active ped in place while preserving position and leash/follow state instead of dismissing and redeploying at the handler.

## 1.7.0-beta

- Replaced the custom menu renderer with LemonUI 2.2.0 for RAGE Plugin Hook.
- Added a visible voice microphone status and activation control to both menus.
- Corrected fetch-ball ground placement and attached the carried ball to the K9 head/mouth bone.
- Anchored the leash display to the handler's right hand and the K9 collar/neck bone.
- Reworked tracking into sustained scent-trail segments with low-head sniff indications and meaningful range.

## 1.6.0-beta
- Rebuilt from source; no binary patching.
- Added persistent command/profile UI, editable immediate voice wake word, runtime command registry and live VAD recognition.
- Added model-aware skin/equipment drawable and texture bounds with live previews.
- Added persistent training XP/certifications, health/injury/stamina/trust/statistics and field care.
- Added movable/scalable compact/expanded HUD and rear-seat vehicle loading fallback.
- Added stay, recall, area/vehicle search, release, guard, bark, enter/exit vehicle, inspect and first-aid commands.

## 1.5.0-beta

- Added the free PopcornRP Doberman add-on preset with three coat textures and retriever-replacement fallback.
- Added the UGURBABA Cane Corso preset using its documented Chop replacement.
- Preserved safe automatic vest detection for models that do not publish clothing components.

## 1.4.0-beta

- Added researched compatibility presets for the martinct Belgian Malinois/shepherd replacement and Bug's Mods Dalmatian add-on/husky replacement.
- Added Dalmatian add-on detection with automatic husky-replacement fallback.
- Added published Malinois/Dutch/black coat support and Dalmatian police, BCSO, trooper, fire, off-duty, service and medic vest choices.
- Added runtime drawable/texture bounds checks, automatic vest-component detection and v1.3 profile migration.

## 1.3.0-beta

- Added persistent breed, coat, vest style and vest-color selection through the kennel menu.
- Added support for custom vested add-on K9 models and configurable clothing components.
- Added handler-defined dog names as AI voice wake words, with “K9” retained as a fallback.

## 1.2.0-beta

- Replaced Windows speech recognition with push-to-talk AI transcription.
- Added configurable OpenAI and Groq providers with environment-variable API keys.
- Preserved the complete keyboard command menu for voice-free play.

## 1.1.0-beta

- Added persistent handler trust affecting obedience, response delay, detection, tracking and apprehension eligibility.
- Added explicit friendly-fire prevention and a continuous handler-attack emergency recall.

## 1.0.0-beta

- Initial source release with deployment, obedience, detection, tracking, non-lethal apprehension, fetch, care, leash, camera, academy and voice commands.
- Added optional Policing Redefined/Common Data Framework runtime integration.
## 1.6.2-beta

- Revives and restores critically injured persisted dogs when deploying.
- Keeps the command menu accessible even if the current K9 entity is incapacitated.
- Rebuilt the interface as a smaller ten-row menu with a compact footer.
- Added Up/Down navigation and instant Left/Right live preview for breed, coat, equipment, equipment texture and HUD options.
- Preserves the selected menu row after each live preview change.

## 1.6.1-beta

- Removed the nonfunctional cross-AppDomain reflection duty gate.
- Starts the K9 controller directly from the RPH plugin entry point.
- Corrected the kennel/profile shortcut documentation to Left Ctrl + U.
