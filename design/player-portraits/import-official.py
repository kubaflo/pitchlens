"""Import only explicitly reviewed PL identities and their original portrait bytes."""
import argparse
import concurrent.futures
import hashlib
import html
import json
from pathlib import Path
import re
import struct
import threading
import time
import unicodedata
import urllib.error
import urllib.request
import zlib

ROOT = Path(__file__).resolve().parents[2]
CDN = "https://resources.premierleague.com/premierleague25/photos/players/110x140/"
TEAMS = {"arsenal": "3", "manchester-city": "43", "liverpool": "14", "chelsea": "8",
         "manchester-united": "1", "tottenham-hotspur": "6"}


def fetch(url):
    with urllib.request.urlopen(url, timeout=45) as response:
        return response.read(), response.headers.get_content_type()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def normalized(value):
    value = value.lower().replace("ø", "o")
    return "".join(c for c in unicodedata.normalize("NFKD", value)
                   if not unicodedata.combining(c))


def png_info(data):
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Not PNG bytes")
    width, height, depth, color, compression, filtering, interlace = struct.unpack(">IIBBBBB", data[16:29])
    if not (108 <= width <= 220 and 138 <= height <= 280 and abs(width / height - 110 / 140) < .02) or depth != 8 or color not in (2, 6) or interlace:
        raise ValueError("Unexpected portrait dimensions or PNG format")
    channels = 4 if color == 6 else 3
    offset, compressed = 8, bytearray()
    while offset < len(data):
        size = struct.unpack(">I", data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        if kind == b"IDAT":
            compressed.extend(data[offset + 8:offset + 8 + size])
        offset += size + 12
    stride = width * channels
    decoder = zlib.decompressobj()
    rows = decoder.decompress(compressed, (stride + 1) * height + 1)
    if not decoder.eof or len(rows) != (stride + 1) * height:
        raise ValueError("Invalid bounded PNG pixel stream")
    previous, colors, visible = bytearray(stride), set(), 0
    for row in range(height):
        start = row * (stride + 1)
        method = rows[start]
        current = bytearray(rows[start + 1:start + 1 + stride])
        for i in range(stride):
            left = current[i - channels] if i >= channels else 0
            above = previous[i]
            corner = previous[i - channels] if i >= channels else 0
            if method == 1:
                predictor = left
            elif method == 2:
                predictor = above
            elif method == 3:
                predictor = (left + above) // 2
            elif method == 4:
                p = left + above - corner
                choices = [(abs(p - left), left), (abs(p - above), above), (abs(p - corner), corner)]
                predictor = min(enumerate(choices), key=lambda pair: (pair[1][0], pair[0]))[1][1]
            elif method == 0:
                predictor = 0
            else:
                raise ValueError("Unknown PNG filter")
            current[i] = (current[i] + predictor) & 255
        for i in range(0, stride, channels):
            if channels == 3 or current[i + 3] > 0:
                visible += 1
                colors.add(bytes(current[i:i + 3]))
        previous = current
    if visible < 1000 or len(colors) < 100:
        raise ValueError("Portrait has insufficient actual nontransparent pixels/colors")
    return width, height, visible, len(colors)


def roster():
    result = {}
    regulation = (ROOT / "src/PitchLens.Generation/RegulationRoster.cs").read_text()
    for local_id, club, name, number in re.findall(
            r'new(?: MatchRosterPlayer)?\("([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*(\d+),', regulation):
        result[local_id] = (club, name, int(number))
    catalog = (ROOT / "src/PitchLens.Generation/FixtureCatalog.cs").read_text()
    for club, surname, name, number in re.findall(r'P\("([^"]+)","([^"]+)","([^"]+)",(\d+),', catalog):
        result[club + "-" + surname] = (club, name, int(number))
    if len(result) != 66:
        raise ValueError("Expected exact selected six-club roster")
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--resume-images-only", action="store_true",
                        help="Use previously verified identity evidence; do not request profile HTML or squad data again.")
    parser.add_argument("--image-cache", type=Path, help="Validate already captured PNG bytes without new HTTP requests.")
    args = parser.parse_args()
    args.evidence.mkdir(parents=True, exist_ok=True)
    identities = json.loads((Path(__file__).parent / "identities.json").read_text())
    local = roster()
    if {item[0] for item in identities} != set(local) or len(identities) != 66:
        raise ValueError("Reviewed mapping must cover each selected player exactly once")
    output = ROOT / "src/PitchLens.Generation/Resources/player-portraits.json"
    destination = ROOT / "src/PitchLens.App/wwwroot/images/players"
    if args.resume_images_only:
        document = json.loads(output.read_text())
        by_id = {entry["playerId"]: entry for entry in document["entries"]}
        limited = False
        for local_id, official_id, official_name in identities:
            entry = by_id[local_id]
            if entry["officialId"] != official_id or entry["officialName"] != official_name:
                raise ValueError("Cached mapping differs from reviewed identities")
            evidence = (args.evidence / (local_id + ".json")).read_bytes()
            if digest(evidence) != entry["identityEvidenceSha256"]:
                raise ValueError("Cached official identity evidence changed")
            if entry["imagePath"]:
                if digest((ROOT / "src/PitchLens.App/wwwroot" / entry["imagePath"]).read_bytes()) != entry["sha256"]:
                    raise ValueError("Previously verified asset bytes changed")
                continue
            if "403" in entry.get("unavailableReason", ""):
                continue
            entry["profileVerification"] = "Official API name/id/number verified; profile HTML returned429 and was not retried."
            if limited:
                entry["unavailableReason"] = "CDN rate limit encountered; remaining requests were not attempted."
                continue
            try:
                if args.image_cache:
                    cached = args.image_cache / (official_id + ".png")
                    if not cached.is_file():
                        entry["unavailableReason"] = "No captured image available for offline validation."
                        continue
                    data, mime = cached.read_bytes(), "image/png"
                else:
                    data, mime = fetch(CDN + official_id + ".png")
                if mime != "image/png" or len(data) > 300000 or digest(data) == document["placeholderSha256"]:
                    raise ValueError("Not a bounded genuine PNG portrait, or official placeholder")
                width, height, visible, colors = png_info(data)
                entry.update(imagePath=f"images/players/{local_id}.png", sha256=digest(data), bytes=len(data),
                             width=width, height=height, mime=mime, visiblePixels=visible, uniqueColors=colors,
                             unavailableReason=None)
                (destination / (local_id + ".png")).write_bytes(data)
            except (urllib.error.URLError, TimeoutError, ValueError) as error:
                entry["unavailableReason"] = str(error)
                if isinstance(error, urllib.error.HTTPError) and error.code == 429:
                    limited = True
                    entry["retryAfter"] = error.headers.get("Retry-After")
            time.sleep(.5)
        present = [e for e in document["entries"] if e["imagePath"]]
        if len({e["sha256"] for e in present}) != len(present):
            raise ValueError("Identical images assigned to different identities")
        output.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps({"verifiedIdentities": len(by_id), "bundled": len(present), "bytes": sum(e["bytes"] for e in present),
                          "missing": [{"id": e["playerId"], "reason": e["unavailableReason"]} for e in document["entries"] if not e["imagePath"]]}, indent=2))
        return
    squads = {}
    for club, team in TEAMS.items():
        url = f"https://sdp-prem-prod.premier-league-prod.pulselive.com/api/v2/competitions/8/seasons/2026/teams/{team}/squad"
        body, _ = fetch(url)
        squads[club] = (url, json.loads(body)["players"])
    placeholder, _ = fetch(CDN + "placeholder.png")
    placeholder_hash = digest(placeholder)
    destination.mkdir(parents=True, exist_ok=True)
    profile_limited = threading.Event()

    def import_one(reviewed):
        local_id, official_id, expected_name = reviewed
        club, catalog_name, number = local[local_id]
        source_url, players = squads[club]
        matches = [p for p in players if p["id"] == official_id]
        note = "Official current selected-club squad record."
        if local_id == "arsenal-martinelli":
            source_url = "https://sdp-prem-prod.premier-league-prod.pulselive.com/api/v1/players/444145"
            body, _ = fetch(source_url)
            matches = [p for p in json.loads(body)
                       if p["id"]["playerId"] == official_id and p["id"]["seasonId"] == "2025"
                       and p["id"]["competitionId"] == "1" and p["currentTeam"]["id"] == "3"]
            note = "Official historical Arsenal profile record; absent from current squad feed. Catalog snapshot unchanged."
        if len(matches) != 1 or matches[0]["name"]["display"] != expected_name or matches[0]["shirtNum"] != number:
            raise ValueError(f"Unverified identity association: {local_id}")
        evidence = {"officialPlayerId": official_id, "officialName": expected_name,
                    "shirtNumber": number, "officialClubId": TEAMS[club], "sourceUrl": source_url, "note": note}
        evidence_bytes = (json.dumps(evidence, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()
        (args.evidence / (local_id + ".json")).write_bytes(evidence_bytes)
        slug = re.sub(r"[^a-z0-9]+", "-", normalized(expected_name)).strip("-")
        profile = f"https://www.premierleague.com/en/players/{official_id}/{slug}/overview"
        entry = {"playerId": local_id, "teamId": club, "name": catalog_name, "officialName": expected_name,
                 "officialId": official_id, "identitySourceUrl": source_url, "identityEvidence": evidence,
                 "identityEvidenceSha256": digest(evidence_bytes), "profileUrl": profile,
                 "imageUrl": CDN + official_id + ".png", "imagePath": None}
        if profile_limited.is_set():
            entry["unavailableReason"] = "Profile HTML rate limit encountered; remaining HTML requests were not attempted."
            return entry
        try:
            page, _ = fetch(profile)
            text = page.decode("utf-8")
            title = html.unescape(re.search(r"<title>(.*?)</title>", text, re.S)[1])
            if f'data-player-id="{official_id}"' not in text or normalized(expected_name) not in normalized(title):
                raise ValueError("Official profile name/ID association mismatch")
            entry["profileTitle"] = title
            data, mime = fetch(entry["imageUrl"])
            if mime != "image/png" or len(data) > 300000 or digest(data) == placeholder_hash:
                raise ValueError("Not a bounded genuine PNG portrait, or official placeholder")
            width, height, visible, colors = png_info(data)
            entry.update(imagePath=f"images/players/{local_id}.png", sha256=digest(data), bytes=len(data),
                         width=width, height=height, mime=mime, visiblePixels=visible, uniqueColors=colors,
                         unavailableReason=None)
            (destination / (local_id + ".png")).write_bytes(data)
        except (urllib.error.URLError, TimeoutError, ValueError) as error:
            entry["unavailableReason"] = str(error)
            if isinstance(error, urllib.error.HTTPError) and error.code == 429:
                profile_limited.set()
                entry["retryAfter"] = error.headers.get("Retry-After")
        return entry

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        entries = list(pool.map(import_one, identities))
    present = [e for e in entries if e["imagePath"]]
    if len({e["sha256"] for e in present}) != len(present):
        raise ValueError("Identical images assigned to different selected identities")
    document = {"checkedOn": "2026-10-22", "listingUrl": "https://www.premierleague.com/en/players?competition=8&season=2026",
                "rights": "User confirmed permission for this non-commercial project. Third-party image rights retained; no blanket website grant, endorsement or open-source asset license is claimed.",
                "imageRuleSource": "https://www.premierleague.com/resources/v1.55.23/scripts/bundle-es.min.js",
                "imageRule": "Official playerOptaId selects photos/players/{size}/{id}.png; official placeholder is not used.",
                "placeholderSha256": placeholder_hash, "entries": entries}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"verifiedIdentities": len(entries), "bundled": len(present), "bytes": sum(e["bytes"] for e in present),
                      "missing": [{"id": e["playerId"], "reason": e["unavailableReason"]} for e in entries if not e["imagePath"]]}, indent=2))


if __name__ == "__main__":
    main()
