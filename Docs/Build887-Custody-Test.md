# AdvancedK9 v0.24.1.6 — Build 887 suspect ownership test

Test build based on Build 886. Replace main 0.24.1.6, bridge 0.24.1.6, callouts 0.24.0.53 and the packaged AdvancedK9.API.dll together, then fully restart GTA V. The API assembly identity remains 0.24.0.0 for compatibility; its file version is 0.24.1.6. The published v0.24.1 release is unchanged.

## Changes awaiting in-game validation

- Each K9 deployment captures a suspect's handle, model and memory identity. Removed, replaced or zero-address entities retire that session. A stale fiber cannot reacquire a session after an external provider takes over.
- Target-specific control phases cover chase, bite, released medical hold, treatment, awaiting arrest, hospital loading/departure, external interaction, arrest and cuffs. Logs include episode, suspect, transition reason and observed health/restraint when available.
- The compatibility bridge observes real LSPDFR/PR interaction and arrest states. A treated patient is no longer automatically registered as a PR stopped pedestrian. PR arrest interception is not enabled by AdvancedK9. During provider handoff, an earlier resistance/combat action is not replayed.
- The core relinquishes its own freeze, writhe and event-blocking controls once a provider takes over, then stops repeating suspect posture tasks. It does not clear the provider's animation/task. Treated patients receive an initial compliance task rather than repeated hands-up commands through searches or the ped menu.
- Hospital loading defers while an external interaction/arrest animation owns the patient; after observed cuffs it can proceed without restarting the bite. The Build 886 hospital requirement and departure recovery remain.
- AdvancedK9 callouts read a fresh lease for the exact suspect, not Rex's overall state. They suspend that suspect's scripted outcomes/hiding/escape tasks and duplicate EMS requests while the K9 or medical owner is active. A bite on another person does not lock this suspect. Queued arrest/escape work rechecks ownership after waits. General-state automatic Release/Follow commands were removed from the shared custody flow.
- Invalid-address failures retire affected core maintenance instead of retrying patient writes every frame. Temporary bridge holds have a short expiry and identity-checked restoration.

## Compatibility limits

AdvancedK9 cannot stop arbitrary task loops inside an unrelated callout DLL. Its own pack now cooperates through the public, optional `SuspectControlApi.TryRead(handle, model, address, out lease)` contract. Other callout creators can use `lease.BlocksCalloutTasks` and `lease.MedicalOwner` to pause their own suspect work. A lease expires after five seconds without a core heartbeat. Public LSPDFR per-ped pursuit control and observed PR handoff remain the available fallback for external packs; universal external-callout compatibility is not claimed.

The previously reported game crash had no conclusive fatal stack. This build addresses the observed control conflict and invalid-address path; it is not proof that the unexplained crash is fixed. No suspect is resurrected or assigned a fake completed arrest to suppress a death message.

## Floating street props

The reported wood-like object is not yet identified. AdvancedK9 kennel props are protected against damage/breakage without changing their positions or dog alignment. Automatic diagnostics record nearby objects whose model names identify wood/planks/boards. When the object is visible, open **K9 Profile > Inspect nearby props** and provide the full RPH log plus a screenshot/clip. It records nearby model/hash/position/attachment and known persistent ownership. It does not delete unidentified world objects. Streamed-map/addon fragments or other mods may still require a separate asset fix.

## Focused regression test

1. Start an AdvancedK9 suspect callout and deploy Rex before its scripted suspect sequence finishes. Confirm the same suspect is chased and bitten without a later hiding/flee animation overwriting the bite.
2. Release and request EMS. Confirm only one medical response owns the patient; on-scene treatment or hospital transport completes as applicable.
3. Open PR's Ped Stop Menu, search, then arrest after a bite. Confirm the suspect is not repeatedly forced into hands-up/writhe poses and no false completed-restraint note appears before actual restraint.
4. Begin an external arrest during the K9 approach or hold. The dog should disengage from that suspect, and the ownership log should show the handoff without reactivating the chase/bite.
5. Repeat with the external callout involved in the previous incident. Its own script may still need the optional lease integration if it continuously retasks the suspect.
6. Repeat ordinary unbitten PR interaction and the confirmed standalone apprehension/EMS path.
7. If the floating object reappears while walking, use **Inspect nearby props** while standing near it and include the image and log.

Build 886's EMS test guide and the voice command guide remain included. Automatic Nexus incident/report linking remains unverified; manual incident linking and the durable outbox remain available. Callouts remain experimental.
