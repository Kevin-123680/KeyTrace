"""生成 KeyTrace 的多尺寸 .ico（无需外部资源）。

用法：
    python scripts/make_icon.py [输出路径]

设计：深色圆角方块 + 青蓝渐变的 K 字，底部一条高亮进度条，
保证 16px 下依然可以辨认。
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
SUPERSAMPLE = 8

BACKGROUND_TOP = (36, 43, 63, 255)
BACKGROUND_BOTTOM = (15, 17, 25, 255)
ACCENT = (124, 156, 255, 255)
ACCENT_DEEP = (96, 126, 235, 255)

FONT_CANDIDATES = (
    r"C:\Windows\Fonts\seguisb.ttf",
    r"C:\Windows\Fonts\segoeuib.ttf",
    r"C:\Windows\Fonts\arialbd.ttf",
    r"C:\Windows\Fonts\consolab.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
)


def load_font(size: int) -> ImageFont.FreeTypeFont:
    for candidate in FONT_CANDIDATES:
        if Path(candidate).exists():
            try:
                return ImageFont.truetype(candidate, size)
            except OSError:
                continue

    return ImageFont.load_default()


def vertical_gradient(size: int, top: tuple[int, int, int, int], bottom: tuple[int, int, int, int]) -> Image.Image:
    gradient = Image.new("RGBA", (1, size))
    for y in range(size):
        ratio = y / max(1, size - 1)
        gradient.putpixel(
            (0, y),
            tuple(round(top[i] + (bottom[i] - top[i]) * ratio) for i in range(4)),
        )
    return gradient.resize((size, size))


def render(size: int) -> Image.Image:
    canvas_size = size * SUPERSAMPLE
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))

    # 圆角背景
    radius = int(canvas_size * 0.24)
    background = vertical_gradient(canvas_size, BACKGROUND_TOP, BACKGROUND_BOTTOM)
    mask = Image.new("L", (canvas_size, canvas_size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, canvas_size - 1, canvas_size - 1), radius=radius, fill=255)
    canvas.paste(background, (0, 0), mask)

    draw = ImageDraw.Draw(canvas)

    # 底部高亮条
    bar_height = max(2, int(canvas_size * 0.055))
    bar_margin = int(canvas_size * 0.22)
    bar_top = canvas_size - bar_margin - bar_height
    draw.rounded_rectangle(
        (bar_margin, bar_top, canvas_size - bar_margin, bar_top + bar_height),
        radius=bar_height // 2,
        fill=ACCENT,
    )

    # 主体字母 K
    font = load_font(int(canvas_size * 0.58))
    glyph = "K"
    left, top, right, bottom = draw.textbbox((0, 0), glyph, font=font)
    text_width = right - left
    text_height = bottom - top
    text_x = (canvas_size - text_width) / 2 - left
    text_y = (canvas_size - text_height) / 2 - top - int(canvas_size * 0.055)

    # 先画一层轻微下沉的深色阴影，增加小尺寸下的对比度
    shadow = max(1, int(canvas_size * 0.008))
    draw.text((text_x + shadow, text_y + shadow), glyph, font=font, fill=ACCENT_DEEP)

    draw.text((text_x, text_y), glyph, font=font, fill=ACCENT)

    return canvas.resize((size, size), Image.LANCZOS)


def main() -> int:
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "src" / "KeyTrace.App" / "Assets" / "app.ico"
    target.parent.mkdir(parents=True, exist_ok=True)

    frames = [render(size) for size in SIZES]
    frames[-1].save(target, format="ICO", sizes=[(size, size) for size in SIZES], append_images=frames[:-1])
    print(f"wrote {target} ({target.stat().st_size} bytes)")

    preview = target.with_suffix(".png")
    frames[-1].save(preview)
    print(f"wrote {preview}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
