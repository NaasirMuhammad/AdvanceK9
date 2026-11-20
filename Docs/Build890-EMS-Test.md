# AdvancedK9 v0.24.1.8 — Build 890 EMS test

Test build, not the approved public release. Includes Build 889 NPCAI callout integration.

Changes:
- Retain the patient and stretcher across pursuit-handoff and boarding retries instead of removing the stretcher and clearing the lying pose.
- Align the animated torso to the configured stretcher surface, rather than positioning only the ped root.
- Reuse confirmed pursuit handoff instead of issuing it again at departure.
- Log the exact loading stage and alignment correction.

In-game validation required: bitten patient should lie on the mattress, move with the paramedic to the ambulance, remain assigned to hospital care during any delay, and depart inside the ambulance. Confirm police stop pursuing the transported patient. Send the RagePluginHook log if loading remains delayed.

Install over the previous test build with GTA closed. Do not replace your configuration unnecessarily. Existing stretcher height remains configurable under Medical/BitePatientStretcherPatientZ.
