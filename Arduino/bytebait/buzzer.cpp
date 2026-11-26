#include "buzzer.h"
#include "config.h"
#include "hardware.h"

static bool playing = false;
static unsigned long startedAt = 0;

void buzzerPlayLost() {
  playing = true;
  startedAt = millis();
  hwBuzzerOn(BUZZ_HIGH_HZ);
}

void buzzerUpdate() {
  if (!playing) return;
  unsigned long t = millis() - startedAt;

  if (t < BUZZ_HOLD_MS) {
    // "EEE": high tone that wobbles up and down a little.
    bool up = ((t / BUZZ_WOBBLE_MS) % 2) == 0;
    hwBuzzerOn(up ? BUZZ_HIGH_HZ + BUZZ_WOBBLE_HZ : BUZZ_HIGH_HZ - BUZZ_WOBBLE_HZ);
  } else if (t < BUZZ_HOLD_MS + BUZZ_SLIDE_MS) {
    // "UUHH": the pitch falls in a straight line from HIGH to LOW.
    unsigned long s = t - BUZZ_HOLD_MS;
    long freq = (long)BUZZ_HIGH_HZ - (long)(BUZZ_HIGH_HZ - BUZZ_LOW_HZ) * (long)s / (long)BUZZ_SLIDE_MS;
    hwBuzzerOn((uint16_t)freq);
  } else {
    hwBuzzerOff();
    playing = false;
  }
}
