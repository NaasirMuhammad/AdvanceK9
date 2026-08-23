# Changelog

All published AdvancedK9 versions are beta builds.

## 0.21-beta

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
