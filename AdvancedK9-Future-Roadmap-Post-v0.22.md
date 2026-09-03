# AdvancedK9 Future Roadmap — Updated Through v0.23.0-beta

This roadmap tracks the original feature plan and all completed work through AdvancedK9 v0.23.0-beta.

Status key:

- ✅ Implemented through v0.23.0-beta
- 🟨 Partially implemented; expansion remains
- ⬜ Not yet implemented

Current verified baseline:

- AdvancedK9 core assembly `0.23.0.0`
- LSPDFR companion bridge assembly `0.23.0.0`
- Windows build pending for the current tracking and patrol-progression update
- Release branch `codex/advancedk9-v0.23.0-beta` (current update pending verification)
- Release artifact `AdvancedK9-v0.23.0-beta.zip`
- PR #18 remains open and unmerged
- Drag-and-drop updates preserve an existing `AdvancedK9.ini`

## Completed in v0.22-beta

### ✅ ~~1. Building-search system~~

Implemented as a six-sector building search. The K9 clears sectors, alerts with a bark, sits, and holds when a subject is located. Building Search never automatically becomes an apprehension.

### ✅ ~~2. K9 warning and deployment sequence~~

Implemented with the verbal police K9 warning and randomized surrender, freeze, flee, or fight reactions. A surrendered suspect is protected from subsequent K9 apprehension.

### ✅ ~~4. Realistic trail loss and reacquisition~~

Implemented with recorded pedestrian scent points, trail degradation, possible trail loss, and the Reacquire Trail command. The dog performs a three-point casting pattern before recommitting to the recovered trail.

### ✅ ~~7. Tactical suspect warning before apprehension~~

Implemented as a separate K9 Warning command. The dog warns and holds while apprehension remains a separate handler decision requiring an aimed target.

### ✅ ~~11. Deployment and bite report~~

Implemented through `K9DeploymentReports.csv`, including:

- Date, time, and location
- Handler and K9
- Deployment type and reason
- Scent source
- Warning status
- Track duration and distance
- Bite duration
- Suspect outcome
- K9 injury
- Final disposition

## Completed in v0.22.1–v0.22.8-beta

### ✅ ~~Compact Glass Tactical HUD and contextual UI~~

- Replaced the fixed legacy overlay with one compact lower-right Glass Tactical card.
- Added normal, collapsed, active-search, and attached amber-alert states.
- Uses real K9 health, stamina, handler distance, current command, behavior, search state, and actual specialty result.
- Every displayed field is optional. Position, scale, opacity, anchoring, automatic collapse, units, portrait, alerts, and search display can be edited in game.
- Added cached custom-profile, model, breed, and final badge/silhouette portrait fallbacks without per-frame image capture.
- Search status remains inside the K9 card; the obsolete bottom-screen `VEHICLE SEARCH` text is removed.
- Search behavior displays `SEARCHING` while active instead of incorrectly remaining `FOLLOWING`.
- Detection alerts attach above the card and use the actual returned narcotics, weapons, or explosives result.

### ✅ ~~Menu and notification cleanup~~

- Reorganized the profile menu into Identity & Appearance, HUD & Display, Vehicle Seat Configuration, Profile/Health/Certifications, and Voice submenus.
- Kept command categories compact and scrollable, with menus closing after action selection.
- Voice-key instructional text and general action notifications are off by default.
- Errors, warnings, search outcomes, and K9-produced positive/negative results remain visible in quiet mode.

### ✅ ~~Optional PR/CDF and Stop The Ped compatibility bridge~~

- Added the LSPDFR-side `AdvancedK9.LSPDFRBridge.dll` using the correct `Plugin.Initialize()` lifecycle.
- Publishes Policing Redefined, CommonDataFramework, and Stop The Ped availability and active entity state across AppDomains.
- Supports active pedestrian/vehicle selection, restrained and surrendered suspect protection, pursuits, vehicle bailouts, exact-entity search requests, K9 indication sharing, arrest handoff, and compatible service requests.
- Retains safe standalone behavior when another plugin or public API is unavailable.

### ✅ ~~Deterministic integrated vehicle-search results~~

- Removed the old random positive/specialty fallback whenever PR/CDF is active.
- Requests the exact searched vehicle by GTA handle and maps returned inventory to the correct trained odor.
- Handgun, pistol, rifle, ammunition, and related firearm terms map to Weapons rather than Narcotics.
- Missing or unusable integrated inventory produces no invented certified odor.
- General vehicle search respects the dog’s completed narcotics, weapons, and explosives certifications.

### ✅ ~~Automatic care and corrected interaction presentation~~

- Bathroom behavior is automatic and removed from verbal commands and HUD statistics.
- Feeding and watering place bowls in front of the K9 without forcing the handler into the old bending animation.
- Removed the incorrect gardening tool from petting.
- Care statistics and individual HUD rows remain optional.

### ✅ ~~Safe dismissal and animal-style vehicle entry~~

- Replaced ambient `Ped.Dismiss()` behavior with guarded cleanup and hard deletion, preventing dismissed K9s from behaving like human NPCs or stealing police cruisers.
- Clears tasks, vehicle/leash/seat attachments, blips, persistence, ownership, and combat state during safety cleanup.
- Replaced the visible human-skeleton vehicle-entry task with a dog-style rear-seat jump and calibrated seated pose.
- Custom/add-on dogs use a brief safe fallback when the animal animation dictionary is unavailable.

### ✅ ~~Station kennel pickup, return, and map system~~

- Added physical exterior doghouses and labeled K9 kennel map blips at supported stations.
- Normal deployment requires K9 pickup at a kennel; normal dismissal requires return to a kennel.
- Physical doghouses stream only when the handler approaches, after local collision has loaded; distant props unload while map blips remain available.
- Existing `AdvancedK9.ini` files are preserved during updates; new settings ship through `AdvancedK9.default.ini` and safe in-code defaults.
- Removed the persistently inaccessible Brook Trail and FIB locations in v0.22.8 and retained only usable kennel/blip entries.

### ✅ ~~LemonUI runtime compatibility correction~~

- Compiles against LemonUI.RagePluginHook 2.2.0 and packages the tested RPH DLL.
- Removed the version-sensitive direct `NativeMenu.MouseBehavior` setter that caused `MissingMethodException` on older compatible runtimes.
- The release ZIP keeps the RPH plugin in `Plugins`, the LSPDFR bridge in `Plugins/LSPDFR`, and does not replace `LSPD First Response.dll`.

## Completed in v0.23.0-beta — approved top five priorities

### ✅ ~~3. Article-based scent identification~~

- Identifies scent from an aimed world article, a recently occupied vehicle, an active-pursuit article, a nearby single-owner article, or a person.
- Rejects ambiguous or ownerless articles instead of assigning the wrong subject.
- Keeps apprehension separate from the scent-collection and tracking decision.

### ✅ ~~5. Track-direction indication~~

- Performs a visible physical direction indication before the K9 begins moving and after trail reacquisition.
- Records body line, cardinal direction, heading, and the selected route in the incident log.
- Retains low-head acquisition, recorded-trail routing, sustained tracking segments, and casting behavior.

### ✅ ~~6. Perimeter and containment commands~~

- Adds separate Hold Perimeter and Contain Suspect commands.
- Hold Perimeter patrols a handler-centered ring.
- Contain Suspect maintains a moving, non-bite containment ring around an identified suspect.
- Both commands can reduce their radius while retaining an attached working leash.

### ✅ ~~8. Automatic pursuit integration~~

- Extends pursuit recognition and bailout tracking to native LSPDFR pursuits as well as PR/STP-compatible pursuits.
- Assigns vehicle-bailout scent and automatically starts the recorded trail when a deployed, available K9 is present.
- Preserves surrendered, restrained, arrested, transported, and officer safety interlocks.

### ✅ ~~9. Handler-down protection~~

- Emergency-deploys the K9 from the vehicle when the handler goes down.
- Engages only an immediate non-officer attacker; otherwise the K9 guards and barks beside the handler.
- Requests medical service when a compatible provider is available.
- Keeps the response configurable and protected by existing target-safety rules.

## Partially implemented — future expansion remains

### 🟨 Further scent-article expansion

Implemented in v0.23.0-beta:

- Direct person and handler-aim scent assignment
- Recently occupied and pursuit-bailout vehicle scent
- Aimed world articles, active-pursuit articles, and nearby single-owner articles
- Ambiguous/ownerless article rejection

Still to add:

- Vehicle seat- or door-specific scent collection
- Dropped clothing
- Weapons
- Blood
- Personal property
- Last-known-location scent pads
- Different quality values for each article type

### 🟨 Further track-direction realism

Implemented through v0.23.0-beta:

- Initial low-head scent acquisition
- Recorded-trail direction selection
- Sustained running segments
- Three-point casting during reacquisition
- Physical direction indication before movement and after reacquisition
- Body-line, cardinal-direction, heading, and incident-log reporting

Still to add:

- Visible full-circle direction testing at the beginning of every track
- Training-, trust-, fatigue-, and weather-based wrong-direction chances
- Strong leash-pull/body-language indication toward the selected route

### 🟨 Further perimeter and containment expansion

Implemented through v0.23.0-beta:

- PR/STP perimeter-unit request when a compatible API is exposed
- Existing Guard command
- Hold Perimeter handler-centered patrol ring
- Contain Suspect moving non-bite ring
- Working-leash retention and reducing containment radius

Still to add:

- Hold This Corner
- Watch This Door
- Cover This Alley
- Guard This Vehicle
- Saved containment points
- Multiple simultaneous containment positions

### 🟨 Further pursuit lifecycle expansion

Currently implemented:

- PR/CDF/STP pursuit-suspect recognition through the LSPDFR companion bridge
- Vehicle-bailout detection for integrated pursuits and callout-created pursuits exposed through those systems
- Automatic bailout scent and pursuit-subject assignment
- Recorded foot-trail collection
- No stop or detention requirement
- `Apprehend` can send the K9 after the identified bailout suspect when the handler has a valid aimed/bridge target and the suspect is not surrendered, restrained, arrested, transported, or otherwise protected
- Native LSPDFR pursuit fallback, bailout scent assignment, and automatic recorded-trail deployment

Still to add:

- Lost-visual event handling
- Hidden-suspect state recognition
- Suspect vehicle-change tracking
- Pursuit-ended cleanup
- Arrest-completed cleanup

### 🟨 10. K9 injury evacuation

Currently implemented:

- K9 health and injury states
- Serious-injury removal from service
- Field first aid
- Veterinary treatment
- Injury incident logging

Still to add:

- Handler carry or assisted-walk animation
- Manual K9-down emergency command
- Emergency loading sequence
- Veterinary transport drive or escort
- Timed rehabilitation period

### 🟨 14. Explosive-detection safety response

Currently implemented:

- Independent explosive-detection certification
- Bomb-squad request through compatible PR/STP APIs
- Negative searches remain silent

Still to add:

- Silent positive bomb indication instead of barking
- Automatic recall to safety distance
- Explosive perimeter marker
- Configurable evacuation radius
- Search lockout around the suspected explosive

### 🟨 19. Simultaneous multiple detections in one search

Currently implemented:

- Reads and classifies every supported odor category exposed by the exact PR/CDF inventory instead of stopping after the first match.
- Can classify every certified odor present in the same target inventory rather than stopping after the first positive category.
- Preserves certification gating, excludes replicas/documents/permits, and avoids duplicate category results.
- Retains the authoritative item list for normal officer-search and NexusMDT reconciliation without disclosing contraband early.

Required final behavior:

- One search can return a combined positive hit for Drugs + Weapons, Drugs + Explosives, Weapons + Explosives, or Drugs + Weapons + Explosives.
- The K9 performs one clear multi-hit indication sequence for the same person, vehicle, building, bag, package, or article; the handler does not need to start separate searches.
- The Glass Tactical HUD shows every detected certified category together in one result, followed by a concise combined summary.
- Exact alert location for each odor when search evidence markers are implemented.
- Configurable physical indication pattern for two-category and three-category hits without confusing a negative, single hit, or explosive safety alert.
- Individual incident-log and deployment-report category entries grouped under one shared search event.
- Safe explosive-first sequencing: silently indicate explosives, recall to a safe distance, and lock out continued close searching before presenting narcotics or weapons results.
- Person, vehicle, building, baggage, package, and article support using the same duplicate-safe multi-detection rules.

### 🟨 17. Shift and kennel lifecycle

Currently implemented:

- On-duty-only K9 deployment and UI
- Field inspection
- Equipment inventory and restocking
- Vehicle loading and saved seating
- Persistent care and training data
- Physical station doghouses with labeled map blips
- Kennel-only normal pickup and return
- Nearby-only prop streaming after local collision loads
- Hard cleanup on off-duty, unload, or emergency dismissal
- Existing user INI preservation during drag-and-drop updates

Still to add:

- Guided pre-shift checklist
- Guided end-of-shift checklist beyond the existing manual kennel return
- Mandatory post-shift feeding and watering option
- Saved duty hours
- Shift deployment summary

### ✅ ~~20. Patrol-earned training XP~~

Implemented in v0.23.0-beta:

- Deployed K9s can receive randomly sized general-training XP from successful real patrol commands.
- Level-based base ranges are 0–10 XP at Levels 1–2, 0–20 XP at Levels 3–4, and 0–30 XP at Level 5.
- Every awarded patrol roll receives the approved 50% live-action markup.
- A patrol reward also grants a separate random 1–3 confidence; genuine hesitation or disobedience can randomly remove 1–3 confidence.
- Command farming is permitted. The first two repeated successful uses retain full reward potential, the third and later identical commands receive progressively smaller XP.
- From the fifth continued nonproductive repetition, an escalating random penalty can remove general-training XP and 1–3 confidence.
- Genuine active Search and Track work is exempt from repetition penalties.
- Reward and penalty notifications and diagnostic log entries show the command, XP, confidence, repeat count, and reduction factor.

## Remaining unimplemented roadmap

### ⬜ 12. Search evidence markers

After a positive indication, place a temporary marker at the exact door, trunk, wheel, seat, bag, person, or ground location where the K9 alerted. The marker should be removable and recorded in the incident log.

### ⬜ 13. Vehicle-specific search behavior

Create different search paths for:

- Sedans
- SUVs
- Vans
- Trucks
- Motorcycles
- Open-bed pickups

Normal vehicles can retain four-corner searches, while large or unusually shaped vehicles can use additional checkpoints.

### ⬜ 15. Water and obstacle tracking

Trails crossing streams, fences, alleys, railroad tracks, construction sites, steep terrain, or heavy traffic should affect trail quality, running speed, and direction checks.

### ⬜ 16. Multiple persistent K9 profiles

Create a kennel roster with multiple dogs, each retaining its own:

- Name and breed
- Coat and vest
- Vehicle seat settings
- Certifications
- Health and injuries
- Trust and care needs
- Equipment
- Training progress
- Deployment statistics

### ⬜ 21. BLR, PD Comp, and Damage Tracker Framework compatibility

Add optional, independently detected compatibility adapters for BLR, PD Comp, and Damage Tracker Framework without making any of them required dependencies.

Planned integration behavior:

- Share the active K9, handler, suspect, vehicle, pursuit, search, apprehension, injury, and deployment state only through documented public APIs or a versioned AdvancedK9 bridge contract.
- Allow BLR-originated supported patrol or incident context to reach AdvancedK9 without duplicating subjects, searches, pursuits, or reports.
- Synchronize supported PD Comp interaction and subject state so surrendered, restrained, arrested, transported, or otherwise protected people remain ineligible for K9 apprehension.
- Publish handler, suspect, and K9 damage events to Damage Tracker Framework and consume supported injury-state updates for first aid, handler-down protection, K9 evacuation, veterinary treatment, and incident reporting.
- Preserve AdvancedK9 as the authority for K9 behavior, certifications, detection results, safety interlocks, XP awards, and deployment reports.
- Detect each framework at runtime, isolate adapter failures, and retain full standalone operation when a framework is absent, disabled, outdated, or exposes no compatible public API.
- Add per-framework configuration toggles and diagnostic logging without displaying routine compatibility noise during normal patrol.
- Do not bundle, replace, modify, or redistribute BLR, PD Comp, Damage Tracker Framework, or any third-party framework files.

### ⬜ 18. K9-specific callouts

Add a fully integrated callout expansion as part of the AdvancedK9 download. Keep the core gameplay systems and the LSPDFR callout lifecycle separated so AdvancedK9 can still load safely when LSPDFR is unavailable:

```text
Plugins/
├── AdvancedK9.dll
└── LSPDFR/
    └── AdvancedK9.Callouts.dll
```

The callout module should use a shared AdvancedK9 API for profiles, certifications, voice commands, tracking, detection, searches, apprehension, Stop The Ped and Policing Redefined compatibility, dispatch updates, and incident reports.

#### Dynamic investigation and outcome system

Callouts must not choose a fixed ending when they begin. The callout should create a consistent set of people, locations, clues, and hidden facts, then allow the outcome to emerge from the live investigation. Outcome probabilities and available branches should update as the scene develops.

Outcome drivers should include:

- Evidence discovered, contaminated, destroyed, or missed
- Witness accuracy, cooperation, conflicting statements, and new information
- Scent age, quality, weather, terrain, obstacles, and cross-contamination
- The deployed dog's certifications, training level, trust, fatigue, health, and care status
- Suspect awareness, fear, intoxication, injuries, weapons, criminal history, and escape opportunities
- The handler's warnings, perimeter placement, tactical positioning, use of cover, and K9 commands
- Stop The Ped or Policing Redefined interactions, searches, identification, questioning, arrests, and transport
- Time elapsed, backup arrival, traffic, civilians, changing vehicles, and environmental hazards

Recommended procedural flow:

1. Dispatch provides limited initial facts rather than the solution.
2. The system creates hidden facts and evidence that remain logically consistent, but does not lock in the ending.
3. The player's investigation reveals, misses, or misinterprets parts of the scene.
4. Suspects, witnesses, and civilians react to the player's actions and developing police presence.
5. K9 tracking, detection, article searches, and apprehension change the available choices and outcome probabilities.
6. The incident resolves from the resulting world state: evidence, behavior, tactical decisions, elapsed time, and K9 performance.

Possible emergent resolutions include a safe recovery, mistaken or false report, voluntary surrender, foot pursuit, vehicle switch, hiding or barricade, armed confrontation, contraband recovery, a cold or reacquired trail, suspect escape, arrest by the handler or backup, and medical or veterinary response. Randomization must never create unexplained contradictory evidence.

#### Recommended future callout roster

| Category | Callout | Example dynamic branches |
| --- | --- | --- |
| Tracking and rescue | Lost child | Wandered away, hiding in fear, injured, located with another adult, or credible abduction evidence develops |
| Tracking and rescue | Missing vulnerable adult | Confused pedestrian, medical emergency, entered a vehicle, returned home, or foul-play evidence appears |
| Tracking and rescue | Fugitive trail from an abandoned vehicle | Immediate foot trail, vehicle change, residential hiding place, surrender, ambush, or trail temporarily goes cold |
| Tracking and rescue | Injured hiker or wilderness search | Safe recovery, fall injury, animal hazard, misleading witness location, or deteriorating weather |
| Tracking and rescue | Evidence and article recovery | Discarded firearm, clothing, stolen property, blood trail, multiple articles, or contaminated search area |
| Tactical | Armed burglary suspect hiding | Building cleared, suspect already fled, hidden suspect, innocent occupant, multiple suspects, hostage, or armed confrontation |
| Tactical | Escaped prisoner | Surrender, stolen clothing, carjacking, accomplice pickup, barricade, or cross-terrain track |
| Tactical | Officer requesting K9 backup | Area search, felony stop, resistant suspect, ambush concern, false identification, or suspect already departed |
| Tactical | Vehicle-pursuit bailout | Driver and passenger split, weapon discarded, vehicle switch, injured suspect, perimeter containment, or escape |
| Tactical | Perimeter search after shots fired | Shooter located, firearm only recovered, innocent witness found, multiple shooters, wounded suspect, or no confirmed target |
| Detection | Roadside narcotics investigation | Clean vehicle, trained-odor alert, concealed narcotics, residual odor, medical substance, or coordinated trafficking lead |
| Detection | Suspicious vehicle weapons sweep | No find, firearm, stolen weapon, hidden compartment, ammunition only, or evidence linked to another incident |
| Detection | Bomb threat and explosives search | Hoax, harmless package, explosive material, viable device, secondary location, fleeing suspect, or evacuation escalation |
| Detection | Warehouse or package scent lineup | Correct package, cross-contamination, multiple targets, decoy shipment, employee involvement, or no trained odor |
| Detection | Firearm discarded during pursuit | Weapon recovered, alternate article found, second weapon, civilian disturbance, suspect doubles back, or evidence is moved |

Each callout should support replayable locations, suspect descriptions, witness reliability, evidence placement, scent conditions, and escalation behavior. Certification should matter: an uncertified dog must not provide a specialty detection result, while a certified dog can contribute through general search when the relevant odor is present.

Suggested configuration:

```ini
[Callouts]
Enabled=true
DynamicOutcomes=true
AllowStopThePedIntegration=true
AllowPolicingRedefinedIntegration=true
UseAdvancedK9Profiles=true
MinimumCalloutDistance=300
MaximumCalloutDistance=3500
```

## Updated recommended priorities

The next six highest-impact additions after v0.23.0-beta are:

1. Multiple simultaneous detections in one search, including any two odor categories or all three together
2. BLR, PD Comp, and Damage Tracker Framework compatibility through optional public APIs
3. Multiple persistent K9 profiles and kennel roster
4. Search evidence markers with exact alert locations
5. Vehicle-specific search paths
6. K9 injury evacuation, emergency loading, veterinary transport, and rehabilitation

Handler-line tracking/search navigation and patrol progression are now implemented for v0.23.0-beta and remain active tuning items during field testing.

The first controlled callout test set remains Lost Child, Fugitive Trail from an Abandoned Vehicle, and Armed Burglary Suspect Hiding. Continue full clothing, blood, weapons, property, and location-pad scent expansion alongside pursuit cleanup, water/obstacle tracking, explosive-safety procedures, and saved containment positions.
