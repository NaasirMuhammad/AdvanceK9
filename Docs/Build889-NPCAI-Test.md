# Build 889 — NPCAI callouts test

AdvancedK9 callouts 0.24.0.54, on the Build 888 core (0.24.1.7). Install into the GTA V directory. Restart GTA after replacing the DLLs.

NPCAI 4.0 is optional. Install it separately; its own installer places NPCAI_Callouts_API.dll next to GTA5.exe. This package does not include that DLL. Both NPCAI editions use the same callout API.

- Bank Robbery: talk to the contact officer, bank manager when available, and the hostage or suspect as the scene permits. The hostage stays in the scripted pose while held.
- Fugitive Trail: ask the contact officer about the abandoned vehicle and scent, then ask about the location update after Rex finds the fugitive. The located suspect has their own perspective.
- Lost Child: ask both parents about the search, then speak with the recovered teen. Ask the parents again after recovery to check updated dialogue.

Approach the contact officer on foot within 8 m and line of sight. Use NPCAI's regular interaction to ask follow-up questions. Test each callout with NPCAI active, test a second callout after clearing the first, and repeat with NPCAI absent to confirm text briefings and normal progression. Check RagePluginHook.log for AdvancedK9 NPCAI binding/failure messages and NPCAI story receipts.

NPCAI handles speech; the callout still owns pursuit, negotiation outcomes, K9, medical, custody and scene cleanup. No voice phrase advances a critical stage. Ambient patrol context and NPCAI-to-Rex commands are not enabled by this callout SDK.
