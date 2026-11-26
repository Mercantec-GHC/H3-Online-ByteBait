#pragma once
#include <Arduino.h>

// S2.1 (#17): detect hook (upward jerk), tilt/pull left and right,
// hold still (give line) and yank (fast upward pull).
// Sends messages via protocol.h: HOOK, TILT:L, TILT:R, HOLD:1/0, YANK.
//
// The motions are only active during the fish fight (see FIGHT_TIMEOUT_MS in config.h),
// so a throw is never also read as a hook, and a hook is never also read as a throw.

void motionUpdate();        // call in loop() after hwAccelPoll()

void motionStartFight();    // a cast happened: motions on, throw detection off
void motionStopFight();     // back to casting: motions off, throw detection on
bool motionFightActive();   // true during the fight (throw detection is paused)
