# AdvancedK9 v0.24.1.1 — Build 882 apprehension test

This is a test build based on v0.24.1. It requires in-game confirmation before publishing as a confirmed fix. Main plugin and compatibility bridge must both report version 0.24.1.1. Restart GTA V after replacing both DLLs; do not hot reload the old bridge.

## Changes under test

- Moving-suspect approach uses measured progress, bounded route recovery and model-sized contact checks. Contact requires line of sight, compatible height and a suspect outside a vehicle. Names and coats do not select different behavior.
- One takedown animation per contact, retaining its final frame rather than replaying the entire takedown over an injured suspect. Reissuing apprehend while holding does not release or rebite the patient.
- Per-patient pursuit AI suppression and PR custom-pursuit exclusion during physical custody. Medical patients are not repeatedly registered as traffic-stop subjects. A PR stop is requested once after confirmed treatment.
- Continuous downed-pose maintenance at the patient's current position, without teleporting them back to the original bite position. Only one unresolved bite patient is supported: complete restraint or EMS handoff before apprehending a different suspect.
- Temporary controls are restored on completed restraint, medical transport, off-duty and unload. A bridge lease expires if the main controller stops refreshing it. Arrest detection yields the medical pose to LSPDFR's actual arrest process.
- Detailed approach and custody diagnostics for the next test log.

The existing v0.24.1 changelog describes the release baseline. These test changes are not yet certified working. Callouts remain experimental. Leash, kennel, vehicle and care behavior are outside this change.

## Test sequence

1. Confirm AdvancedK9.dll and AdvancedK9.LSPDFRBridge.dll both load as 0.24.1.1. Use the same fleeing-suspect scenario with Rex and the alternate shepherd coat/name.
2. Apprehend a moving suspect from a moderate distance. Check for immediate pursuit, limited circling, one takedown, and no repeating attack animation during a 20-second hold.
3. Say apprehend again while holding. It should acknowledge the existing hold without starting another bite or releasing it.
4. Release and wait at least 30 seconds. The untreated suspect should remain down without standing, fleeing, or snapping back to an old position.
5. Complete a real arrest and check that medical maintenance yields to the cuffing animation. In a separate test, request EMS and verify treatment and stretcher loading are unobstructed.
6. Repeat with PR absent if available, then test off-duty cleanup. Save the full RagePluginHook log and a clip from deployment through release/arrest.

If the final-frame dog pose looks unnatural, report that separately from whether the patient stays down. This build uses the stock takedown clip; it does not include a custom sustained bite animation.
