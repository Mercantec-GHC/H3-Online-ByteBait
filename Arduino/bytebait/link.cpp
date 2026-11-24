#include "link.h"
#include "config.h"
#include "link_wifi.h"

static LinkType active = LINK_USB;

void linkBegin() {
  // TODO(#38): Serial.begin() moves here from bytebait.ino
  // TODO(#38): wifiBegin()
}

void linkUpdate() {
  // TODO(#38): wifiUpdate()
  // TODO(#38): choose the active connection: wifiConnected() ? LINK_WIFI : LINK_USB
  // TODO(#38): send HELLO:<via>:1 when the active connection changes
}

LinkType linkActive() {
  return active;
}

bool linkAvailable() {
  // TODO(#38): read from the active connection
  return false;
}

int linkRead() {
  // TODO(#38): read from the active connection
  return -1;
}

void linkWriteLine(const char* line) {
  (void)line;
  // TODO(#38): write line + '\n' to the active connection
}
