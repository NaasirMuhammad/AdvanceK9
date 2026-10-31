# AdvancedK9 v0.24.1.2 — Build 883 continuous pursuit test

Test build based on Build 882. In-game confirmation is required. Restart GTA V after replacing both the main plugin and compatibility bridge; both report 0.24.1.2.

## Changes under test

- Apprehend bypasses the random trust/confidence response delay for every input source. Existing health, compliance, protected-ped and target checks still apply. Sitting/lying dogs finish their get-up transition.
- The first chase task is assigned immediately after target validation and any necessary get-up. The selected ped remains the chase target even when the handler stops aiming, changes direction, or loses sight of the suspect.
- Removed the gap-growth, projected-advance and overshoot triggers that repeatedly restarted an active task. Recovery now requires a sustained missing/finished task or three seconds without meaningful movement. Jumping, falling, ragdoll and get-up states receive time to finish.
- Removed the 25-second chase deadline. An available, noncompliant target remains assigned until contact, a new handler command or target/dog unavailability. Natural obstacles and GTA navigation still determine which routes can be traversed.
- A new handler command can supersede an ongoing chase. Reissuing apprehend starts a new explicitly selected deployment; the previous worker cannot resume later.
- Contact checks run every frame and trigger the existing one-shot nonlethal takedown once range, height, line of sight and target state permit it.
- Preserves Build 882 bite/medical custody changes. One unresolved bite patient is supported at a time; complete restraint or EMS handoff before another suspect is bitten.

The unchanged v0.24.1 changelog describes the release baseline. These pursuit changes are not yet confirmed working. Callouts remain experimental. Leash, kennel, vehicle and care changes are outside this test.

## Focused test

1. Deploy from standing on the same moving suspect. Record command, first movement and acceleration.
2. Keep chasing through straight roads, a corner and an obstacle detour. Stop aiming and let the suspect get ahead; Rex should remain committed to that suspect.
3. Let an active chase exceed 25 seconds. It should not revert to following because of elapsed time or increasing separation.
4. Issue recall, sit or lay down during the chase. The new command should take ownership and the old chase should never resume.
5. In a separate deployment, allow contact. Check for one prompt takedown, release, continued medical hold, and real arrest/EMS handoff.
6. Repeat with another name/coat of the same model. Provide the complete log and a clip including the command and chase.

Automated checks replay running/slow movement, obstacle detours, direction changes, real stalls, transient/finished task states, physical transitions and clock wrap. They validate recovery decisions; actual GTA movement and animation smoothness still need the game test.
