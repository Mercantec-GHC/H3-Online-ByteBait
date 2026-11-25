/**
 * Draws the float on a 320 × 180 pixel water scene. The hook attempt owns the
 * timing; this module paints a single frame for the state supplied by its caller.
 */
const floatX = 160;
const waterlineY = 108;

function drawWater(context) {
    context.fillStyle = "#9ec7bf";
    context.fillRect(0, 0, 320, 46);
    context.fillStyle = "#c7d8bd";
    context.fillRect(0, 36, 320, 10);
    context.fillStyle = "#588e91";
    context.fillRect(0, 46, 320, 19);
    context.fillStyle = "#387579";
    context.fillRect(0, 65, 320, 49);
    context.fillStyle = "#30696f";
    context.fillRect(0, 114, 320, 66);

    // Short horizontal reflections leave the centre clear for the bite signal.
    context.fillStyle = "#79a5a1";
    for (const [x, y, width] of [
        [9, 52, 22], [55, 56, 14], [94, 50, 33], [201, 55, 23], [272, 51, 31],
        [28, 81, 19], [92, 74, 12], [242, 85, 16], [292, 78, 11],
    ]) {
        context.fillRect(x, y, width, 1);
    }
    context.fillStyle = "#437c7f";
    for (const [x, y, width] of [[17, 145, 33], [73, 126, 16], [201, 159, 27], [268, 133, 20]]) {
        context.fillRect(x, y, width, 1);
    }
}

// Stepped edges keep the two ripples crisp even at the original resolution.
function drawRipple(context, radius, height) {
    const half = Math.floor(radius / 2);
    for (const direction of [-1, 1]) {
        context.fillRect(floatX - half, waterlineY + direction * height, half * 2 + 1, 1);
        const shoulderY = waterlineY + direction * (height - 1);
        context.fillRect(floatX - radius + 2, shoulderY, radius - half - 2, 1);
        context.fillRect(floatX + half + 1, shoulderY, radius - half - 2, 1);
    }
    context.fillRect(floatX - radius, waterlineY - height + 2, 2, 2 * height - 3);
    context.fillRect(floatX + radius - 1, waterlineY - height + 2, 2, 2 * height - 3);
}

/**
 * Waiting shows both the red tip and the pale body. A nibble leaves only the
 * tip above water. A bite replaces the float with rings. A resolved or idle
 * attempt (null) shows open water. The optional background painter receives
 * the 2D context before the signal is drawn. Coordinates are native pixels.
 */
export function drawFloatScene(canvas, state, drawBackground = drawWater) {
    const context = canvas.getContext("2d");
    if (!context) throw new Error("The float scene requires a 2D canvas.");

    drawBackground(context);
    if (state === "bite") {
        context.fillStyle = "#99ccc0";
        drawRipple(context, 16, 4);
        context.fillStyle = "#d6ecce";
        drawRipple(context, 8, 2);
    } else if (state === "waiting" || state === "nibble") {
        context.fillStyle = "#244e59";
        context.fillRect(floatX - 6, waterlineY, 13, 1);
        const tipY = waterlineY - (state === "waiting" ? 14 : 4);
        context.fillStyle = "#ff6956";
        context.fillRect(floatX - 2, tipY, 5, 4);
        if (state === "waiting") {
            context.fillStyle = "#fff1cb";
            context.fillRect(floatX - 2, tipY + 4, 5, 3);
            context.fillRect(floatX - 4, tipY + 7, 9, 5);
            context.fillRect(floatX - 3, tipY + 12, 7, 2);
        }
    }
}
