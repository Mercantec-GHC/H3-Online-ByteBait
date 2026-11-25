/**
 * Defines the nibble and bite timing for a single cast. The float and the hook
 * input can use the same timeline without depending on the rendering frame rate.
 */
const nibbleMs = 800;
const pauseAfterNibbleMs = 900;
const phases = [
    { state: "waiting", durationMs: 1500 },
    { state: "nibble", durationMs: nibbleMs },
    { state: "waiting", durationMs: pauseAfterNibbleMs },
    { state: "nibble", durationMs: nibbleMs },
    { state: "waiting", durationMs: pauseAfterNibbleMs },
    { state: "bite", durationMs: 1500 },
];

export const hookDurationMs = phases.reduce((total, phase) => total + phase.durationMs, 0);

/**
 * Creates an independent attempt. Call update(elapsedMs) to read the phase or
 * hook(elapsedMs) to apply a pull. Both take milliseconds since this attempt
 * started and return its current state. Its first outcome lasts for the cast.
 */
export function createHookAttempt() {
    let outcome = null;

    function update(elapsedMs) {
        if (!Number.isFinite(elapsedMs) || elapsedMs < 0) {
            throw new RangeError("Elapsed time must be a finite, non-negative number of milliseconds.");
        }
        if (outcome !== null) return outcome;

        let endMs = 0;
        for (const phase of phases) {
            endMs += phase.durationMs;
            if (elapsedMs < endMs) return phase.state;
        }

        outcome = "escaped";
        return outcome;
    }

    function hook(elapsedMs) {
        const state = update(elapsedMs);
        if (state === "nibble") {
            outcome = "scared";
        } else if (state === "bite") {
            outcome = "hooked";
        }
        return outcome ?? state;
    }

    return { update, hook };
}
