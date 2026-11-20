#pragma once
#include <Arduino.h>

// Line format and messages, see docs/protocol.md.

void protocolBegin();                 // announce USB or connect the optional HTTPS throw probe
void protocolPoll();                  // read game commands in USB mode
void protocolSendThrow(int strength); // THROW:<0-100>
void protocolSendHook();              // HOOK            (#17)
void protocolSendTilt(char dir);      // TILT:L / TILT:R (#17)
void protocolSendHold(bool still);    // HOLD:1 / HOLD:0 (#17)
void protocolSendYank();              // YANK            (#17)

// How to add a new message:
//  - Oplà -> game: add a protocolSendXxx() here and in protocol.cpp.
//  - Game -> Oplà: add a branch in handleLine() in protocol.cpp.
//  - Update docs/protocol.md.
