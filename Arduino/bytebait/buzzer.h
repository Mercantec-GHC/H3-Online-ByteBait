#pragma once
#include <Arduino.h>

// S2.3 (#19): buzzer "EEEEEEEUUHH" when the fish gets away (BUZZ:LOST).
// Non-blocking: the sound plays over several calls to buzzerUpdate().

void buzzerPlayLost();   // start the sound (called from protocol.cpp on BUZZ:LOST)
void buzzerUpdate();     // call in loop()
