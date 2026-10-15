# Changelog

## 0.2.5 - 2026-10-15

- Separate every queued enemy impact by at least 115 ms so attacks after the first two remain distinct.
- Shorten the collision pulse to 70 ms and strengthen its onset, leaving an audible-animation-paced motor gap between hits.
- Preserve fallback attack order when several enemy actions execute together; reset the schedule for later groups.
- Add a twelve-enemy scheduling check.

## 0.2.4 - 2026-10-15

- Diagnose a real combat log with working card and pickup events but no enemy attack events.
- Hook the upstream `EnemyAction.Execute` path and identify concrete damage action types before scheduling their visual impact cues.
- Add trace-only probes for enemy turn execution, performed actions, custom animations, and damage settlement; damage settlement still cannot produce vibration.

## 0.2.3 - 2026-10-15

- Fix silent enemy attacks when the monitored transform does not move or an attack has no VFX prefab.
- Schedule single-hit feedback from the attack animation's wait parameter, without patching damage resolution.
- Use the game's attack timing configuration as a fallback for single and multi-hit animations.
- Keep visual lunge peaks and VFX callbacks as preferred signals and avoid duplicate fallback pulses.
- Add bounded animation-cue timing checks.

## 0.2.2 - 2026-10-15

- Move enemy impact pulses from `PlayerModel.ResolveDamage` to the enemy attack animation's forward-motion apex and attack VFX spawn.
- Track each enemy view independently so sequential attack animations generate separate visual impact pulses, including attacks blocked by armor.
- Keep sustained spell vibration tied to named animation clips or particle VFX.
- Add lunge-apex and recoil checks to the standalone smoke tests.

## 0.2.1 - 2026-10-15

- Trigger short hit accents when enemy damage resolves, instead of vibrating throughout every attack wind-up.
- Preserve separate accents for closely spaced hits from multiple enemies, spacing same-frame impacts by at most 150 ms.
- Sustain a softer waveform for enemy spell clips and multi-hit spell VFX identified by their asset names, using clip or particle duration where available.
- Track overlapping spell effects independently and clear them when an encounter ends.

## 0.2.0 - 2026-10-15

- Replace the card-play pulse with sustained weapon-animation rumble and separate hard and soft impact profiles.
- Add short village navigation and confirmation ticks.
- Add stronger wall and destructible-object bump feedback.
- Add coin scatter and experience-crystal shimmer streams tied to visual spawn and arrival events.
- Add a sustained enemy attack lead-in and retain the player-hit accent.
- Add waveform checks that run without proprietary game files.
- Keep the 0.1.0 release archive for rollback. The old `Events.CardPlayed` setting is no longer used.

## 0.1.0 - 2026-10-15

- Add DualSense and other Unity Input System gamepad rumble for successful manual card plays, encounter damage, and player damage.
- Add configurable strength and event toggles.
- Add a one-shot diagnostic pulse and stop motors when focus or the gamepad is lost.

