# AdvancedK9 Build 877 — core consistency checks

Internal version 0.24.0.91. This is a focused test build after the released v0.24.0 beta baseline; it is not yet certified in game.

1. Start an area, vehicle and building search. During navigation, sniffing and the final indication, issue Follow, Stay, Sit, then a different search. The latest accepted task must remain in control; old workers must not restore a previous route, sit or alert later.
2. Start a track and interrupt it with Sit, Stay, Recall, a search and Enter Vehicle. Repeat while Rex is turning or checking direction. Test Inspect, camera and EMS requests during tracking: these must leave it active.
3. Sit -> Follow/Recall must finish sit_exit before walking. Lie -> Follow must finish getup_l/getup_r before moving. Sit -> Lie remains the existing direct transition. Idle variation must not restart a sit/get-up transition.
4. Issue Sit, Recall and Sit rapidly. Foreground actions execute one at a time and only the latest pending request survives; Rex must not overlap animations.
5. Enter/exit the tested sedan and SUV, then deploy/dismiss at both kennel types. Queue Recall during a transition. Check door, collision, height, frozen state and final positioning against the previous working build.
6. Reject a command through medical eligibility or hesitation while Rex is doing other work. His current task must remain valid. Confirm apprehend still works from keyboard, controller and voice.
7. Go off duty or unload during search, track and a transition. No old worker should task a replacement/deleted dog or resume after redeployment.

Record the command sequence, vehicle/dog model, visible result and RagePluginHook.log. Look for the task-owner-superseded message on interruptions. Windows compilation alone does not validate animation appearance or native timing.

## Kennel regression check

At the large kennel, repeat deploy -> Sit/Lie/Follow -> return at least ten times. Rex must walk in, turn, settle into sleep and remain there. Queue Recall during return, then deploy again. Repeat at the original doghouse. Confirm one resident, correct floor height and no sleep-maintenance correction during the active exit sequence. Each successful return should log `kennel return completed`; it must not be followed by self-cancellation before the sleep pose is assigned.

## Additional Build 868 checks

- Standing follow: start/stop walking, turn, jog and sprint while leashed and unleashed. Rex should respond promptly without repeated task resets or ordinary-walk leash detachment. Sit/lie getup must still finish first.
- Feed and water: one care loop, approximately 4.2 / 3.2 seconds plus posture transitions, bowl cleaned up after completion or interruption.
- Load/unload both 16 FPIU and ambient Explorer, both rear doors where available, flat and sloped pavement. Check final seat height, roof clearance and one grounded exit. Keep existing saved calibrations.
- EMS was not retested by the user; camera testing was partial. Neither is newly confirmed by this build.

## Build 869 vehicle regression checks

- Repeat three entry/exit cycles on 16K9, SWATCHGR and LAPD1. Check the entire jump, final turn, seated height, roof clearance and grounded landing before follow resumes.
- Repeat on the ambient Explorer and both rear doors where available; try a curb and a slope. Saved seat positions must remain unchanged.
- Confirm only one jump clip per action. Log should report vehicle body reference and per-clip body compensation; keep log and video together.
- The brief seated finish of get_in remains; no second sit_enter should replay on arrival.
- Kennel return was confirmed by the user in Build 868 and its implementation is unchanged here.

## Build 870 targeted checks

- Entry must visibly play get_in on SWATCHGR and LAPD1, then 16K9 and ambient Explorer. Calibration failure must not silently warp the dog into the car.
- Exit must travel sideways from the occupied seat, through the selected door, then land at ground clearance before follow resumes. Test right and left rear doors, road, curb and slope.
- Logs must include standing reference before entry, body bone lookup or a specific calibration failure, observed=True get_in playback and the doorway landing coordinate.
- Return to kennel remains the Build 868 implementation; do not recalibrate kennel positioning.

## Build 871 targeted follow checks

- On an open sidewalk, walk continuously for 60 seconds while leashed; repeat turns, walk/jog/sprint changes and handler stops. Rex should not pause and let normal walking exhaust the leash.
- Repeat unleashed. Confirm he can stop when the handler stops; test a real obstacle without teleporting or clipping through it.
- Sit and Lie Down while following: the latest command must hold, and get-up must finish before following resumes. Repeat the confirmed vehicle-search interruption test; no delayed result notification.
- Capture video and RagePluginHook.log. `follow pause recovery` should identify measured stalls rather than appear repeatedly during smooth movement.
- Leash endpoints, length and animation assets remain unchanged; out-of-range safety release remains for actual unreachable separation.

## Build 872 targeted checks

- Repeat the failed Build 871 sidewalk route, both leashed and unleashed. Walk/jog/sprint, turn and stop. Dog must keep moving without regular stops; must settle when handler stops.
- Test a fence or parked vehicle between dog and handler; seeking must not clip/teleport through the obstacle. Capture follow motion diagnostics plus any recovery.
- Apprehend a valid active suspect nearby and at distance. Approach should start without stationary combat barks; controlled takedown only at close, clear contact. A suspect behind a wall or above/below the dog must not be taken down remotely.
- Cancel approach with Sit/Follow and test suspect surrender before contact. Confirm no stale bite; repeat nonlethal hold, Release and Dispatch call EMS.
- Start apprehension from Sit/Lie Down: get-up must finish before running. Vehicles and kennel routines require a short regression check.

## Build 873 targeted checks

- Repeat continuous walking, turns, stops and walk/jog/run changes both leashed and unleashed. Rex should follow without periodic route resets or ordinary-walk leash release.
- Capture the full RagePluginHook.log if a pause occurs. Compare `follow task assigned`, `follow pause status` and `follow motion`; status 0/1/2 is waiting/performing/dormant, 3/7 is vacant/finished.
- Pace changes alone must not produce assignment logs. Finished-task assignments should occur only when movement is still needed; normal handler stops must not produce a restart loop.
- Test a real obstacle and interrupt Follow with Sit/Lie/search. Get-up must complete before following, and the newest command must hold.
- Apprehension was confirmed working by the user in Build 872 and its implementation is unchanged. Perform a short regression check alongside vehicle and kennel transitions.

## Build 874 targeted checks

- Walk continuously for 60 seconds leashed; turn 90/180 degrees, stop/start and change to jogging/sprinting. Repeat unleashed. Rex must remain responsive without repeated braking or ordinary-walk leash release.
- Walk past a parked vehicle/fence and around a corner. Navmesh avoidance must remain functional; no new position or velocity warps are introduced for ordinary following. A genuinely unreachable separation retains safety detachment.
- While leashed, Stand -> Sit -> Stand -> Lie -> Stand. The latch must stay at the vest hook through the whole transition and settled pose; the handler loop must remain at the left hand. Repeat after kennel deployment, breed change and redeployment.
- Check `leash body anchor` for a nonzero supported boneId. A supported=False result means the skeleton fallback still uses the standing root offset and needs model-specific inspection.
- Capture video and the full log. Moving-destination assignment logs are expected; `follow motion` includes task, route and remaining slack. Compare physical hesitation with those measurements.
- Interrupt follow with Sit/Lie/search. Get-up must complete before navigation resumes. Apprehension, vehicle entry/exit and both kennel types need a short regression check; their implementations are unchanged.

## Build 875 targeted checks

- Install the included updated DLC and restart GTA. Walk/turn/jog leashed; confirm both endpoints stay attached as separation crosses 3.5m and approaches 4.5m. Retraction uses the 6m asset range; patrol safety release remains at 4.5m.
- Stand -> Sit -> Lie -> Stand while leashed. Standing/sitting placement must remain correct and the lying latch must stay on the vest hook instead of bare neck/skin. Record a close side view if correction is needed.
- Say Rex bark/speak or use menu Bark. Confirm visible enter/idle/exit, audible bark and no indefinite loop. Following resumes only after completion; bark from rest must finish get-up first.
- Interrupt bark with Sit/Follow and confirm the newest command holds. Test internal positive alerts for audible dog vocalization without new full-body task replacement.
- Short regression: confirmed follow, apprehension, vehicle and both kennel types. Check animated leash color variants after restart.

## Build 876 targeted checks

- On duty with Rex undeployed, stand at his kennel, hold the configured push-to-talk key and say Deploy K9. Confirm recording/recognition and natural kennel deployment. Say Kennel up to return, then deploy by voice again.
- Other dog commands while undeployed must not create a dog away from a kennel. Off-duty voice activation remains blocked by duty lifecycle.
- Compare walk -> jog -> sprint -> stop, straight and turning, while leashed. Check faster catch-up without sliding/overshoot; genuine separation beyond 4.5m still releases. Repeat Sit/Lie get-up before running.
- Capture a close side-view clip of Rex standing and lying with the vest hook and latch visible, plus full log. `leash settled pose` and mesh endpoint lines record each pose even if span changes are small. Lying placement is not yet fixed.
- Bark, vehicle/kennel transitions and apprehension need only a short regression check; their implementation and the 6m asset are unchanged.

## Build 877 targeted checks

- On the pictured shepherd/Malinois, attach leash while standing, Sit, Lie, then Follow. Capture a close side view matching the prior screenshot. The lying latch should move back onto the vest hook, with no final offset change to standing or sitting.
- Repeat Lie -> Sit and Lie -> Follow from both left/right handler positions so both get-up clips are covered. The correction should blend out as Rex rises, not jump at the command boundary.
- Check settled-pose diagnostics: lyingBlend should settle at 1 while lying and 0 while upright; compare hook and mesh latch positions. The 0.18m estimate requires in-game visual confirmation and may need finer calibration.
- Short regression: verbal deployment/return, bark and walking/running follow. No new DLC installation is required if the Build 875/876 6m pack is already installed.
