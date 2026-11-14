#!/usr/bin/env python3
"""Makes the PA announcements that introduce each act a few minutes before they go on.

The lines are in game/Audio/Announcements/announcements.json, two for each artist, each read by
one of the same ElevenLabs voices as the crush announcements, then made to sound like a field PA
(thin, with an echo). Files already there are left alone unless --force is given.

    python3 tools/make_announcements.py                     make any that are missing
    python3 tools/make_announcements.py --only intro_dj_otter_1 --force    make this one again
    python3 tools/make_announcements.py --cost              how many characters (credits) it would use

The key is read from the ELEVENLABS_API_KEY environment variable and is never printed or saved.
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

FOLDER = Path(__file__).resolve().parent.parent / "game" / "Audio" / "Announcements"
SPEECH_URL = "https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"
PA_FILTER = ("highpass=f=280,lowpass=f=4200,acompressor=threshold=-18dB:ratio=4:attack=5:release=80,"
             "aecho=0.8:0.55:180|420:0.32|0.18,apad=pad_dur=0.6,loudnorm=I=-16:TP=-1.5")


def ask(key, line):
    body = json.dumps({
        "text": line["prompt"],
        "model_id": "eleven_multilingual_v2",
        "voice_settings": {"stability": 0.45, "similarity_boost": 0.75, "style": 0.35},
    }).encode()
    request = urllib.request.Request(SPEECH_URL.format(voice=line["voice"]), data=body,
                                     headers={"xi-api-key": key, "Content-Type": "application/json"})
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
    raise RuntimeError("ElevenLabs did not answer.")


def save(line, mp3):
    raw = FOLDER / f"{line['id']}.mp3"
    raw.write_bytes(mp3)
    if not shutil.which("ffmpeg"):
        return raw
    ogg = FOLDER / f"{line['id']}.ogg"
    delay = int(line.get("lead", 0) * 1000)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(raw), "-af", f"{PA_FILTER},adelay={delay}:all=1",
                    "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5", str(ogg)], check=True)
    raw.unlink()
    return ogg


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--cost", action="store_true")
    args = parser.parse_args()
    lines = json.loads((FOLDER / "announcements.json").read_text())["announcements"]
    only = {name for name in args.only.split(",") if name}
    todo = [line for line in lines if (not only or line["id"] in only)
            and (args.force or not any((FOLDER / f"{line['id']}.{e}").exists() for e in ("ogg", "mp3")))]
    print(f"{len(todo)} to make, {sum(len(line['prompt']) for line in todo)} characters.")
    if args.cost or not todo:
        return
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        sys.exit("Set ELEVENLABS_API_KEY first.")
    failed = []
    for number, line in enumerate(todo, 1):
        try:
            path = save(line, ask(key, line))
            print(f"[{number}/{len(todo)}] {line['id']}: {path.stat().st_size // 1024} KB", flush=True)
        except RuntimeError as error:
            failed.append(line["id"])
            print(f"[{number}/{len(todo)}] {line['id']}: {error}", flush=True)
            if "quota" in str(error).lower():
                break
    if failed:
        sys.exit(f"Not made: {', '.join(failed)}")


if __name__ == "__main__":
    main()
