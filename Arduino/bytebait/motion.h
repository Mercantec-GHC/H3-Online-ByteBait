#pragma once
#include <Arduino.h>

// S2.1 (#17): detect hook (upward jerk), tilt/pull left and right,
// hold still (give line) and yank (fast upward pull).
// Sends messages via protocol.h: HOOK, TILT:L, TILT:R, HOLD:1/0, YANK.

void motionUpdate();   // call in loop()
