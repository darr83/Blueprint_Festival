#!/usr/bin/env python3
"""Makes the game's sound effects with ElevenLabs.

Reads game/Audio/sounds.json and, for every sound that has no file yet, asks ElevenLabs for it
from the prompt in the list and saves it as game/Audio/<id>.ogg (or <id>.mp3 if ffmpeg is not
installed). The game plays whatever files are there and is silent for the rest.

    ELEVENLABS_API_KEY=...  python3 tools/make_sounds.py            make what is missing
                            python3 tools/make_sounds.py --list     show what is there and what is not
                            python3 tools/make_sounds.py --only till,fry --force    make these again

The key is read from the ELEVENLABS_API_KEY environment variable and is never printed or saved.
The acts' music is not made here.
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

AUDIO = Path(__file__).resolve().parent.parent / "game" / "Audio"
URL = "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128"
ENDINGS = ("ogg", "wav", "mp3")


def existing(sound_id):
    return next((AUDIO / f"{sound_id}.{e}" for e in ENDINGS if (AUDIO / f"{sound_id}.{e}").exists()), None)


def ask(key, sound):
    body = json.dumps({
        "text": sound["prompt"],
        "duration_seconds": sound["seconds"],
        "loop": sound["loop"],
        "prompt_influence": 0.45,
    }).encode()
    request = urllib.request.Request(URL, data=body, headers={"xi-api-key": key, "Content-Type": "application/json"})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")[:300]
            if error.code in (429, 500, 502, 503) and attempt < 3:
                time.sleep(5 * (attempt + 1))
                continue
            raise RuntimeError(f"ElevenLabs said {error.code}: {detail}") from None
        except urllib.error.URLError as error:
            if attempt < 3:
                time.sleep(5 * (attempt + 1))
                continue
            raise RuntimeError(f"Could not reach ElevenLabs: {error.reason}") from None


def save(sound_id, mp3):
    for ending in ENDINGS:
        (AUDIO / f"{sound_id}.{ending}").unlink(missing_ok=True)
        (AUDIO / f"{sound_id}.{ending}.import").unlink(missing_ok=True)
    raw = AUDIO / f"{sound_id}.mp3"
    raw.write_bytes(mp3)
    if not shutil.which("ffmpeg"):
        return raw
    # Ogg loops without the tiny gap an mp3 leaves at the join.
    ogg = AUDIO / f"{sound_id}.ogg"
    done = subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(raw), "-c:a", "libvorbis", "-q:a", "5", str(ogg)], check=False)
    if done.returncode != 0 or not ogg.exists():
        ogg.unlink(missing_ok=True)
        return raw
    raw.unlink()
    return ogg


def main():
    parser = argparse.ArgumentParser(description="Make the game's sound effects with ElevenLabs.")
    parser.add_argument("--only", help="make only these sounds: names with commas between them")
    parser.add_argument("--force", action="store_true", help="make a sound again even if its file is there")
    parser.add_argument("--list", action="store_true", help="show what is there and what is missing, and make nothing")
    args = parser.parse_args()

    sounds = json.loads((AUDIO / "sounds.json").read_text())["sounds"]
    if args.only:
        wanted = set(args.only.split(","))
        unknown = wanted - {s["id"] for s in sounds}
        if unknown:
            sys.exit(f"Not on the list: {', '.join(sorted(unknown))}")
        sounds = [s for s in sounds if s["id"] in wanted]

    if args.list:
        for sound in sounds:
            found = existing(sound["id"])
            print(f"{'have' if found else 'MISSING':8}{sound['id']:18}{sound['seconds']:>5}s  {found.name if found else ''}")
        print(f"{sum(1 for s in sounds if existing(s['id']))} of {len(sounds)} sounds have files.")
        return

    todo = [s for s in sounds if args.force or not existing(s["id"])]
    if not todo:
        print("Every sound already has a file. Use --force to make them again.")
        return
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key:
        sys.exit("Set ELEVENLABS_API_KEY first. Nothing was made.")

    failed = []
    for number, sound in enumerate(todo, 1):
        try:
            path = save(sound["id"], ask(key, sound))
            print(f"[{number}/{len(todo)}] {sound['id']}: {path.name}, {path.stat().st_size // 1024} KB", flush=True)
        except RuntimeError as error:
            failed.append(sound["id"])
            print(f"[{number}/{len(todo)}] {sound['id']}: FAILED. {error}", flush=True)
    print(f"Made {len(todo) - len(failed)} of {len(todo)}." + (f" Failed: {', '.join(failed)}" if failed else ""))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
