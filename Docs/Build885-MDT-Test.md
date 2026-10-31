# AdvancedK9 v0.24.1.4 — Build 885 Nexus MDT test

This is an integration test build based on the confirmed Build 884 behavior. The published v0.24.1 release is unchanged. Restart GTA V after replacing both main and bridge DLLs; both should report 0.24.1.4.

## Recorded milestones

One bite episode groups the dog's name, available patient/handler identity, UTC event time, location, deployment-to-contact and bite-to-release intervals, release, EMS request, actual treatment start/completion, transport handoff or retention for arrest, response failure and detected completed restraint. No diagnosis, hospital admission or completed arrest is invented. External recovery is labelled as observed recovery with an unidentified provider.

Each milestone is captured once per episode. Repeated requests/failures of the same type do not produce additional entries. Name changes after the bite do not rewrite the original dog's identity.

## Linking and delivery

- AdvancedK9 uses NexusMDT's public `NexusMDT.Api.DispatchBridge.AppendIncidentNote(number, note)` function. It appends incident notes; whether Nexus includes those notes in a final report narrative must be confirmed in the game. It does not submit or finalize a report.
- If a supported public active-incident accessor resolves a number at the bite, the episode is linked automatically. The installed Nexus API may not expose this accessor; the known log confirms the note writer, not automatic incident discovery.
- Otherwise open **K9 Profile > Bite / EMS MDT reports**, select the bite, choose **Link to report** and enter its Nexus incident number. Events remain pending until linked. An older unbound bite is never automatically assigned to a later active incident.
- Once linked, the bite and its medical events stay on that incident even if the active incident changes. A link cannot be moved to a different incident.
- Notes are stored in a durable outbox under the normal AdvancedK9 data folder. Accepted milestones are not resent. An exception or interruption during delivery is held as uncertain: inspect the MDT incident before choosing **After checking MDT, retry unconfirmed notes**, since that manual retry can duplicate a note that Nexus accepted before the interruption.
- A void API return confirms that the writer returned normally, not that Nexus saved the final report. The log distinguishes queued, pending, API-accepted and uncertain states. This is not an exactly-once transaction across both plugins.
- Existing `[Compatibility] ShareK9Results` and `ShareWithNexusMDT` settings must be enabled when the bite occurs. With sharing disabled, new report events are not queued and pending delivery is paused. Re-enable sharing and restart to resume pending notes.

## Test sequence

1. Start a Nexus incident, then apprehend and release one suspect. Check the MDT menu's link/status and look for the bite and release notes in that incident.
2. Request EMS. Confirm the request is logged before treatment and completion only after actual treatment. Check the on-scene or transport disposition.
3. Confirm that the notes appear in the associated report narrative/draft in Nexus. If they remain incident notes only, provide the Nexus API documentation or DLL so its supported report-draft interface can be verified.
4. Change the active incident before treatment ends: medical notes must stay with the original incident.
5. Repeat without an active incident, link manually afterward, and restart with pending notes. Accepted milestones should not repeat.
6. Supply the full RagePluginHook log and the MDT incident/report view. Verify ordinary apprehension, release and EMS movement still behave as before.

The existing release changelog lists the confirmed baseline. These MDT features await in-game confirmation; callouts remain experimental. No Nexus database files are edited directly.
