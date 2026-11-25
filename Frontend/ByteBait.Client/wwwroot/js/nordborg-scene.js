/**
 * The daylight view from Nordborg Søpark towards the castle. All shapes use
 * the game's native 320 × 180 pixel grid. The float is a separate layer.
 */
import { drawFishingRod } from "./fishing-rod.js";

const sky = "#accfd9", haze = "#d2dfcf", cloud = "#e7ead7";
const hill = "#84a596", meadow = "#a7b482", willow = "#758f71";
const leaves = "#517563", shade = "#345955", reeds = "#9d9f71";
const shallows = "#82a99e", water = "#497a80", deep = "#3d6972";
const reflectedTrees = "#587f78", glint = "#aac2ae";
const roof = "#ad694f", plaster = "#e4d9b6";

// Join landmarks with a stepped outline and fill to a common shoreline or eave.
// Putting the baseline above the outline draws the banks' reflections instead.
function fillProfile(context, color, baseline, points) {
    context.fillStyle = color;
    for (let i = 1; i < points.length; i++) {
        const [left, fromY] = points[i - 1];
        const [right, toY] = points[i];
        for (let x = left; x < right; x++) {
            const y = fromY + Math.round((toY - fromY) * (x - left) / (right - left));
            context.fillRect(x, Math.min(y, baseline), 1, Math.abs(baseline - y));
        }
    }
}

// Broken horizontal strokes suggest clouds, reed beds and movement on the lake.
function drawStrokes(context, color, strokes) {
    context.fillStyle = color;
    for (const [x, y, width] of strokes) context.fillRect(x, y, width, 1);
}

function drawFoliage(context, color, clusters) {
    context.fillStyle = color;
    for (const [x, y, width] of clusters) {
        context.fillRect(x + 2, y, width - 4, 1);
        context.fillRect(x, y + 1, width, 2);
        context.fillRect(x + 1, y + 3, width - 3, 1);
    }
}

function drawCastle(context) {
    // The round tower and the long, low wing share a warm facade. Trees
    // overlap the lower storey as they do in the view across the water.
    context.fillStyle = plaster;
    context.fillRect(80, 46, 9, 19);
    context.fillRect(89, 54, 31, 11);
    fillProfile(context, roof, 55, [[87, 54], [93, 48], [115, 48], [121, 55]]);
    fillProfile(context, roof, 47, [[79, 46], [82, 41], [84, 36], [85, 36], [87, 41], [90, 47]]);
    context.fillStyle = shade;
    context.fillRect(84, 33, 1, 4);
    drawStrokes(context, shade, [[79, 47, 11], [90, 55, 31]]);
    for (const x of [82, 86, 93, 98, 103, 108, 113, 118]) context.fillRect(x, 59, 1, 3);
    context.fillRect(83, 50, 2, 3);
    context.fillStyle = plaster;
    for (const x of [96, 104, 112]) context.fillRect(x, 52, 2, 2);
}

/** Paint one complete background. The centre stays quiet around the float at (160, 108). */
function drawNordborgBackground(context) {
    context.fillStyle = sky;
    context.fillRect(0, 0, 320, 74);
    fillProfile(context, haze, 74, [[0, 55], [50, 53], [111, 58], [162, 49], [220, 50], [269, 42], [320, 46]]);
    fillProfile(context, cloud, 20, [
        [133, 20], [144, 18], [152, 18], [156, 16], [166, 16], [171, 12],
        [179, 12], [183, 15], [193, 15], [197, 18], [205, 18], [214, 20],
    ]);
    drawStrokes(context, haze, [[129, 21, 46], [182, 21, 34], [218, 22, 11], [18, 33, 23], [45, 32, 13]]);

    // A low gap between the wooded banks opens the view across the lake.
    fillProfile(context, hill, 74, [
        [0, 48], [37, 49], [67, 55], [94, 51], [128, 58], [148, 55], [168, 60],
        [197, 57], [218, 51], [240, 53], [267, 45], [295, 50], [320, 41],
    ]);
    fillProfile(context, meadow, 74, [
        [0, 69], [120, 66], [155, 63], [177, 66], [195, 65], [222, 58], [252, 56], [320, 64],
    ]);
    fillProfile(context, willow, 73, [
        [0, 36], [6, 33], [12, 34], [16, 31], [22, 31], [28, 36], [34, 37], [39, 44],
        [47, 41], [51, 42], [54, 38], [61, 37], [67, 43], [72, 42], [79, 49],
        [92, 50], [111, 57], [127, 54], [132, 51], [138, 53], [143, 52], [150, 61], [162, 69],
    ]);
    drawCastle(context);
    fillProfile(context, leaves, 74, [
        [0, 45], [5, 40], [12, 38], [18, 41], [23, 39], [29, 44], [34, 43], [40, 51],
        [45, 49], [51, 51], [57, 46], [62, 47], [68, 54], [73, 53], [79, 57], [82, 63],
        [89, 65], [93, 63], [103, 65], [116, 63], [121, 57], [125, 56], [130, 59],
        [134, 57], [139, 60], [145, 60], [152, 66], [159, 67], [165, 71], [172, 74],
    ]);
    fillProfile(context, willow, 74, [
        [217, 74], [230, 65], [242, 63], [248, 56], [253, 56], [259, 49], [265, 48],
        [271, 51], [277, 45], [282, 45], [287, 39], [293, 40], [300, 32], [306, 31], [313, 34], [320, 30],
    ]);
    fillProfile(context, leaves, 74, [
        [237, 74], [249, 66], [258, 65], [266, 57], [272, 58], [278, 52], [285, 53],
        [290, 47], [295, 48], [301, 40], [308, 43], [313, 38], [320, 39],
    ]);
    drawFoliage(context, willow, [
        [3, 44, 8], [10, 42, 7], [21, 48, 6], [39, 56, 7], [55, 50, 7],
        [65, 58, 5], [125, 61, 6], [268, 62, 7], [281, 55, 6], [299, 50, 10], [306, 46, 9],
    ]);
    drawFoliage(context, shade, [
        [4, 62, 11], [13, 59, 8], [32, 63, 10], [39, 66, 8], [60, 59, 7],
        [70, 65, 8], [117, 65, 7], [252, 69, 8], [280, 63, 10], [288, 60, 7], [310, 56, 10],
    ]);
    drawStrokes(context, reeds, [
        [0, 71, 24], [30, 71, 15], [51, 72, 28], [83, 71, 15], [101, 72, 30],
        [146, 72, 14], [178, 70, 18], [200, 69, 9], [230, 72, 13], [277, 71, 35],
    ]);

    context.fillStyle = water;
    context.fillRect(0, 74, 320, 106);
    fillProfile(context, shallows, 74, [[0, 96], [56, 91], [104, 87], [156, 82], [214, 81], [265, 87], [320, 93]]);
    fillProfile(context, reflectedTrees, 74, [
        [0, 92], [19, 88], [30, 90], [48, 84], [60, 86], [77, 81], [91, 79],
        [117, 78], [129, 81], [149, 78], [163, 74],
    ]);
    fillProfile(context, reflectedTrees, 74, [[237, 74], [254, 77], [270, 81], [289, 80], [305, 86], [320, 88]]);
    fillProfile(context, deep, 180, [
        [0, 145], [34, 145], [51, 146], [73, 146], [95, 149], [146, 149],
        [169, 152], [215, 152], [238, 149], [271, 149], [294, 147], [320, 147],
    ]);
    drawStrokes(context, glint, [
        [11, 82, 13], [37, 87, 21], [73, 79, 16], [91, 77, 10], [109, 80, 6],
        [147, 76, 20], [175, 79, 30], [213, 77, 12], [247, 83, 18], [294, 79, 12],
    ]);
    drawStrokes(context, shallows, [
        [0, 96, 17], [25, 99, 27], [55, 93, 12], [83, 104, 15], [107, 90, 19],
        [209, 98, 18], [242, 114, 23], [269, 103, 10], [293, 95, 27],
        [11, 126, 21], [40, 127, 8], [78, 138, 23], [197, 131, 12], [286, 139, 18],
    ]);
    drawStrokes(context, water, [[4, 156, 35], [61, 165, 18], [109, 154, 31], [183, 170, 23], [251, 157, 29], [300, 174, 14]]);
}

/** Compose the view in paint order. The caller adds the current float signal on top. */
export function drawNordborgScene(context) {
    drawNordborgBackground(context);
    drawFishingRod(context);
}
