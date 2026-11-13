"""
Eureka Mine's pictures: icon.png (the mod manager's, 32x32 pixel art saved at 128x128) and sprites/eureka_icon.png (the run map's,
12x12 like the game's own map icons). Run: python art/make_icons.py
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.join(HERE, "..", "EurekaMine")

PAL = {
    ".": (0, 0, 0, 0),
    "k": (22, 20, 22, 255),     # coal black
    "d": (48, 44, 44, 255),     # breaker boards
    "g": (92, 88, 84, 255),     # rock
    "s": (130, 132, 140, 255),  # steel
    "w": (124, 82, 46, 255),    # timber
    "y": (244, 196, 92, 255),   # lamp light
    "o": (255, 236, 170, 255),  # lamp core
    "r": (150, 64, 46, 255),    # rust
    "b": (70, 84, 96, 255),     # sky
    "c": (106, 118, 128, 255),  # pale sky
}

MAP = [
    "....s..s....",
    "...sss.ss...",
    "...s.ssss...",
    "..s..s..s...",
    "..s.s....s..",
    ".s..s....s..",
    ".s.wwwwww.s.",
    "s..w.kk.w..s",
    "s..wkyyk.w.s",
    "..w.kyok.w..",
    "kkwkkkkkkwkk",
    "kkkkkkkkkkkk",
]

ICON = [
    "cccccccccccccccccccccccccccccccc",
    "cccccccccccccccccccsccccsccccccc",
    "ccccccccccccccccccsssccsssccccbc",
    "cccccccccccccccccsscsssscsscbbbb",
    "ccccccccccccccccsscccsscccsscbbb",
    "cccccccccccccccsscccsssscccssbbb",
    "ccccccdddcccccsscccsscssccccssbb",
    "cccccddddddcccsccccsscsscccccsbb",
    "cccccddddddddsdccccsscsscccccsbb",
    "ccccddddddddsdddccsscccssccccsbb",
    "ccccddddddddsdddddsscccsscccsbbb",
    "cccdddydddydsdddddsccccssccssbbb",
    "cccddddddddsddydddsgggggggggggbb",
    "ccddydddyddsdddddsggggggggggggggg"[:32],
    "ccdddddddddsddddddgggggggggggggg",
    "cdddddyddddsdddyddgggwwwwwwwgggg",
    "cddydddddddsddddddggwwwwwwwwwggg",
    "cdddddddydsddddyddggwwkkkkkwwggg",
    "kddddddddsdddddddggwwkkyyykkwwgg",
    "kkddyddddsddyddddggwwkkyoookwwgg",
    "kkkdddddsddddddddggwwkkyoookwwgg",
    "kkkkdddsdddddddddggwwkkkyyykkwgg",
    "kkkkkksdddddddddgggwwkkkkkkkkwgg",
    "kkkkkkkkkkkkkkkggggwwkkkkkkkkwgg",
    "kkkkkkkkkkkkkkgggggwwkkkkkkkkwgg",
    "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
    "kkkkrrrrrrkkkkkkkkkkkkkkkkkkkkkk",
    "kkkrrrrrrrrkkkkkkkkkkkkkkkkkkkkk",
    "kkkrrrrrrrrkkkkkkkkkkkkkkkkkkkkk",
    "kkkkskkkkskkkkkkkkkkkkkkkkkkkkkk",
    "ssssssssssssssssssssssssssssssss",
    "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk",
]


def draw(rows, scale):
    h, w = len(rows), max(len(r) for r in rows)
    img = Image.new("RGBA", (w, h))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row.ljust(w, row[-1])):
            img.putpixel((x, y), PAL[ch])
    return img.resize((w * scale, h * scale), Image.NEAREST)


if __name__ == "__main__":
    os.makedirs(os.path.join(MOD, "sprites"), exist_ok=True)
    draw(MAP, 1).save(os.path.join(MOD, "sprites", "eureka_icon.png"))
    draw(ICON, 4).save(os.path.join(MOD, "icon.png"))
    print("icons written")
