# AdvancedK9 v0.24.1.5 — Build 886 EMS / MDT test

Based on the confirmed Build 884 apprehension behavior and Build 885 reporting outbox. This is a test build, not a replacement for the published v0.24.1 release. Restart GTA V after replacing both main and bridge DLLs; both must load as 0.24.1.5 / Build 886.

## EMS changes awaiting game validation

- Uses the base-game `v_med_emptybed` wheeled medical gurney instead of depending on the unavailable `prop_amb_stretcher` names. `[Medical] BitePatientStretcherModel` can select an installed addon, with the stock gurney as fallback. `BitePatientStretcherPatientZ` controls mattress alignment (default 0.85).
- After treatment, pre-treatment health below 60 requires hospital care; the existing configurable hospital-transport chance also remains. A hospital assignment survives equipment/boarding failures. The patient is not labelled medically cleared because transport failed.
- Gurney retrieval, approach, loading and crew boarding are checked separately. A timer expiring does not count as reaching the patient or ambulance.
- Medics leave treatment scenarios, approach their respective front doors and receive explicit normal entry tasks for driver/front-passenger seats. One medic's failure cannot erase ownership of the entire crew. Failed attempts remain queued and retry.
- When all relevant entities are over 120 m away and off-screen, responder seats may recover without a visible teleport. Hospital loading is still required before departure.
- Hospital crews drive toward a nearby hospital. Empty crews drive back into service. Departure is logged only after the ambulance actually moves away with the required occupants. Transport notes do not claim hospital admission.
- K9 deployment/dismissal no longer cancels the separate EMS workflow. Going off duty or unloading the plugin releases its responders.
- Code 4 compatibility uses the exact fresh Nexus dispatch action/global-dismissal log messages observed in the supplied RPH logs. It does not treat transcripts or AI acknowledgements as commands. It signals pending crew recovery; active treatment and required hospital transport continue. This is a log-based compatibility fallback, not a verified Nexus event subscription. Keep Nexus/RPH logging enabled.

## MDT status

Bite, release, hospital requirement, EMS request, treatment, departure and cuff milestones remain in the durable outbox. Existing accepted events are not resent; existing stage filenames are preserved. The public incident-note writer remains `NexusMDT.Api.DispatchBridge.AppendIncidentNote(number, note)`.

Automatic incident linking still depends on Nexus exposing a supported public current-incident accessor at the bite. The bridge now recognises public current-incident/call object getters as well as number getters and logs the actual installed public incident/report API surface. No active number is invented, and old unbound bites are not assigned to later incidents. If no accessor exists, use **K9 Profile > Bite / EMS MDT reports > Link to report**. Full automatic association and inclusion in a report draft remain unconfirmed; the NexusMDT DLL or developer API documentation is needed to verify further integration. No Nexus database is edited.

## Focused test

1. Confirm the log loads main and bridge 0.24.1.5.
2. Temporarily set `[Medical] BitePatientTransportChance=100` in your existing AdvancedK9.ini and restart. Apprehend/release a suspect and request EMS. Watch gurney alignment, patient loading, both medics boarding and ambulance departure. Restore your preferred chance afterward.
3. Test an invalid configured stretcher model: the stock gurney must load and hospital transport must continue.
4. With a noncritical patient and transport chance 0, verify on-scene treatment followed by crew boarding/departure. Health below 60 still requires hospital care.
5. Say your normal Nexus Code 4 phrase after treatment, including during a boarding delay. Look for `AdvancedK9 EMS: Nexus Code 4 observed` and subsequent assigned-seat/departure logs. Repeat during treatment: it must not cancel necessary care.
6. Check the MDT incident after linking. Expect `API accepted` only after an actual writer call. `queued` or `Pending` alone does not confirm delivery.
7. Supply the full RPH log plus any gurney/loading/boarding clip. Keep existing kennel, leash and K9 vehicle animation settings unchanged.

The confirmed release changelog is retained separately; callouts remain experimental.
