# AdvancedK9 v0.24.1

Maintenance update by mtennekten, covering changes since v0.24.0. See CHANGELOG.md for confirmed updates and VOICE_COMMANDS.md for the complete command reference.

## Confirmed apprehension update

Immediate deployment, continuous target-locked pursuit, first-contact bite transition, one-shot takedown and release/medical custody fixes have been confirmed in-game by the author. This refresh retains the v0.24.1 public version and incorporates the tested Build 884 behavior.

## Included

Core AdvancedK9 plugin, compatibility bridge, shared API, experimental callout expansion, portraits, default settings, required LemonUI and NAudio voice runtime assemblies, and the AdvancedK9 props DLC (large kennel, dual food/water bowl and retractable leash). External dog models and third-party integrations are not bundled.

## Known limitations

- One unresolved bite patient is supported at a time. Complete restraint or EMS handoff before apprehending another suspect.

- Follow hesitation can still occur, especially during direction changes, and can cause an automatic leash release. This release includes the latest reversal/progress recovery changes from Build 881, but their in-game result has not yet been confirmed. The 6m range does not guarantee a release-free walk or run.
- Vehicle entry can briefly finish seated before Rex turns and settles. Low-clearance vehicle/model combinations may still clip; saved seat calibration matters.
- Custom sniffing, positive-alert and low-clearance-exit animation work is deferred and is not advertised as a completed feature.

## Experimental callouts — exactly three active

- Missing Vulnerable Teen (the Lost Child scenario)
- Fugitive Trail from an Abandoned Vehicle (listed in game as “Fugitive Trail”)
- Armed Burglary Suspect Hiding

These scenarios remain experimental. Bank-robbery development is not an additional active registration. Their existing assemblies are included without new callout work in v0.24.1.

## Installation / upgrade

Close GTA V and RAGE Plugin Hook, then merge the ZIP contents into the GTA V folder. Follow INSTALL.txt, including DLC registration. Existing AdvancedK9.ini, profiles, rosters and saved vehicle/kennel settings are not supplied as replacements. The included AdvancedK9.default.ini supplies defaults for new installs. Keep a backup of your personal configuration before updating.

This package is prepared for publishing with the confirmed apprehension updates. Attach RagePluginHook.log when reporting a remaining issue.

