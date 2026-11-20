#pragma once
#include <Arduino.h>

// S3.9 (#38): WiFi (WiFiNINA) and an open connection to Dokploy over HTTPS/WSS.
// Spike #4: a new HTTPS request per message takes about 3.5 s, so the connection MUST be kept open.

void wifiBegin();                       // start the WiFi connection (credentials from arduino_secrets.h)
void wifiUpdate();                      // call in loop(): reconnect without blocking
bool wifiConnected();                   // WiFi + server connection up
bool wifiAvailable();
int  wifiRead();
void wifiWriteLine(const char* line);
