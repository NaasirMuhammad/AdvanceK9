# AdvancedK9 v0.24.1.3 — Build 884 contact transition test

Based on the confirmed Build 883 pursuit behavior. In-game confirmation of the new bite handoff is required. Restart GTA V after replacing both DLLs; main plugin and compatibility bridge report 0.24.1.3.

## Changes under test

- Uses the dog's head bone plus a short forward muzzle offset against a capsule around the locked suspect's lower body. This can recognize reachable body contact before the actor centres pass the old approximately 1.15 m gate.
- Checks contact every frame and sweeps recent muzzle movement relative to the moving suspect, avoiding a missed contact window at running speed. This is proximity geometry around bones, not a skinned-mesh collision test.
- Rejects stale samples, large position jumps, targets in vehicles, excessive root separation, different heights and blocked line of sight. Existing compliance/protected-target checks remain in place.
- Commits once and immediately clears the chase task before applying the existing nonlethal takedown. It does not wait for another approach after valid contact.
- Logs the selected contact mode, resolved bones and root separation at commitment. Models without the required head/pelvis bones retain the prior root-distance contact fallback.
- Build 883 chase speed, target lock, navigation recovery and cancellation behavior remain unchanged. Bite hold, release and medical custody retain the previous behavior.

## Test

1. Repeat the straight moving-suspect chase from the latest clip. Record the first close approach through the takedown. Rex should hand off to the bite on the first reachable pass instead of pausing and chasing again.
2. Repeat front, rear and side approaches; include a suspect changing direction close to Rex.
3. Check that nearby bystanders, vehicles, walls and different-height positions do not cause remote contact.
4. Verify one takedown, release, persistent medical hold and actual arrest/EMS handoff. Repeat with the alternate shepherd coat/name.
5. Send the complete RagePluginHook log and a clip showing the first approach. Automated geometry checks and compilation cannot certify animation smoothness inside GTA.

This is a test build. The baseline release changelog is unchanged, and callouts remain experimental. One unresolved bite patient is supported at a time; complete restraint or EMS handoff before another apprehension.
