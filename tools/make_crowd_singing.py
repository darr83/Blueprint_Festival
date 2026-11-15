#!/usr/bin/env python3
"""Makes the crowd's singing for each song whose choruses have been marked.

For every song in game/Music that has a ".chorus" file beside it (made with "Mark the choruses" on
the game's main menu) and no crowd file yet, this:

  1. pulls the singer's voice out of the song with Demucs (a free vocal separator), then
  2. turns that one voice into a field of people singing it: many copies, each a little out of
     tune and out of time, spread from left to right, the top and bottom taken off and a big
     outdoor echo added,

and saves it beside the song as "<song>.crowd.ogg" ("01 Hay Fever.mp3" gets "01 Hay Fever.crowd.ogg").
The game plays it quietly under the song in the marked choruses, louder the more people sing.
The whole song is made, so moving or adding choruses later needs nothing made again.

    pip install demucs soundfile          (once; needs ffmpeg with rubberband too)
    python3 tools/make_crowd_singing.py                  make what is missing
    python3 tools/make_crowd_singing.py --force          make them all again
    python3 tools/make_crowd_singing.py --music DIR      use another Music folder
    python3 tools/make_crowd_singing.py --song "01 Hay Fever.mp3"   only songs with this in the name
"""

import argparse
import random
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

MUSIC = Path(__file__).resolve().parent.parent / "game" / "Music"
ENDINGS = (".mp3", ".ogg", ".wav")

# The crowd: how many copies of the voice, how far out of tune (cents) and out of time (ms) each
# can be, and how loud the finished crowd is.
VOICES = 10
MOST_CENTS = 28
MOST_LATE_MS = 90
LOUDNESS = -20


def is_song(path):
    stem = path.name[: -len(path.suffix)]
    return path.suffix.lower() in ENDINGS and not stem.lower().endswith((".crowd", ".vocals"))


def crowd_file(song):
    return song.with_name(song.name[: -len(song.suffix)] + ".crowd.ogg")


def separate(song, work):
    """The singing on its own, as a wav file."""
    subprocess.run([sys.executable, "-m", "demucs", "--two-stems", "vocals", "-n", "htdemucs", "-o", str(work), str(song)],
                   check=True, capture_output=True)
    found = list(Path(work).rglob("vocals.wav"))
    if not found:
        raise RuntimeError("Demucs made no vocals file")
    return found[0]


def crowd_filter(seed):
    """The ffmpeg filter that turns one voice into a crowd, the same every time for the same song."""
    rng = random.Random(seed)
    parts = [f"[0:a]aformat=sample_rates=44100:channel_layouts=mono,asplit={VOICES}" + "".join(f"[v{i}]" for i in range(VOICES))]
    for i in range(VOICES):
        cents = rng.uniform(-MOST_CENTS, MOST_CENTS)
        late = rng.uniform(0, MOST_LATE_MS)
        pan = -0.9 + (1.8 * i / (VOICES - 1))
        level = rng.uniform(0.55, 1.0)
        left = level * (1 - pan) / 2
        right = level * (1 + pan) / 2
        # A slow wobble in each voice's timing, as people never quite stay together.
        wobble = rng.uniform(0.15, 0.5)
        parts.append(
            f"[v{i}]rubberband=pitch={2 ** (cents / 1200):.5f}:formant=preserved,"
            f"adelay={late:.0f},chorus=0.6:0.9:{rng.uniform(20, 45):.0f}:0.35:{wobble:.2f}:2,"
            f"pan=stereo|c0={left:.3f}*c0|c1={right:.3f}*c0[c{i}]")
    parts.append("".join(f"[c{i}]" for i in range(VOICES)) + f"amix=inputs={VOICES}:normalize=0,"
                 "highpass=f=220,lowpass=f=3600,"
                 "aecho=0.8:0.6:70|140|230|340:0.35|0.28|0.2|0.14,"
                 "acompressor=threshold=-22dB:ratio=3:attack=20:release=250,"
                 f"loudnorm=I={LOUDNESS}:TP=-2[out]")
    return ";".join(parts)


def make(song, force):
    out = crowd_file(song)
    if out.exists() and not force:
        print(f"  {song.parent.name}/{song.name}: crowd already made")
        return False
    with tempfile.TemporaryDirectory() as work:
        print(f"  {song.parent.name}/{song.name}: finding the singing...", flush=True)
        vocals = separate(song, work)
        print(f"  {song.parent.name}/{song.name}: making the crowd...", flush=True)
        subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", str(vocals), "-filter_complex", crowd_filter(song.name),
                        "-map", "[out]", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "4", str(out)], check=True)
    print(f"  saved {out.name}")
    return True


def main():
    parser = argparse.ArgumentParser(description="Make the crowd's singing for songs whose choruses are marked.")
    parser.add_argument("--music", type=Path, default=MUSIC, help="the Music folder")
    parser.add_argument("--force", action="store_true", help="make crowd files again even if they are there")
    parser.add_argument("--song", default="", help="only songs with this in their file name")
    args = parser.parse_args()
    if not shutil.which("ffmpeg"):
        sys.exit("ffmpeg is needed (with rubberband).")
    songs = sorted(p for p in args.music.rglob("*") if p.is_file() and is_song(p)
                   and p.with_name(p.name + ".chorus").exists() and args.song in p.name)
    if not songs:
        print("No songs with marked choruses. Mark some with 'Mark the choruses' on the game's main menu.")
        return
    made = sum(make(song, args.force) for song in songs)
    print(f"Made {made} of {len(songs)}.")


if __name__ == "__main__":
    main()
