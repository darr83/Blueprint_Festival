#!/usr/bin/env python3
"""Makes the game's sound effects with ElevenLabs.

Reads game/Audio/sounds.json and, for every sound that has no file yet, asks ElevenLabs for it
from the prompt in the list and saves it as game/Audio/<id>.ogg (or <id>.mp3 if ffmpeg is not
installed). The game plays whatever files are there and is silent for the rest.

    ELEVENLABS_API_KEY=...  python3 tools/make_sounds.py            make what is missing
                            python3 tools/make_sounds.py --list     show what is there and what is not
                            python3 tools/make_sounds.py --only till,fry --force    make these again
                            python3 tools/make_sounds.py --level    bring every file to the same peak

Sounds with a "voice" are spoken announcements: the prompt is read out by that ElevenLabs voice,
then made to sound like a field PA (thin, with an echo) with "lead" seconds of quiet first.

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
SPEECH_URL = "https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"
PA_FILTER = ("highpass=f=280,lowpass=f=4200,acompressor=threshold=-18dB:ratio=4:attack=5:release=80,"
             "aecho=0.8:0.55:180|420:0.32|0.18,apad=pad_dur=0.6,loudnorm=I=-16:TP=-1.5")
ENDINGS = ("ogg", "wav", "mp3")
PEAK_DB = -1.0


def existing(sound_id):
    return next((AUDIO / f"{sound_id}.{e}" for e in ENDINGS if (AUDIO / f"{sound_id}.{e}").exists()), None)


def ask(key, sound):
    if sound.get("voice"):
        url = SPEECH_URL.format(voice=sound["voice"])
        body = json.dumps({
            "text": sound["prompt"],
            "model_id": "eleven_multilingual_v2",
            "voice_settings": {"stability": 0.5, "similarity_boost": 0.75, "style": 0.2},
        }).encode()
    else:
        url = URL
        body = json.dumps({
            "text": sound["prompt"],
            "duration_seconds": sound["seconds"],
            "loop": sound["loop"],
            "prompt_influence": 0.45,
        }).encode()
    request = urllib.request.Request(url, data=body, headers={"xi-api-key": key, "Content-Type": "application/json"})
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


def peak_db(path):
    """The loudest point in a sound file, in dB below full scale."""
    found = subprocess.run(["ffmpeg", "-i", str(path), "-af", "volumedetect", "-f", "null", "-"], capture_output=True, text=True)
    for line in found.stderr.splitlines():
        if "max_volume:" in line:
            return float(line.split("max_volume:")[1].split()[0])
    return PEAK_DB


def level(path):
    """Brings a file already made to the same peak as the rest."""
    gain = PEAK_DB - peak_db(path)
    if abs(gain) < 0.5:
        return False
    out = path.with_name(path.stem + ".levelled" + path.suffix)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(path), "-af", f"volume={gain:.2f}dB",
                    "-c:a", "libvorbis", "-q:a", "5", str(out)], check=True)
    out.replace(path)
    return True


def keep_from_onset(path, seconds):
    """Cuts a one-hit sound (a clap) so the hit is right at the start and nothing comes after it.
    The game plays these on the beat, so any quiet lead-in would make them late."""
    out = path.with_name(path.stem + ".cut" + path.suffix)
    fade = min(0.12, seconds / 3)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(path), "-af",
                    f"silenceremove=start_periods=1:start_threshold=-30dB:start_silence=0.005,"
                    f"atrim=0:{seconds},afade=t=out:st={seconds - fade:.3f}:d={fade:.3f}",
                    "-c:a", "libvorbis", "-q:a", "5", str(out)], check=True)
    out.replace(path)


def save_speech(sound_id, mp3, lead):
    """An announcement over the field PA: thin and echoing, after a moment's quiet."""
    for ending in ENDINGS:
        (AUDIO / f"{sound_id}.{ending}").unlink(missing_ok=True)
    raw = AUDIO / f"{sound_id}.mp3"
    raw.write_bytes(mp3)
    if not shutil.which("ffmpeg"):
        return raw
    ogg = AUDIO / f"{sound_id}.ogg"
    delay = int(lead * 1000)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(raw), "-af", f"{PA_FILTER},adelay={delay}:all=1",
                    "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5", str(ogg)], check=True)
    raw.unlink()
    return ogg


def save(sound_id, mp3, keep=None):
    for ending in ENDINGS:
        (AUDIO / f"{sound_id}.{ending}").unlink(missing_ok=True)
        (AUDIO / f"{sound_id}.{ending}.import").unlink(missing_ok=True)
    raw = AUDIO / f"{sound_id}.mp3"
    raw.write_bytes(mp3)
    if not shutil.which("ffmpeg"):
        return raw
    # Ogg loops without the tiny gap an mp3 leaves at the join. ElevenLabs makes some sounds very
    # quiet, so every file is brought up (or down) to the same peak; sounds.json sets the mix.
    ogg = AUDIO / f"{sound_id}.ogg"
    gain = PEAK_DB - peak_db(raw)
    done = subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(raw), "-af", f"volume={gain:.2f}dB",
                           "-c:a", "libvorbis", "-q:a", "5", str(ogg)], check=False)
    if done.returncode != 0 or not ogg.exists():
        ogg.unlink(missing_ok=True)
        return raw
    raw.unlink()
    if keep:
        keep_from_onset(ogg, keep)
    return ogg


def main():
    parser = argparse.ArgumentParser(description="Make the game's sound effects with ElevenLabs.")
    parser.add_argument("--only", help="make only these sounds: names with commas between them")
    parser.add_argument("--force", action="store_true", help="make a sound again even if its file is there")
    parser.add_argument("--level", action="store_true", help="bring the .ogg files already made to the same peak, and make nothing")
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

    if args.level:
        changed = [s["id"] for s in sounds if (path := existing(s["id"])) and path.suffix == ".ogg" and level(path)]
        print(f"Levelled {len(changed)} of {len(sounds)}.")
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
            path = (save_speech(sound["id"], ask(key, sound), sound.get("lead", 0)) if sound.get("voice")
                    else save(sound["id"], ask(key, sound), sound.get("keep")))
            print(f"[{number}/{len(todo)}] {sound['id']}: {path.name}, {path.stat().st_size // 1024} KB", flush=True)
        except RuntimeError as error:
            failed.append(sound["id"])
            print(f"[{number}/{len(todo)}] {sound['id']}: FAILED. {error}", flush=True)
    print(f"Made {len(todo) - len(failed)} of {len(todo)}." + (f" Failed: {', '.join(failed)}" if failed else ""))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
