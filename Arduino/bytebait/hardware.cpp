#include <Arduino_MKRIoTCarrier.h>
#include "hardware.h"
#include "config.h"

// The Carrier library is included ONLY here, so it is not defined multiple times.
static MKRIoTCarrier carrier;

void hwBegin() {
  CARRIER_CASE = false;                       // true if the Oplà sits in its case
  carrier.begin();
  carrier.leds.setBrightness(LED_BRIGHTNESS);
}

bool hwAccelAvailable() {
  return carrier.IMUmodule.accelerationAvailable();
}

void hwReadAccel(float &x, float &y, float &z) {
  carrier.IMUmodule.readAcceleration(x, y, z);
}

void hwSetLeds(uint8_t r, uint8_t g, uint8_t b) {
  uint32_t color = carrier.leds.Color(r, g, b);
  for (uint8_t i = 0; i < LED_COUNT; i++) carrier.leds.setPixelColor(i, color);
  carrier.leds.show();
}
