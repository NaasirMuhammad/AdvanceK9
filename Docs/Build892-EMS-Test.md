# AdvancedK9 v0.24.1.10 — Build 892 EMS transport test

Includes the previous stretcher, recovery-key and NPCAI updates. In-game validation pending.

Changes:
- Once the patient is secured on the stretcher, medics begin moving to the ambulance immediately. Pursuit-handoff acknowledgement is no longer a prerequisite for carrying, loading or ambulance departure.
- Pursuit handoff retries in a separate worker while EMS continues. Other pursuit suspects are preserved. A provider/API failure is logged; an unsuccessful handoff is never reported as confirmed.
- Holding Backspace for 1.5 seconds brings the existing responding ambulance and crew to the closest clear road position near the handler, rather than near the patient. A small clearance buffer protects the handler. No duplicate crew is created.

Test: request EMS, allow hospital transport, and leave the police scene active without Code 4. Confirm the secured patient immediately moves to the ambulance and the crew departs. Check whether police continue following the ambulance and provide the RagePluginHook log if so. Background handoff still depends on available provider APIs; unsupported per-patient removal in a multi-suspect pursuit remains logged.

For Backspace recovery, stand in a clear street near the scene and hold the key. Confirm the ambulance appears close to your position with room to exit. Treatment and stretcher loading are not interrupted by the recovery shortcut. Existing INI settings remain compatible.
