"""Generate the 16x16 toolbar icons of the Commands and Containers panes into SuperPutty/Resources.

Each icon is drawn at 4x and scaled down, so edges are smooth. Edit the drawing functions below and run:

    python tools/pane-icons/make_pane_icons.py            # writes the icons
    python tools/pane-icons/make_pane_icons.py --preview  # also writes preview.png (8x, labelled)

Needs Pillow. The icons are embedded resources (Properties/Resources.resx), not installer files.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "SuperPutty", "Resources"))

S = 64  # drawing size; scaled to 16
GREEN = (46, 160, 67, 255)
RED = (207, 34, 46, 255)
BLUE = (31, 111, 196, 255)
GREY = (110, 118, 129, 255)
DARK = (60, 60, 60, 255)
YELLOW = (240, 190, 50, 255)
WHITE = (255, 255, 255, 255)


def add(d):
    d.ellipse((4, 4, 60, 60), fill=GREEN)
    d.rectangle((28, 16, 36, 48), fill=WHITE)
    d.rectangle((16, 28, 48, 36), fill=WHITE)


def edit(d):
    # pencil from bottom left to top right
    body = [(14, 42), (42, 14), (50, 22), (22, 50)]
    d.polygon(body, fill=YELLOW, outline=DARK)
    d.line(body + [body[0]], fill=DARK, width=4)
    d.polygon([(14, 42), (22, 50), (8, 56)], fill=DARK)          # tip
    d.polygon([(42, 14), (48, 8), (56, 16), (50, 22)], fill=RED)  # eraser
    d.line([(42, 14), (48, 8), (56, 16), (50, 22)], fill=DARK, width=4)


def delete(d):
    for a, b in (((14, 14), (50, 50)), ((50, 14), (14, 50))):
        d.line([a, b], fill=RED, width=12)
    for x, y in ((14, 14), (50, 50), (50, 14), (14, 50)):
        d.ellipse((x - 6, y - 6, x + 6, y + 6), fill=RED)


def refresh(d):
    # clockwise arc with a gap at the top right; PIL angles run clockwise from 3 o'clock
    cx, cy, r, end = 32, 32, 22, 250
    d.arc((cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4), start=-20, end=end, fill=BLUE, width=9)
    # arrowhead at the end of the arc, pointing along it
    a = math.radians(end)
    px, py = cx + (r + 0.5) * math.cos(a), cy + (r + 0.5) * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)   # direction of travel
    nx, ny = math.cos(a), math.sin(a)    # outwards
    d.polygon([(px + tx * 16, py + ty * 16), (px + nx * 13, py + ny * 13), (px - nx * 13, py - ny * 13)], fill=BLUE)


def show_stopped(d):
    d.ellipse((4, 4, 60, 60), outline=GREY, width=6)
    d.rounded_rectangle((20, 20, 44, 44), radius=4, fill=GREY)


ICONS = {
    "pane_add": add,
    "pane_edit": edit,
    "pane_delete": delete,
    "pane_refresh": refresh,
    "pane_show_stopped": show_stopped,
}


def render(draw_fn):
    big = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(big))
    return big.resize((16, 16), Image.LANCZOS)


def main():
    images = {}
    for name, fn in ICONS.items():
        image = render(fn)
        image.save(os.path.join(OUT, name + ".png"))
        images[name] = image
        print("wrote", name + ".png")
    if "--preview" in sys.argv:
        cell = 16 * 8
        preview = Image.new("RGBA", (cell * len(images), cell + 20), (255, 255, 255, 255))
        d = ImageDraw.Draw(preview)
        for i, (name, image) in enumerate(images.items()):
            preview.paste(image.resize((cell, cell), Image.NEAREST), (i * cell, 0))
            d.text((i * cell + 4, cell + 4), name.replace("pane_", ""), fill=(0, 0, 0, 255))
        preview.save(os.path.join(HERE, "preview.png"))
        print("wrote preview.png")


if __name__ == "__main__":
    main()
