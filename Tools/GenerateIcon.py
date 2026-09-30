#!/usr/bin/env python3
"""
NO UNIT 404 - application icon.

Draws the unit number plate that the game is named after and writes it to
Assets/_Project/Art/Icon/AppIcon.png. Editor/ReleaseSetup.cs is what hands it
to PlayerSettings; this only makes the file.

The mark has to survive 32 px in a Windows taskbar, so it is three numerals at
high contrast on a dark plate and nothing else. The one piece of storytelling
it can afford is that the middle zero is cut out rather than drawn - the unit
is a hole in the building, not a door (GDD 4.2).

Usage:  python Tools/GenerateIcon.py [--out <path>] [--size N]
"""

import argparse
import os

from PIL import Image, ImageDraw, ImageFont

# GDD 16.3 palette.
BACKGROUND = (22, 24, 27, 255)
PLATE = (45, 49, 52, 255)
PLATE_EDGE = (68, 73, 76, 255)
NUMERAL = (224, 227, 228, 255)
GHOST = (92, 98, 102, 255)
ACCENT = (76, 164, 164, 255)

SIZES = (1024, 512, 256, 128, 64, 48, 32, 16)

FONT = os.path.join("Assets", "_Project", "Resources", "NO404", "Fonts", "Pretendard-Regular.otf")


def rounded(draw, box, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def build(size):
    """
    One icon at one size.

    Small sizes are not the big one shrunk. Below 128 px the hollow zero washes out
    into the plate and the mark reads as "4 4", which is a different number and a
    worse joke, so the small variants fill the zero in and drop the plate edge and
    the accent rule - detail that turns to mush is worse than no detail.
    """
    ghost_zero = size >= 128
    fine_detail = size >= 64

    # Drawn at 4x and downsampled: PIL has no antialiased vector fill, and the
    # numerals fringe badly at small sizes without it.
    scale = 4
    n = size * scale
    image = Image.new("RGBA", (n, n), BACKGROUND)
    draw = ImageDraw.Draw(image)

    # The small variants give the plate the whole tile; there is no room to spend
    # on framing when the numerals have 24 px to be legible in.
    margin = int(n * (0.10 if fine_detail else 0.045))
    top_edge = int(n * (0.15 if fine_detail else 0.10))
    bottom_edge = int(n * (0.19 if fine_detail else 0.10))

    plate = (margin, top_edge, n - margin, n - bottom_edge)
    rounded(draw, plate, radius=int(n * 0.045), fill=PLATE,
            outline=PLATE_EDGE if fine_detail else None,
            width=max(1, int(n * 0.008)))

    plate_height = plate[3] - plate[1]
    font = ImageFont.truetype(FONT, int(plate_height * (0.62 if fine_detail else 0.72)))

    text = "404"
    left, top, right, bottom = draw.textbbox((0, 0), text, font=font)
    x = (n - (right - left)) / 2 - left
    y = plate[1] + (plate_height - (bottom - top)) / 2 - top
    draw.text((x, y), text, font=font, fill=NUMERAL)

    if ghost_zero:
        # Hollow the middle zero out. Erasing with the glyph itself rather than with
        # a rectangle matters: a box guessed from the advance width leaves slivers of
        # the curve behind at either side, which read as dirt rather than as design.
        zero_x = x + draw.textlength("4", font=font)
        draw.text((zero_x, y), "0", font=font, fill=PLATE)
        draw.text((zero_x, y), "0", font=font, fill=PLATE,
                  stroke_width=max(1, int(n * 0.006)), stroke_fill=GHOST)

    if fine_detail:
        # One accent rule under the plate: the teal the whole UI is keyed to.
        rule_y = plate[3] + int(n * 0.045)
        rule_half = int(n * 0.14)
        draw.rectangle((n // 2 - rule_half, rule_y,
                        n // 2 + rule_half, rule_y + max(1, int(n * 0.016))),
                       fill=ACCENT)

    return image.resize((size, size), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description="Render the NO UNIT 404 app icons.")
    parser.add_argument("--out", default=os.path.join("Assets", "_Project", "Art", "Icon"))
    args = parser.parse_args()

    if not os.path.exists(FONT):
        raise SystemExit("font not found at " + FONT + "; the icon uses the same face as the UI")

    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    for size in SIZES:
        path = os.path.join(out_dir, "AppIcon_%d.png" % size)
        build(size).save(path)
        print("  AppIcon_%-4d %7.1f KB" % (size, os.path.getsize(path) / 1024.0))

    print("%d sizes -> %s" % (len(SIZES), out_dir))


if __name__ == "__main__":
    main()
