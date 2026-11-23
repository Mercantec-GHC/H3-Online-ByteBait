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

// The sensor hands out each sample only once: reading it clears the "new data" flag.
// Several modules (throw and motion) need the same sample, so it is read here once per loop
// pass and cached.
static float lastX = 0, lastY = 0, lastZ = 1;
static bool  fresh = false;

void hwAccelPoll() {
  fresh = carrier.IMUmodule.accelerationAvailable();
  if (fresh) carrier.IMUmodule.readAcceleration(lastX, lastY, lastZ);
}

bool hwAccelAvailable() {
  return fresh;
}

void hwReadAccel(float &x, float &y, float &z) {
  x = lastX; y = lastY; z = lastZ;
}

void hwSetLeds(uint8_t r, uint8_t g, uint8_t b) {
  uint32_t color = carrier.leds.Color(r, g, b);
  for (uint8_t i = 0; i < LED_COUNT; i++) carrier.leds.setPixelColor(i, color);
  carrier.leds.show();
}
