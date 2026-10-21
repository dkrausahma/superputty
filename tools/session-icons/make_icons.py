"""Generate the session icons <environment>-<role>.png (16x16) into the default theme.

Shape = role (server, web, database, ...), colour = environment (prod red, qual amber, dev green,
"role" = neutral blue). Edit the glyphs or the palette below and run:

    python tools/session-icons/make_icons.py            # writes the icons
    python tools/session-icons/make_icons.py --preview  # also writes preview.png (8x, labelled)

Needs Pillow. The installer lists every icon explicitly: add new files to SuperPuttyInstaller/Product.wxs.
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "SuperPutty", "Resources", "themes", "default", "icons"))

# environment -> (outline, fill); "W" pixels are always white
PALETTE = {
    "role": ((44, 90, 140), (156, 195, 232)),   # neutral, no environment
    "dev": ((30, 110, 45), (130, 205, 125)),
    "qual": ((150, 95, 0), (247, 190, 80)),
    "prod": ((150, 25, 25), (235, 105, 100)),
}

# D = outline, L = fill, W = white, . = transparent; glyphs are centred automatically
GLYPHS = {
    "server": """
DDDDDDDDDDDD
DLLLLLLLLLLD
DLWWWWWLLWLD
DLLLLLLLLLLD
DDDDDDDDDDDD
DLLLLLLLLLLD
DLWWWWWLLWLD
DLLLLLLLLLLD
DDDDDDDDDDDD
DLLLLLLLLLLD
DLWWWWWLLWLD
DLLLLLLLLLLD
DDDDDDDDDDDD
""",
    "web": """
....DDDDDD....
..DDLLDDLLDD..
.DLLLDLLDLLLD.
.DLLDLLLLDLLD.
DLLLDLLLLDLLLD
DDDDDDDDDDDDDD
DLLLDLLLLDLLLD
DLLLDLLLLDLLLD
DDDDDDDDDDDDDD
DLLLDLLLLDLLLD
.DLLDLLLLDLLD.
.DLLLDLLDLLLD.
..DDLLDDLLDD..
....DDDDDD....
""",
    "database": """
..DDDDDDDD..
DDWWWWWWWWDD
DWWWWWWWWWWD
DDWWWWWWWWDD
DLDDDDDDDDLD
DLLLLLLLLLLD
DLLLLLLLLLLD
DDLLLLLLLLDD
DLDDDDDDDDLD
DLLLLLLLLLLD
DLLLLLLLLLLD
DDLLLLLLLLDD
.DDDDDDDDDD.
""",
    "docker": """
....DDDDDD....
....DLWLLD....
....DLWLLD....
....DLLLLD....
....DDDDDD....
..............
DDDDDD..DDDDDD
DLWLLD..DLWLLD
DLWLLD..DLWLLD
DLLLLD..DLLLLD
DDDDDD..DDDDDD
""",
    "gpu": """
..D.D.D.D.D..
.DDDDDDDDDDD.
.DLLLLLLLLLD.
DDLWWWWWWWLDD
.DLWDDDDDWLD.
DDLWDLLLDWLDD
.DLWDLLLDWLD.
DDLWDDDDDWLDD
.DLWWWWWWWLD.
.DLLLLLLLLLD.
.DDDDDDDDDDD.
..D.D.D.D.D..
""",
    "app": """
......DD......
..DD.DLLD.DD..
..DLDLLLLDLD..
...DLLLLLLD...
DDDLLLDDLLLDDD
DLLLLDWWDLLLLD
DLLLDWWWWDLLLD
.DDLDWWWWDLDD.
DLLLDWWWWDLLLD
DLLLLDWWDLLLLD
DDDLLLDDLLLDDD
...DLLLLLLD...
..DLDLLLLDLD..
..DD.DLLD.DD..
......DD......
""",
    "storage": """
DDDDDDDDDDDD
DLLLLDDLLLLD
DLWWLDDLWWLD
DLLLLDDLLLLD
DLLLLDDLLLLD
DLLLLDDLLLLD
DLLLLDDLLLLD
DLLLLDDLLLLD
DLLLLDDLLLLD
DLWLLDDLWLLD
DLLLLDDLLLLD
DDDDDDDDDDDD
""",
    "security": """
DDDDDDDDDDDD
DLLLLLLLLLLD
DLLLLWWLLLLD
DLLLWWWWLLLD
DLLLWWWWLLLD
DLLLLWWLLLLD
DLLLLWWLLLLD
.DLLLLLLLLD.
.DLLLLLLLLD.
..DLLLLLLD..
...DLLLLD...
....DLLD....
.....DD.....
""",
    "mail": """
DDDDDDDDDDDDDD
DDLLLLLLLLLLDD
DLDLLLLLLLLDLD
DLLDLLLLLLDLLD
DLLLDLLLLDLLLD
DLLLLDDDDLLLLD
DLLLDLLLLDLLLD
DLLDLLLLLLDLLD
DLDLLLLLLLLDLD
DDDDDDDDDDDDDD
""",
    "monitoring": """
DDDDDDDDDDDDDD
DLLLLLLLLLLLLD
DLLLLLLLLWLLLD
DLLLLWLLLWLLLD
DLLLWLWLWLWLLD
DWWWWLLWLLLWWD
DLLLLLLWLLLLLD
DLLLLLLLLLLLLD
DDDDDDDDDDDDDD
.....DLLD.....
...DDDDDDDD...
""",
    "jumphost": """
..DDDD..........
.DLLLLD.........
DLLWWLLD........
DLW..WLDDDDDDDD.
DLW..WLLLLLLLLLD
DLLWWLLDDDDDLDLD
.DLLLLD.....D.D.
..DDDD..........
""",
}


def render(glyph, outline, fill):
    rows = [r for r in glyph.strip("\n").split("\n")]
    height, width = len(rows), max(len(r) for r in rows)
    image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    left, top = (16 - width) // 2, (16 - height) // 2
    colours = {"D": outline + (255,), "L": fill + (255,), "W": (255, 255, 255, 255)}
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c in colours:
                image.putpixel((left + x, top + y), colours[c])
    return image


def main():
    icons = {}
    for env, (outline, fill) in PALETTE.items():
        for role, glyph in GLYPHS.items():
            icons["%s-%s" % (env, role)] = render(glyph, outline, fill)
    for name, image in icons.items():
        image.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote %d icons to %s" % (len(icons), OUT))

    if "--preview" in sys.argv:
        scale, cell = 8, 16 * 8 + 24
        envs, roles = list(PALETTE), list(GLYPHS)
        sheet = Image.new("RGBA", (cell * len(roles), cell * len(envs) + 10), (255, 255, 255, 255))
        draw = ImageDraw.Draw(sheet)
        for row, env in enumerate(envs):
            for col, role in enumerate(roles):
                big = icons["%s-%s" % (env, role)].resize((16 * scale, 16 * scale), Image.NEAREST)
                sheet.paste(big, (col * cell + 12, row * cell + 4), big)
                draw.text((col * cell + 12, row * cell + 16 * scale + 6), "%s-%s" % (env, role), fill=(0, 0, 0, 255))
        path = os.path.join(HERE, "preview.png")
        sheet.save(path)
        print("preview: " + path)


if __name__ == "__main__":
    main()
