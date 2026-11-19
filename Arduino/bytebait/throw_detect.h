#pragma once

// Throw detection (S1.5): one throw produces exactly one THROW message.
// Call in loop(). In PLOT_MODE (config.h) no messages are sent, only data for Serial Plotter.
void throwUpdate();
