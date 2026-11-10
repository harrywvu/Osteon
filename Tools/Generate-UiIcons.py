"""Regenerate the small white UI icon set used by world-space panels."""
from pathlib import Path
from uuid import uuid4
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / "Assets/Resources/OsteonUI"
ROOT.mkdir(parents=True, exist_ok=True)
S = 96
META = (ROOT.parents[1] / "VRTemplateAssets/Sprites/Icons/Checkmark.png.meta").read_text()


def icon(name, draw):
    image = Image.new("RGBA", (S, S))
    pen = ImageDraw.Draw(image)
    draw(pen)
    image.save(ROOT / f"{name}.png")
    meta = ROOT / f"{name}.png.meta"
    if not meta.exists():
        body = META.replace("guid: 4aafc51823472ec4ea8ab57045fb659a", f"guid: {uuid4().hex}")
        if name == "rounded":
            body = body.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                                "spriteBorder: {x: 24, y: 24, z: 24, w: 24}")
        meta.write_text(body)


W = "white"
icon("back", lambda d: (d.line((70, 48, 25, 48), fill=W, width=9),
                        d.line((43, 25, 20, 48, 43, 71), fill=W, width=9, joint="curve")))
icon("next", lambda d: (d.line((26, 48, 71, 48), fill=W, width=9),
                        d.line((53, 25, 76, 48, 53, 71), fill=W, width=9, joint="curve")))
icon("close", lambda d: (d.line((27, 27, 69, 69), fill=W, width=9),
                         d.line((69, 27, 27, 69), fill=W, width=9)))
icon("check", lambda d: d.line((19, 49, 39, 68, 77, 26), fill=W, width=10, joint="curve"))
icon("quiz", lambda d: (d.rounded_rectangle((20, 16, 76, 80), radius=8, outline=W, width=7),
                        d.line((33, 36, 42, 45, 61, 26), fill=W, width=6),
                        d.line((34, 61, 63, 61), fill=W, width=6)))
icon("account", lambda d: (d.ellipse((34, 16, 62, 44), outline=W, width=7),
                           d.arc((18, 42, 78, 88), 180, 360, fill=W, width=8)))
icon("settings", lambda d: (d.ellipse((20, 20, 76, 76), outline=W, width=8),
                            d.ellipse((39, 39, 57, 57), outline=W, width=7),
                            *[d.line((48, 5, 48, 19), fill=W, width=8),
                              d.line((48, 77, 48, 91), fill=W, width=8),
                              d.line((5, 48, 19, 48), fill=W, width=8),
                              d.line((77, 48, 91, 48), fill=W, width=8)]))
icon("guide", lambda d: (d.line((16, 22, 16, 75, 47, 82, 47, 29, 16, 22), fill=W, width=6, joint="curve"),
                         d.line((47, 29, 78, 22, 78, 75, 47, 82), fill=W, width=6, joint="curve")))
icon("audio", lambda d: (d.polygon([(15, 38), (32, 38), (49, 23), (49, 73), (32, 58), (15, 58)], outline=W),
                         d.line((15, 38, 32, 38, 49, 23, 49, 73, 32, 58, 15, 58, 15, 38), fill=W, width=6),
                         d.arc((39, 24, 79, 72), 300, 60, fill=W, width=7),
                         d.arc((37, 12, 94, 84), 300, 60, fill=W, width=6)))
icon("move", lambda d: (d.line((48, 12, 48, 84), fill=W, width=7),
                        d.line((12, 48, 84, 48), fill=W, width=7),
                        d.polygon([(48, 5), (35, 23), (61, 23)], fill=W),
                        d.polygon([(48, 91), (35, 73), (61, 73)], fill=W),
                        d.polygon([(5, 48), (23, 35), (23, 61)], fill=W),
                        d.polygon([(91, 48), (73, 35), (73, 61)], fill=W)))
icon("color", lambda d: (d.ellipse((13, 13, 83, 83), outline=W, width=7),
                         *[d.ellipse(box, fill=W) for box in [(29, 29, 40, 40), (52, 24, 63, 35),
                                                              (60, 44, 71, 55), (39, 59, 50, 70)]]))
icon("theme", lambda d: (d.ellipse((28, 28, 68, 68), outline=W, width=7),
                         *[d.line(line, fill=W, width=6) for line in [(48, 6, 48, 18), (48, 78, 48, 90),
                                                                       (6, 48, 18, 48), (78, 48, 90, 48)]]))
icon("text", lambda d: (d.line((20, 77, 46, 19, 72, 77), fill=W, width=8, joint="curve"),
                        d.line((30, 55, 62, 55), fill=W, width=7)))
icon("reset", lambda d: (d.arc((18, 18, 78, 78), 60, 345, fill=W, width=8),
                         d.polygon([(18, 14), (18, 40), (42, 29)], fill=W)))
icon("bone", lambda d: (d.line((27, 69, 69, 27), fill=W, width=13),
                        *[d.ellipse(box, outline=W, width=6) for box in
                          [(12, 59, 34, 81), (59, 12, 81, 34)]]))

# Nine-slice white shape; Unity tint supplies each palette's surface color.
rounded = Image.new("RGBA", (S, S))
ImageDraw.Draw(rounded).rounded_rectangle((1, 1, 94, 94), radius=22, fill=W)
rounded.save(ROOT / "rounded.png")
meta = ROOT / "rounded.png.meta"
if not meta.exists():
    meta.write_text(META.replace("guid: 4aafc51823472ec4ea8ab57045fb659a", f"guid: {uuid4().hex}")
                    .replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                             "spriteBorder: {x: 24, y: 24, z: 24, w: 24}"))
