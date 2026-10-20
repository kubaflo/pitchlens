export const ARENA = { x: -4, y: -3, width: 113, height: 74 };

export function fitView(width, height, view) {
    if (!(width > 0 && height > 0 && view.width > 0 && view.height > 0)) throw new RangeError("Invalid camera viewport.");
    const scale = Math.min(width / view.width, height / view.height);
    return { scale, left: (width - view.width * scale) / 2 - view.x * scale, top: (height - view.height * scale) / 2 - view.y * scale };
}

export function viewportMap(width, height, pixelWidth, pixelHeight, view = { x: 0, y: 0, width: 105, height: 68 }) {
    const field = fitView(width, height, view);
    const sx = pixelWidth / width, sy = pixelHeight / height;
    return {
        width, height, pixelWidth, pixelHeight, field, view,
        matrix: [sx * field.scale, 0, 0, sy * field.scale, sx * field.left, sy * field.top]
    };
}
export function viewportToField(map, point) {
    return { x: (point.x - map.field.left) / map.field.scale, y: (point.y - map.field.top) / map.field.scale };
}
export function backgroundTransform(wide, camera) {
    const x = camera.matrix[0] / wide.matrix[0], y = camera.matrix[3] / wide.matrix[3];
    return [x, 0, 0, y, camera.matrix[4] - x * wide.matrix[4], camera.matrix[5] - y * wide.matrix[5]];
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
