/**
 * Static first-person foreground for the 320 × 180 fishing scene. The right
 * hand holds the reel seat; the left supports the cork further down the rod.
 */
const graphite = "#293b46", steel = "#759099", silver = "#b6c5ba";
const corkShade = "#977149", cork = "#c8a164", corkLight = "#e3c384";
const outline = "#684c47", skinShade = "#b47760", skin = "#dd9e77", light = "#f1c49a";
const sleeve = "#385467", seam = "#547184";

// Each row gives [y, left edge, right edge]. Interpolate between rows in
// whole pixels so even the thinnest part of the blank has a crisp silhouette.
function fillShape(context, color, rows) {
    context.fillStyle = color;
    for (let i = 1; i < rows.length; i++) {
        const [top, left, right] = rows[i - 1];
        const [bottom, endLeft, endRight] = rows[i];
        for (let y = top; y < bottom; y++) {
            const fraction = (y - top) / (bottom - top);
            const x = Math.round(left + (endLeft - left) * fraction);
            const last = Math.round(right + (endRight - right) * fraction);
            context.fillRect(x, y, last - x + 1, 1);
        }
    }
}

function drawRodAndReel(context) {
    // A tapered graphite blank with small line guides along its underside.
    fillShape(context, graphite, [[70, 202, 202], [88, 216, 217], [111, 235, 237], [123, 244, 247]]);
    fillShape(context, steel, [[91, 220, 220], [121, 245, 245]]);
    context.fillStyle = graphite;
    for (const [x, y, size] of [[202, 71, 2], [213, 85, 3], [230, 106, 4]]) {
        context.fillRect(x - size + 1, y, 1, size);
        context.fillRect(x - size + 2, y + size - 1, size - 1, 1);
        context.fillRect(x + 1, y + 1, 1, size - 1);
    }

    // The cork is continuous under both hands, with a dark seat and butt cap.
    fillShape(context, graphite, [[114, 236, 239], [118, 237, 244], [173, 281, 291], [178, 286, 294], [181, 289, 291]]);
    fillShape(context, corkShade, [[115, 238, 239], [119, 239, 243], [171, 280, 289], [176, 285, 292]]);
    fillShape(context, cork, [[116, 238, 239], [120, 240, 243], [171, 282, 289], [175, 286, 290]]);
    fillShape(context, corkLight, [[139, 258, 260], [156, 272, 273]]);
    fillShape(context, graphite, [[123, 243, 245], [125, 243, 249], [136, 251, 259], [139, 256, 258]]);

    // The spool points towards the tip. Its stem, bail and offset crank make
    // the reel readable in profile, below the forward hand.
    fillShape(context, graphite, [[129, 248, 252], [135, 246, 252], [143, 242, 247], [146, 242, 245]]);
    fillShape(context, steel, [[135, 248, 249], [142, 243, 245], [144, 244, 245]]);
    fillShape(context, graphite, [[135, 234, 238], [140, 230, 242], [144, 233, 247], [148, 239, 250], [152, 243, 248]]);
    fillShape(context, steel, [[136, 235, 237], [139, 232, 241], [143, 234, 244], [147, 240, 247], [150, 243, 246]]);
    fillShape(context, silver, [[137, 235, 237], [141, 231, 234], [142, 232, 233]]);
    fillShape(context, graphite, [[137, 236, 238], [141, 231, 233]]);
    fillShape(context, silver, [[139, 238, 239], [143, 233, 234]]);
    fillShape(context, graphite, [[140, 240, 240], [144, 234, 236], [145, 236, 236]]);
    fillShape(context, silver, [[138, 242, 243], [143, 246, 247], [146, 241, 244]]);
    fillShape(context, graphite, [[146, 242, 245], [151, 238, 240], [153, 232, 238], [155, 231, 233]]);
    context.fillRect(229, 152, 5, 4);
    context.fillStyle = steel;
    context.fillRect(230, 152, 3, 1);
    context.fillStyle = silver;
    context.fillRect(244, 147, 2, 2);
}

function drawRightHand(context) {
    // Wrist to knuckles, then the folded fingers. The thumb passes in front
    // of the grip; the darker underside separates the fingertips from it.
    fillShape(context, outline, [[119, 249, 254], [122, 244, 259], [127, 243, 265],
        [133, 246, 271], [138, 251, 275], [142, 260, 282], [154, 278, 297], [161, 289, 299]]);
    fillShape(context, skinShade, [[120, 250, 254], [124, 245, 259], [128, 244, 265],
        [134, 248, 270], [138, 253, 273], [141, 261, 279], [154, 280, 294], [159, 289, 297]]);
    fillShape(context, skin, [[120, 251, 254], [125, 248, 261], [132, 250, 269],
        [137, 258, 273], [141, 267, 279], [154, 284, 293]]);
    fillShape(context, light, [[120, 251, 254], [123, 248, 254], [126, 247, 249]]);
    fillShape(context, outline, [[123, 253, 254], [129, 246, 248], [131, 246, 247]]);
    fillShape(context, light, [[124, 254, 256], [129, 250, 251]]);
    fillShape(context, skinShade, [[128, 257, 258], [133, 251, 252]]);
    fillShape(context, skinShade, [[132, 261, 262], [136, 256, 257]]);
    fillShape(context, outline, [[120, 247, 248], [123, 245, 251], [128, 249, 255], [130, 253, 255]]);
    fillShape(context, skin, [[120, 247, 248], [123, 246, 250], [128, 251, 254], [129, 253, 254]]);
    context.fillStyle = light;
    context.fillRect(247, 121, 3, 2);
    // Cuff and sleeve reach the bottom right, behind the supporting hand.
    fillShape(context, graphite, [[144, 284, 286], [153, 272, 302], [180, 299, 329]]);
    fillShape(context, sleeve, [[147, 287, 288], [156, 277, 303], [180, 303, 326]]);
    fillShape(context, seam, [[149, 288, 290], [157, 278, 281], [159, 280, 281]]);
}

function drawLeftHand(context) {
    // This arm reaches in from the opposite side of the butt. The raised
    // thumb crosses the rear grip and the fingertips curl around its far edge.
    fillShape(context, outline, [[150, 269, 272], [154, 264, 278], [158, 259, 283],
        [164, 252, 287], [168, 245, 284], [174, 235, 266], [180, 228, 256]]);
    fillShape(context, skinShade, [[151, 270, 272], [155, 265, 277], [159, 260, 282],
        [164, 253, 285], [168, 247, 281], [174, 237, 264], [180, 232, 253]]);
    fillShape(context, skin, [[152, 269, 272], [157, 264, 277], [161, 260, 282],
        [164, 256, 282], [168, 250, 273], [175, 240, 258]]);
    fillShape(context, light, [[151, 269, 271], [155, 267, 273], [159, 268, 271]]);
    fillShape(context, skinShade, [[155, 274, 274], [160, 273, 276], [163, 269, 272]]);
    fillShape(context, outline, [[157, 278, 279], [160, 276, 277]]);
    fillShape(context, outline, [[161, 283, 284], [165, 278, 279]]);
    fillShape(context, light, [[158, 280, 281], [160, 279, 280]]);
    fillShape(context, light, [[162, 284, 285], [164, 282, 283]]);
    fillShape(context, graphite, [[165, 246, 249], [174, 233, 261], [180, 221, 254]]);
    fillShape(context, sleeve, [[167, 246, 248], [175, 233, 257], [180, 224, 250]]);
    fillShape(context, seam, [[168, 246, 248], [175, 254, 255], [176, 254, 255]]);
}

/** Paint the rod and both hands over the lake, clear of the float and rings. */
export function drawFishingRod(context) {
    drawRodAndReel(context);
    drawRightHand(context);
    drawLeftHand(context);
}
