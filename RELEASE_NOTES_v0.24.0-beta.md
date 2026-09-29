# AdvancedK9 v0.24.0 beta — changes since v0.23.3

Build 866 (AdvancedK9.dll 0.24.0.80). The v0.23.3 release contained the core K9 plugin and compatibility bridge. This beta adds the separate callout expansion and shared API, the AdvancedK9 props DLC, and the behavior and medical updates below. The callouts are experimental.

## Core features confirmed in the latest in-game test

- The retractable leash extends and shortens with handler/K9 distance. The loop follows the handler's left hand and the latch follows Rex's vest. Rex stays within lead range, and moving beyond range releases the leash rather than leaving a floating prop.
- Rex can enter the rear seat, turn and sit, then exit to the ground in a single jump.
- The medical voice request is spoken as “Dispatch, call EMS.”

## Kennels, canine behavior and props

- Added the optional AdvancedK9 props DLC: a larger kennel, dual food/water bowl and animated retractable leash. Kennel, bowl and leash colors can be selected independently; each station can save its own kennel style and color.
- The large kennel displays the assigned dog's editable name on its plate. Its resting position, sleep pose, doorway approach and exit use geometry specific to this kennel. The original doghouse retains its separate setup.
- Deployment and dismissal were revised to let Rex get up, move through the kennel doorway and settle without the earlier ground phasing and injury. Sit entry/exit, lying down/getting up, swimming, and care animations were also revised during the beta cycle.
- Leash development replaced the earlier disconnected rigid props with one animated span, aligned the hand loop and vest latch, added color control and animation diagnostics, and capped the lead at 3.50 m. The final positioning and retraction were confirmed in game.
- Vehicle travel was revised around the rear door: approach and facing, jump start, seat landing, turn to sit, and one grounded exit. The final entry and exit were tested in game with the known entry pose quirk below.

## Core medical and dispatch work

- Added controlled-bite and released-patient safeguards to keep a bitten suspect in place while medical attention or restraint is pending, plus a native K9 contact presentation and nonlethal injury handling.
- Revised EMS dispatch and patient-position handling after K9 apprehension. The two EMS responders and rear-bag sequence received development fixes; callout-specific medical outcomes still need broad tester coverage.
- Added mobile veterinary care and additional first-aid recovery protection for Rex. The dedicated apprehend shortcut and long-distance Recall were refined.
- Changed the spoken EMS request to address dispatch. This wording was confirmed in the latest test.

## Experimental callout expansion

- Added `AdvancedK9.API.dll` and a separate `AdvancedK9.Callouts.dll`, with the API stored below the LSPDFR scan directory so it is treated as a dependency rather than an executable plugin.
- Registered exactly three tester callouts: **Missing Vulnerable Teen**, **Fugitive Trail from an Abandoned Vehicle**, and **Armed Burglary Suspect Hiding**. The Ctrl+U callout menu now labels all three to match their actual registration.
- Developed scent assignment, tracking, tactical scene, apprehension, medical, custody and dispatch flows for these calls. These flows remain experimental; inclusion here does not certify every branch, location or third-party integration.
- Added automatic dispatch support and a menu path to request the three test callouts. Bank scenes and other work-in-progress scenarios are not among the three registered callouts in this package.

## Packaging and compatibility

- Bundled the props DLC, core plugin, compatibility bridge, separate callout assembly, versioned API, required voice runtime, default configuration, install guide and third-party notices in a direct-install ZIP.
- Preserved existing user settings by supplying `AdvancedK9.default.ini`. No third-party integration plugin is bundled or modified.

## Known beta limitation

- On vehicle entry, Rex may briefly land in a seated pose before standing, turning and taking his final seat. The tester accepted this for the beta; the entry clip still needs polish.

Follow `INSTALL.txt` for setup. For callout feedback, include the callout name, reproduction steps and relevant `RagePluginHook.log` excerpt.
