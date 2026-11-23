#pragma once
#include <Arduino.h>

// The only interface to the Oplà hardware. The rest of the code does not touch the Carrier library.
void hwBegin();
void hwAccelPoll();                                // call ONCE at the start of loop(): reads a new sample if there is one
bool hwAccelAvailable();                           // true if hwAccelPoll() found a new sample in this loop pass
void hwReadAccel(float &x, float &y, float &z);    // the latest sample, unit: g (can be called by several modules)
void hwSetLeds(uint8_t r, uint8_t g, uint8_t b);   // all LEDs the same color

// TODO(#19): void hwBuzzerOn(uint16_t freq);  void hwBuzzerOff();  (the Carrier buzzer)
