#pragma once
#include <Arduino.h>

// Set all LEDs by color name: RED, YELLOW, GREEN, BLUE, OFF.
// Returns false if the name is unknown. The color is remembered, also while the LEDs are held.
bool ledsSetByName(const char* name);

// The Oplà itself uses the LEDs (calibration, sensitivity level). While they are held, a color from the game
// is only remembered, so it can't hide what the Oplà shows. ledsRelease() shows the game's last color again.
void ledsHold();
void ledsRelease();
