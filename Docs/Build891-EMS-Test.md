# AdvancedK9 v0.24.1.9 — Build 891 EMS recovery test

Includes Build 890 stretcher changes and Build 889 NPCAI integration. In-game validation pending.

Hold Backspace for 1.5 seconds while AdvancedK9 EMS is driving to the bite patient to bring the existing ambulance and both medics to a nearby road position. No Ctrl modifier is needed. A tap does nothing; release before another attempt. This does not spawn a duplicate crew or skip treatment. Recovery is unavailable after medics begin exiting, during treatment, and during stretcher loading or transport. PR/STP-owned service vehicles remain managed by those plugins.

Nearby occupied vehicle/ped locations and unsuitable ground levels are rejected. If no clear position is found, clear space and retry.

Existing INI files work without edits. Optional settings:

```ini
[Keys]
EmsRecovery=Back

[Medical]
EmsRecoveryHoldMilliseconds=1500
```

Set EmsRecovery=None to disable. Hold duration is clamped to 750–5000 milliseconds.

Test a stuck en-route ambulance, a short tap, continued holding, and a hold during treatment. Confirm there is one ambulance, both medics exit normally, treatment continues, and stretcher transport still works.
