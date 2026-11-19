#pragma once
#include <Arduino.h>

// Line format and messages, see docs/protocol.md.

void protocolPoll();                  // call in loop(): reads and handles messages from the game
void protocolSendHello();             // HELLO:USB:1
void protocolSendThrow(int strength); // THROW:<0-100>
void protocolSendHook();              // HOOK            (#17)
void protocolSendTilt(char dir);      // TILT:L / TILT:R (#17)
void protocolSendHold(bool still);    // HOLD:1 / HOLD:0 (#17)
void protocolSendYank();              // YANK            (#17)

// How to add a new message:
//  - Oplà -> game: add a protocolSendXxx() here and in protocol.cpp.
//  - Game -> Oplà: add a branch in handleLine() in protocol.cpp.
//  - Update docs/protocol.md.
