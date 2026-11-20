#include "link_wifi.h"
#include "config.h"
// #include <WiFiNINA.h>
// #include <ArduinoHttpClient.h>
// #include "arduino_secrets.h"   // SSID, password, host, path (NOT committed; update the example file)

// TODO(#38): client objects (static), state, time of the last attempt

void wifiBegin() {
  // TODO(#38): WiFi.begin(SECRET_SSID, SECRET_PASS) without blocking the whole program
}

void wifiUpdate() {
  // TODO(#38): reconnect with a pause between attempts (the handshake takes 3-4 s)
  // TODO(#38): PING/PONG monitoring (see docs/protocol.md section 4)
}

bool wifiConnected() {
  return false;   // TODO(#38)
}

bool wifiAvailable() {
  return false;   // TODO(#38)
}

int wifiRead() {
  return -1;      // TODO(#38)
}

void wifiWriteLine(const char* line) {
  (void)line;
  // TODO(#38)
}
