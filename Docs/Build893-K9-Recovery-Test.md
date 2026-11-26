# AdvancedK9 v0.24.1.11 — Build 893 K9 recovery test

Includes previous EMS transport, Backspace recovery and NPCAI changes. In-game validation pending.

- The downed-state monitor and regular vitals checks yield during K9 entity recovery, preventing the recovery command from cancelling itself.
- Medical restoration uses the living portion of GTA ped health. Deployment and vitals use the same percentage scale. A stabilized dog at 35% no longer receives terminal-range native health.
- Recovery must observe a living K9 for ten consecutive samples before success is reported. Failure retains the medical requirement.
- First aid consumes a kit only after entity stabilization is verified. Replacement during treatment does not reset the profile through normal deployment preparation.
- First aid still requires veterinary follow-up; it does not clear rehabilitation.

Test: on a downed K9, use Care & Medical > First Aid once. Confirm one recovery, a stabilized injury state, one kit consumed, and no immediate repeated DOWNED notification. Wait past the temporary protection period, then verify carry, veterinary care and rehabilitation. Test veterinary treatment from both downed and living injured states. Send RagePluginHook.log if treatment fails.
