# AdvancedK9 v0.24.0 beta

Build 866 (AdvancedK9.dll 0.24.0.80). This beta packages the core K9 features confirmed in the latest in-game tester session. The three included callouts remain experimental.

## Confirmed working in the latest tester session

- Retractable leash extends and shortens with the distance between handler and K9. Its ends track the handler's left hand and Rex's vest attachment. Rex follows within the leash limit; moving beyond range releases the leash instead of leaving it floating.
- Vehicle entry and exit are usable: Rex reaches the rear seat and turns to sit, and exits in one jump to the ground.
- The medical voice request uses “Dispatch, call EMS.”

## Included K9 features and props

- Deploy and dismiss the selected dog at a station kennel; the large kennel has its own positioning and sleep/exit sequence. The original kennel retains its own setup.
- Assign a name to a K9 profile and display that name on the large kennel's plate. Choose kennel type and color independently for each station.
- The AdvancedK9 props DLC provides the large kennel, a dual food/water bowl, and the retractable leash. The kennel, bowl, and leash have independent color choices in the menu.
- Handler commands cover leash/follow, sit and rest, care, search and tracking, apprehension and release, and vehicle travel. Dog animations include entry and exit transitions where configured.

## Experimental callouts

These three are registered for tester feedback; their scenes, custody and dispatch outcomes have not been certified as complete:

1. Missing Vulnerable Teen
2. Fugitive Trail from an Abandoned Vehicle
3. Armed Burglary Suspect Hiding

The callout menu now shows these three entries and Back. The coordinate logging entry was removed, and the third entry's label now matches the actual armed burglary callout.

## Known issue

- Vehicle entry can briefly show Rex sitting on landing before he stands, turns and takes his final seat. This was accepted for the beta and remains a polish item.

## Install and feedback

Follow `INSTALL.txt` to place the plugin, callout expansion and optional props DLC. Existing settings are preserved by the supplied default INI. When reporting a callout issue, include the callout name, steps, game version and the relevant `RagePluginHook.log` excerpt.
