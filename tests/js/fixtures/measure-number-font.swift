import Foundation
import CoreText
import CoreGraphics
import CryptoKit

let fontURL = URL(fileURLWithPath: CommandLine.arguments[1])
let data = try Data(contentsOf: fontURL)
guard let provider = CGDataProvider(data: data as CFData),
      let graphicsFont = CGFont(provider) else { fatalError("Cannot read bundled Barlow font") }
let font = CTFontCreateWithGraphicsFont(graphicsFont, 100, nil, nil)
func measure(_ face: CTFont) -> [String: [String: Double]] {
    var numbers: [String: [String: Double]] = [:]
    for number in 1...99 {
        let text = String(number)
        let value = NSAttributedString(string: text, attributes: [NSAttributedString.Key(kCTFontAttributeName as String): face])
        let line = CTLineCreateWithAttributedString(value)
        let bounds = CTLineGetBoundsWithOptions(line, [.useGlyphPathBounds])
        guard bounds.width > 0 && bounds.height > 0 else { fatalError("Empty numeral ink") }
        numbers[text] = ["actualBoundingBoxLeft": -bounds.minX, "actualBoundingBoxRight": bounds.maxX,
                         "actualBoundingBoxAscent": bounds.maxY, "actualBoundingBoxDescent": -bounds.minY]
    }
    return numbers
}
let fallback = CTFontCreateWithName("Helvetica-Bold" as CFString, 100, nil)
let result: [String: Any] = [
    "measurement": "CoreText CTLine glyph-path bounds, not browser raster pixels",
    "font": CTFontCopyPostScriptName(font) as String, "fontSize": 100,
    "fontSHA256": SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined(),
    "numbers": measure(font), "fallbackFont": CTFontCopyPostScriptName(fallback) as String,
    "fallbackNumbers": measure(fallback)
]
let json = try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys])
try json.write(to: URL(fileURLWithPath: CommandLine.arguments[2]), options: .atomic)
