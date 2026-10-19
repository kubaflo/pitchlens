import { fit } from "./replay.mjs";

export function viewportMap(width, height, pixelWidth, pixelHeight) {
    const field = fit(width, height);
    const sx = pixelWidth / width, sy = pixelHeight / height;
    return {
        width, height, pixelWidth, pixelHeight, field,
        matrix: [sx * field.scale, 0, 0, sy * field.scale, sx * field.left, sy * field.top]
    };
}
export function applyField(context, map) { context.setTransform(...map.matrix); }
export function fieldToViewport(map, point) {
    const m = map.matrix;
    return { x: (m[0] * point.x + m[4]) * map.width / map.pixelWidth,
        y: (m[3] * point.y + m[5]) * map.height / map.pixelHeight };
}
export function pointerInDrawnViewport(map, rect, clientX, clientY) {
    if (rect.width <= 0 || rect.height <= 0) return null;
    return { x: (clientX - rect.left) * map.width / rect.width, y: (clientY - rect.top) * map.height / rect.height };
}
