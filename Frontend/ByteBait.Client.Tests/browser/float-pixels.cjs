// Read the float drawn by either the preview or the actual game, independently of its status text.
function readSignal(page) {
    return page.locator("canvas").evaluate(canvas => {
        const top = 80;
        const { data, width } = canvas.getContext("2d").getImageData(130, top, 61, 40);
        let tipTop = null;
        let body = false, innerRing = false, outerRing = false;
        for (let i = 0; i < data.length; i += 4) {
            const rgb = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
            if (rgb === 0xff6956 && tipTop === null) tipTop = top + Math.floor(i / 4 / width);
            if (rgb === 0xfff1cb) body = true;
            if (rgb === 0xd6ecce) innerRing = true;
            if (rgb === 0x99ccc0) outerRing = true;
        }
        return { tipTop, body, innerRing, outerRing };
    });
}

const openWater = { tipTop: null, body: false, innerRing: false, outerRing: false };
const waiting = { ...openWater, tipTop: 94, body: true };
const nibble = { ...openWater, tipTop: 104 };
const bite = { ...openWater, innerRing: true, outerRing: true };

module.exports = { readSignal, openWater, waiting, nibble, bite };
