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


def host_info(d):
    # monitor with a pulse line, on a stand
    d.rounded_rectangle((4, 8, 60, 48), radius=6, fill=(44, 62, 80, 255))
    d.line([(10, 30), (22, 30), (28, 18), (36, 42), (42, 26), (46, 30), (54, 30)], fill=(80, 220, 120, 255), width=5, joint="curve")
    d.rectangle((28, 48, 36, 54), fill=GREY)
    d.rounded_rectangle((18, 54, 46, 60), radius=3, fill=GREY)


NAVY = (44, 62, 80, 255)
PURPLE = (136, 57, 190, 255)
ORANGE = (230, 126, 34, 255)
PAPER = (250, 250, 250, 255)


def page(d, box=(12, 4, 52, 60), lines=True):
    """A sheet of paper with a folded corner"""
    x0, y0, x1, y1 = box
    d.polygon([(x0, y0), (x1 - 12, y0), (x1, y0 + 12), (x1, y1), (x0, y1)], fill=PAPER, outline=DARK)
    d.line([(x0, y0), (x1 - 12, y0), (x1, y0 + 12), (x1, y1), (x0, y1), (x0, y0)], fill=DARK, width=4)
    d.polygon([(x1 - 12, y0), (x1 - 12, y0 + 12), (x1, y0 + 12)], fill=(200, 200, 200, 255))
    if lines:
        for y in range(y0 + 20, y1 - 6, 9):
            d.line([(x0 + 8, y), (x1 - 8, y)], fill=GREY, width=4)


def terminal(d, box=(4, 10, 60, 54)):
    """A small terminal window with a prompt"""
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=5, fill=NAVY)
    d.rectangle((x0, y0, x1, y0 + 8), fill=(90, 110, 130, 255))
    d.line([(x0 + 8, y0 + 18), (x0 + 16, y0 + 25), (x0 + 8, y0 + 32)], fill=WHITE, width=5)


def session_detail(d):
    page(d)
    d.ellipse((30, 30, 60, 60), fill=BLUE)
    d.rectangle((43, 42, 47, 55), fill=WHITE)
    d.rectangle((43, 34, 47, 38), fill=WHITE)


def log_viewer(d):
    page(d)
    d.ellipse((28, 30, 52, 54), outline=DARK, width=5)
    d.line([(48, 50), (60, 62)], fill=DARK, width=7)


def open_session(d):
    terminal(d)
    d.line([(28, 40), (44, 40)], fill=(80, 220, 120, 255), width=5)
    d.ellipse((34, 26, 58, 50), outline=YELLOW, width=5)
    d.line([(54, 46), (62, 54)], fill=YELLOW, width=6)


def recent_sessions(d):
    d.ellipse((4, 4, 60, 60), fill=BLUE)
    d.ellipse((10, 10, 54, 54), fill=WHITE)
    d.line([(32, 32), (32, 16)], fill=DARK, width=5)
    d.line([(32, 32), (44, 40)], fill=DARK, width=5)
    d.ellipse((28, 28, 36, 36), fill=DARK)


def copy_last_command(d):
    page(d, (22, 16, 60, 62), lines=False)
    terminal(d, (2, 2, 42, 34))
    d.line([(18, 22), (34, 22)], fill=WHITE, width=4)
    for y in (36, 45, 54):
        d.line([(30, y), (52, y)], fill=GREY, width=4)


def explain_claude(d):
    # speech bubble with a sparkle
    d.rounded_rectangle((4, 6, 60, 46), radius=10, fill=ORANGE)
    d.polygon([(14, 44), (28, 44), (12, 60)], fill=ORANGE)
    cx, cy = 32, 26
    d.polygon([(cx, cy - 14), (cx + 4, cy - 4), (cx + 14, cy), (cx + 4, cy + 4), (cx, cy + 14), (cx - 4, cy + 4), (cx - 14, cy), (cx - 4, cy - 4)], fill=WHITE)


def session_log(d):
    page(d)
    d.rounded_rectangle((30, 34, 60, 60), radius=4, fill=NAVY)
    d.line([(36, 41), (42, 47), (36, 53)], fill=WHITE, width=4)


def log_folder(d):
    d.polygon([(4, 14), (24, 14), (30, 20), (60, 20), (60, 56), (4, 56)], fill=YELLOW, outline=DARK)
    d.line([(4, 14), (24, 14), (30, 20), (60, 20), (60, 56), (4, 56), (4, 14)], fill=(150, 110, 0, 255), width=4)
    for y in (30, 39, 48):
        d.line([(16, y), (48, y)], fill=(150, 110, 0, 255), width=4)


def ssh_tunnels(d):
    # two hosts joined by a pipe with arrows both ways
    d.rounded_rectangle((2, 18, 18, 46), radius=3, fill=NAVY)
    d.rounded_rectangle((46, 18, 62, 46), radius=3, fill=NAVY)
    d.rectangle((18, 27, 46, 37), fill=(80, 220, 120, 255))
    d.polygon([(22, 32), (30, 24), (30, 40)], fill=DARK)
    d.polygon([(42, 32), (34, 24), (34, 40)], fill=DARK)


def run_on_hosts(d):
    for i, y in enumerate((6, 24, 42)):
        d.rounded_rectangle((2, y, 40, y + 14), radius=3, fill=NAVY)
        d.ellipse((30, y + 5, 35, y + 10), fill=(80, 220, 120, 255))
    d.polygon([(40, 18), (62, 32), (40, 46)], fill=GREEN)


def gear(d, color):
    cx, cy = 32, 32
    for i in range(8):
        a = math.radians(i * 45)
        x, y = cx + 22 * math.cos(a), cy + 22 * math.sin(a)
        d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=color)
    d.ellipse((10, 10, 54, 54), fill=color)
    d.ellipse((24, 24, 40, 40), fill=WHITE)


def putty_config(d):
    terminal(d, (2, 2, 44, 36))
    d.line([(16, 20), (30, 20)], fill=WHITE, width=4)
    # wrench
    d.line([(30, 58), (52, 36)], fill=GREY, width=9)
    d.ellipse((44, 24, 62, 42), fill=GREY)
    d.ellipse((51, 27, 61, 37), fill=(0, 0, 0, 0))


def options(d):
    gear(d, GREY)


ICONS = {
    "host_info_pane": host_info,
    "session_detail": session_detail,
    "log_viewer": log_viewer,
    "open_session": open_session,
    "recent_sessions": recent_sessions,
    "copy_last_command": copy_last_command,
    "explain_claude": explain_claude,
    "session_log": session_log,
    "log_folder": log_folder,
    "ssh_tunnels": ssh_tunnels,
    "run_on_hosts": run_on_hosts,
    "putty_config": putty_config,
    "options": options,
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
        cell = 16 * 6
        preview = Image.new("RGBA", (cell * len(images), cell + 20), (255, 255, 255, 255))
        d = ImageDraw.Draw(preview)
        for i, (name, image) in enumerate(images.items()):
            preview.paste(image.resize((cell, cell), Image.NEAREST), (i * cell, 0))
            d.text((i * cell + 4, cell + 4), name.replace("pane_", ""), fill=(0, 0, 0, 255))
        preview.save(os.path.join(HERE, "preview.png"))
        print("wrote preview.png")


if __name__ == "__main__":
    main()
