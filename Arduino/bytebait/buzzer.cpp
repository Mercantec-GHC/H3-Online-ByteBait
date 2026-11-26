#include "buzzer.h"
#include "config.h"
#include "hardware.h"

static bool playing = false;
static unsigned long startedAt = 0;
static uint16_t currentFreq = 0;     // the tone that is playing now (0 = none)

// tone() restarts its timer on every call, so only call it when the pitch really changes.
static void setTone(uint16_t freq) {
  if (freq == currentFreq) return;
  currentFreq = freq;
  hwBuzzerOn(freq);
}

void buzzerPlayLost() {
  playing = true;
  startedAt = millis();
  currentFreq = 0;
  setTone(BUZZ_HIGH_HZ + BUZZ_WOBBLE_HZ);
}

void buzzerUpdate() {
  if (!playing) return;
  unsigned long t = millis() - startedAt;

  if (t < BUZZ_HOLD_MS) {
    // "EEE": high tone that wobbles up and down a little (changes every BUZZ_WOBBLE_MS).
    bool up = ((t / BUZZ_WOBBLE_MS) % 2) == 0;
    setTone(up ? BUZZ_HIGH_HZ + BUZZ_WOBBLE_HZ : BUZZ_HIGH_HZ - BUZZ_WOBBLE_HZ);
  } else if (t < BUZZ_HOLD_MS + BUZZ_SLIDE_MS) {
    // "UUHH": the pitch falls in steps of BUZZ_SLIDE_STEP_MS from HIGH to LOW.
    unsigned long s = (t - BUZZ_HOLD_MS) / BUZZ_SLIDE_STEP_MS * BUZZ_SLIDE_STEP_MS;
    long freq = (long)BUZZ_HIGH_HZ - (long)(BUZZ_HIGH_HZ - BUZZ_LOW_HZ) * (long)s / (long)BUZZ_SLIDE_MS;
    setTone((uint16_t)freq);
  } else {
    hwBuzzerOff();
    currentFreq = 0;
    playing = false;
  }
}
