import AppKit
import WebKit

// Offscreen Razor HTML and production popup handlers; no window or user website data.
NSApplication.shared.setActivationPolicy(.prohibited)
guard CommandLine.arguments.count == 2 else { fputs("Usage: measure-info-reflow.swift <Razor preview directory>\n", stderr); exit(1) }
let folder = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
let files = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil)
    .filter { $0.pathExtension == "html" && ($0.lastPathComponent.contains("-closed") || $0.lastPathComponent.contains("-open")) }
    .sorted { $0.lastPathComponent < $1.lastPathComponent }
guard files.count == 42 else { fputs("Expected 42 exported Razor previews; found \(files.count). No layout result certified.\n", stderr); exit(1) }
let popupSource = try String(contentsOf: folder.appendingPathComponent("js/info-disclosures.mjs"), encoding: .utf8)
    .replacingOccurrences(of: "export function ", with: "function ")
let sizes = [(width: 1280, height: 800), (width: 640, height: 480), (width: 390, height: 500)]
let config = WKWebViewConfiguration()
config.websiteDataStore = .nonPersistent()
let web = WKWebView(frame: .zero, configuration: config)
var results = [[String: Any]]()
var finished = false
var failure: Error?

final class Navigation: NSObject, WKNavigationDelegate {
    var loaded: (() -> Void)?
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) { loaded?() }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) { failure = error; finished = true }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) { failure = error; finished = true }
}
let delegate = Navigation()
web.navigationDelegate = delegate
var index = 0
func next() {
    if index == files.count * sizes.count { finished = true; return }
    let file = files[index / sizes.count], size = sizes[index % sizes.count]
    index += 1
    web.frame = NSRect(x: 0, y: 0, width: size.width, height: size.height)
    delegate.loaded = {
        web.callAsyncJavaScript(popupSource + """
            const cleanup = installInfoDisclosures(document);
            await document.fonts.ready;
            const dialog = document.querySelector('dialog[open]');
            if (dialog) {
              dialog.close();
              if (dialog.getAttribute('aria-modal') === 'false') dialog.show(); else dialog.showModal();
            }
            const scope = dialog ?? document.querySelector('.setup-card') ?? document.body;
            const dimensions = () => [scope.scrollWidth, scope.scrollHeight, scope.getBoundingClientRect().width, scope.getBoundingClientRect().height];
            const before = dimensions();
            const geometry = () => [scope, ...scope.querySelectorAll('.agent-step,.agent-workflow,.setup-card,.dialog-head,.dialog-body,.view-styles,.reading-options,.studio-grid,.studio-main,.studio-analysis,.button,.info-disclosure')]
              .filter(e => !e.closest('.info-content'))
              .map(e => {const r=e.getBoundingClientRect(); return [r.x,r.y,r.width,r.height];});
            const beforeBoxes = geometry();
            const opener = scope.querySelector('[data-info-toggle]');
            if (document.body.dataset.infoPreview === 'open') opener.click();
            const close = dialog?.querySelector('[data-close-popup]');
            const closeBox = close?.getBoundingClientRect();
            const help = [...scope.querySelectorAll('.info-content:not([hidden])')];
            const afterBoxes = geometry();
            if (beforeBoxes.length !== afterBoxes.length) throw new Error('Parent/card layout nodes changed while opening help.');
            const layoutShiftPixels = Math.max(0,...beforeBoxes.flatMap((r,i) => r.map((v,j) => Math.abs(v-afterBoxes[i][j]))));
            const result = {
              rootOverflow: document.documentElement.scrollWidth > innerWidth + 1,
              scopeOverflow: scope.scrollWidth > scope.clientWidth + 1,
              closeReachable: !closeBox || (closeBox.left >= 0 && closeBox.top >= 0 && closeBox.bottom <= innerHeight && closeBox.right <= innerWidth),
              helpContained: help.every(e => { const r=e.getBoundingClientRect(); return r.left >= -1 && r.right <= innerWidth + 1 && r.top >= 0 && r.bottom <= innerHeight; }),
              visibleHelp: help.filter(e => e.getBoundingClientRect().height > 0).length,
              helpBounds: help.map(e => { const r=e.getBoundingClientRect(); return {left:r.left,top:r.top,width:r.width,height:r.height,cssLeft:e.style.left,cssTop:e.style.top}; }),
              nativePopup: help.every(e => e.matches(':popover-open') && getComputedStyle(e).position === 'fixed'),
              parentLayoutUnchanged: JSON.stringify(before) === JSON.stringify(dimensions()),
              layoutShiftPixels,
              parentAndCardBoxesChecked: beforeBoxes.length,
              rootFont: getComputedStyle(document.documentElement).fontSize,
              width: innerWidth,
              height: innerHeight
            };
            if (help.length) {
              const event = new KeyboardEvent('keydown', {key:'Escape', bubbles:true, cancelable:true});
              opener.dispatchEvent(event);
              result.escapeHandled = event.defaultPrevented && !help[0].matches(':popover-open') && help[0].hidden && document.activeElement === opener && (!dialog || dialog.open);
              opener.click();
              (scope.querySelector('.dialog-body') ?? scope).dispatchEvent(new MouseEvent('click',{bubbles:true}));
              result.outsideDismissedOnlyPopup = !help[0].matches(':popover-open') && help[0].hidden && (!dialog || dialog.open);
            } else {result.escapeHandled = true; result.outsideDismissedOnlyPopup = true;}
            cleanup();
            return result;
            """, arguments: [:], in: nil, in: .page, completionHandler: { result in
                switch result {
                case .success(let value):
                    if var entry = value as? [String: Any] {
                        entry["file"] = file.lastPathComponent
                        let font = file.lastPathComponent.contains("-200-") ? "36px" : file.lastPathComponent.contains("-150-") ? "27px" : "18px"
                        entry["scaleCorrect"] = entry["rootFont"] as? String == font
                        entry["helpStateCorrect"] = entry["visibleHelp"] as? Int == (file.lastPathComponent.contains("-open") ? 1 : 0)
                        results.append(entry)
                    } else { failure = NSError(domain: "No layout result", code: 1) }
                case .failure(let error): failure = error
                }
                if failure != nil { finished = true } else { next() }
            })
    }
    web.loadFileURL(file, allowingReadAccessTo: folder)
}
next()
let deadline = Date().addingTimeInterval(180)
while !finished && Date() < deadline { RunLoop.main.run(until: Date().addingTimeInterval(0.03)) }
if let failure { fputs("Static WebKit layout failed: \(failure)\n", stderr); exit(1) }
if !finished { fputs("Static WebKit layout timed out\n", stderr); exit(1) }
let data = try JSONSerialization.data(withJSONObject: results, options: [.prettyPrinted, .sortedKeys])
try data.write(to: folder.appendingPathComponent("static-reflow.json"))
let failures = results.filter { ($0["rootOverflow"] as? Bool == true) || ($0["scopeOverflow"] as? Bool == true) ||
    ($0["closeReachable"] as? Bool == false) || ($0["helpContained"] as? Bool == false) ||
    ($0["scaleCorrect"] as? Bool != true) || ($0["helpStateCorrect"] as? Bool != true) ||
    ($0["nativePopup"] as? Bool != true) || ($0["parentLayoutUnchanged"] as? Bool != true) || ($0["escapeHandled"] as? Bool != true) ||
    ($0["layoutShiftPixels"] as? Double != 0) || ($0["outsideDismissedOnlyPopup"] as? Bool != true) }
print("Offscreen WebKit popups: \(results.count) layouts; \(failures.count) failures. No native-app AX or pixel certification.")
for entry in failures { print(entry) }
exit(failures.isEmpty ? 0 : 1)
