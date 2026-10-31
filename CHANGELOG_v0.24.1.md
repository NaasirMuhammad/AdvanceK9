# AdvancedK9 v0.24.1 — Changes Since v0.24.0

## Command control

- Fixed interrupted searches resuming after a newer command. Cancelled searches no longer return later with an outdated positive or negative result.
- Improved command ownership so the latest accepted dog command remains in control.

## Leash and equipment

- Updated the retractable leash asset to a 0.48–6.00m operating range and enabled its full 6m maximum.
- Fixed the vest attachment following Rex's posture, including sitting, lying down and getting back up.
- Corrected the lying attachment so the latch remains at the vest hook instead of floating above Rex or attaching to bare neck/skin.
- Preserved the handler loop's attachment to the left hand as the handler moves.
- Retained automatic release beyond the physical leash range rather than leaving a floating leash.

## Kennels

- Fixed a kennel-return interruption that could leave Rex turning or walking instead of settling into the sleeping animation.
- Prevented kennel maintenance from interfering with the dog during deployment and return.
- Preserved the existing large-kennel placement, resting alignment and separate setup for the original kennel type.

## Realistic vehicle entry and exit

- Restored the visible entry jump when optional body-position calibration was unavailable.
- Corrected vehicle jump and seat alignment to prevent the exaggerated jump above the roof before settling into the seat.
- Corrected exit ground clearance and the sideways doorway route, preventing the previously reported underground landing and rearward jump through the vehicle.
- Preserved saved vehicle-specific seat configurations.

## Apprehension and barking

- Removed the bark/preparation and random command-response delays before a valid apprehension across keyboard, controller, voice and menu controls.
- Fixed repeated chase-task restarts that caused stutter steps, hesitation and loss of acceleration. Pursuit remains locked to the selected suspect through movement and direction changes.
- Removed the 25-second chase timeout that recalled the dog during an active pursuit. New handler commands can still cancel the chase.
- Fixed the first-contact pause and second approach before biting. Muzzle/body contact checks now hand pursuit directly to one front/back takedown, including contact between movement frames.
- Prevented the full takedown animation from repeatedly restarting during the hold.
- Improved injured-suspect custody after release so the patient remains down pending treatment or actual restraint, with coordinated pursuit control and arrest/EMS handoff.
- Applied the same apprehension logic across supported dog names and coats.
- Restored the manual Bark command with audible barking and its enter, bark and exit animation sequence.

## Care

- Shortened eating and drinking by replacing repeated full animation restarts with a bounded interaction loop.

## Voice commands and documentation

- Enabled on-duty voice deployment before a K9 has been deployed: say “Deploy K9” near a configured station kennel.
- Added a complete guide covering all 57 registered voice commands, built-in English aliases, additional phrases for 13 language packs, wake-word requirements, dispatch commands and custom aliases.
- Updated release installation and upgrade instructions; existing user configuration and profiles are preserved.

