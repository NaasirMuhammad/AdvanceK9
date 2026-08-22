# Changelog

All published AdvancedK9 versions are beta builds.

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
