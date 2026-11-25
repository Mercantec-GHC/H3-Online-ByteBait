#pragma once
#include <Arduino.h>

// Set all LEDs by color name: RED, YELLOW, GREEN, BLUE, OFF.
// Returns false if the name is unknown. The color is remembered (see ledsRestoreLast).
bool ledsSetByName(const char* name);

// Show the last color the game asked for again. Used after the Oplà itself has used the
// LEDs (calibration, sensitivity level) so the player still sees the color from the game.
void ledsRestoreLast();
