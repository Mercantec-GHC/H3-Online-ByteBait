#pragma once
#include <Arduino.h>

// S3.9 (#38): common transport layer, so the protocol is the same over USB and WiFi.
// protocol.cpp must use link* instead of Serial directly.
// Automatic selection: WiFi if connected, otherwise USB.

enum LinkType { LINK_USB, LINK_WIFI };

void     linkBegin();                       // call in setup()
void     linkUpdate();                      // call in loop(): maintain/restore the connection
LinkType linkActive();                      // which connection is in use right now
bool     linkAvailable();                   // is there a byte to read
int      linkRead();                        // read one byte, -1 if none
void     linkWriteLine(const char* line);   // send a line (without \n, link adds it)
