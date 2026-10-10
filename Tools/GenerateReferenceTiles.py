#!/usr/bin/env python3
"""
NO UNIT 404 - surface tiles drawn from the map reference sheets.

The look is taken by eye from map_refer/floor_ref_zip_input - the "주요 재질 / 표면 참고"
strip on each sheet: the two-tone corridor paint (cream over teal, flaking), mould and
water marks, cracked floor tiles that are always a little wet, stained ceiling board,
rusted steel, hazard paint on the car park pillars, and the plywood and plastic sheet
that board up the fourth floor. Nothing is copied out of the sheets: every surface is
drawn from noise on a torus, so it repeats without a seam in both directions.

For each surface it writes, to map_refer/tile/:
    <name>_albedo.png     colour, sRGB (RGBA for the plastic sheet: alpha = opacity)
    <name>_normal.png     tangent-space normal, OpenGL convention (+Y up), as Unity reads it
    <name>_roughness.png  linear roughness, white = matte
and tile_preview.png, each albedo repeated 2x2 so a seam would show.

With --game it also writes what the game loads, to Assets/_Project/Resources/NO404/Surfaces/:
    <name>_albedo.png     RGB colour, A = smoothness (URP Lit reads smoothness from albedo alpha)
    <name>_normal.png     imported as a normal map by Editor/SurfaceTextureImporter.cs
SurfaceArt.cs maps its surfaces onto these names and falls back to drawing its own when a
file is missing.

Usage:  python Tools/GenerateReferenceTiles.py [--size 1024] [--only a,b] [--game]
"""

import argparse
import os
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from scipy.spatial import cKDTree

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "map_refer", "tile")
GAME_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "NO404", "Surfaces")
FONT = os.path.join(ROOT, "Assets", "_Project", "Resources", "NO404", "Fonts", "Pretendard-Regular.otf")


# =====================================================================================
# noise on a torus
# =====================================================================================

def grid(n):
    """(v, u) in [0, 1): v runs down the image, u across it."""
    v, u = np.mgrid[0:n, 0:n].astype(np.float64)
    return v / n, u / n


def normalize(a):
    lo, hi = float(a.min()), float(a.max())
    return (a - lo) / (hi - lo) if hi > lo else np.zeros_like(a)


def vnoise(n, cy, cx=None, seed=0, order=3):
    """Value noise with cy x cx cells, wrapped, smooth (cubic spline)."""
    cx = cy if cx is None else cx
    rng = np.random.default_rng(seed)
    base = rng.random((int(cy), int(cx)))
    z = ndimage.zoom(base, (n / cy, n / cx), order=order, mode="grid-wrap", grid_mode=True)
    return z[:n, :n]


def fbm(n, cells, octaves=5, seed=0, gain=0.5, aniso=(1.0, 1.0)):
    """Fractal value noise. aniso scales the cell count on (v, u): (0.25, 4) is long and thin."""
    total = np.zeros((n, n))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        c = cells * (2 ** o)
        cy = max(1, int(round(c * aniso[0])))
        cx = max(1, int(round(c * aniso[1])))
        if cy > n // 3 or cx > n // 3:
            break
        total += amp * vnoise(n, cy, cx, seed + 101 * o)
        norm += amp
        amp *= gain
    return normalize(total)


def worley(n, count, seed):
    """Distances to the nearest and second-nearest of `count` points, in cell units."""
    rng = np.random.default_rng(seed)
    pts = rng.random((count, 2))
    tree = cKDTree(pts, boxsize=1.0)
    v, u = grid(n)
    q = np.column_stack([v.ravel(), u.ravel()]) % 1.0
    d, i = tree.query(q, k=2)
    s = np.sqrt(count)
    return d[:, 0].reshape(n, n) * s, d[:, 1].reshape(n, n) * s, i[:, 0].reshape(n, n)


def warp(a, du, dv):
    """Samples a at (u + du, v + dv), wrapping. du and dv are in uv units."""
    n = a.shape[0]
    rows, cols = np.mgrid[0:n, 0:n].astype(np.float64)
    coords = [rows + dv * n, cols + du * n]
    if a.ndim == 2:
        return ndimage.map_coordinates(a, coords, order=1, mode="grid-wrap")
    return np.stack([ndimage.map_coordinates(a[..., c], coords, order=1, mode="grid-wrap")
                     for c in range(a.shape[2])], axis=-1)


def blur(a, sigma):
    if a.ndim == 3:
        return np.stack([ndimage.gaussian_filter(a[..., c], sigma, mode="wrap") for c in range(a.shape[2])], -1)
    return ndimage.gaussian_filter(a, sigma, mode="wrap")


def blur_v(a, sigma):
    """Blur along v only (down the wall): what water does."""
    return ndimage.gaussian_filter1d(a, sigma, axis=0, mode="wrap")


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def C(r, g, b):
    return np.array([r, g, b], dtype=np.float64)


def mix(a, b, t):
    """Blend colour fields/constants a -> b by the scalar field t."""
    t = np.asarray(t)[..., None]
    return a + (b - a) * t


def fill(n, colour):
    return np.broadcast_to(colour, (n, n, 3)).astype(np.float64).copy()


def shade(c, k):
    return c * np.asarray(k)[..., None]


def lines_mask(n, segments, width, seed_jitter=None):
    """Thin straight scratches, drawn wrapped. segments: list of (u0, v0, u1, v1) in uv."""
    img = Image.new("L", (n * 3, n * 3), 0)
    d = ImageDraw.Draw(img)
    for (u0, v0, u1, v1, w) in segments:
        for ox in (0, 1, 2):
            for oy in (0, 1, 2):
                d.line([((u0 + ox) * n, (v0 + oy) * n), ((u1 + ox) * n, (v1 + oy) * n)],
                       fill=255, width=max(1, int(round(w * width))))
    a = np.asarray(img, dtype=np.float64) / 255.0
    tiled = a[n:2 * n, n:2 * n]
    # Wrap the parts of strokes that fell into the outer tiles back onto the middle one.
    for ox in (0, 1, 2):
        for oy in (0, 1, 2):
            tiled = np.maximum(tiled, a[oy * n:(oy + 1) * n, ox * n:(ox + 1) * n])
    return tiled


def random_scratches(n, rng, count, length, angle=None, spread=0.4, width=1.5):
    segs = []
    for _ in range(count):
        u, v = rng.random(), rng.random()
        a = (angle if angle is not None else rng.random() * np.pi) + (rng.random() - 0.5) * spread
        l = length * (0.4 + rng.random())
        segs.append((u, v, u + np.cos(a) * l, v + np.sin(a) * l, 0.6 + rng.random()))
    return lines_mask(n, segs, width)


def crack_lines(n, count, seed, width_px, jag=0.012):
    """Edges of a jittered Voronoi diagram, roughened: the shape tiles and plaster crack in."""
    f1, f2, _ = worley(n, count, seed)
    edge = f2 - f1
    edge = warp(edge, (fbm(n, 24, 3, seed + 1) - 0.5) * jag, (fbm(n, 24, 3, seed + 2) - 0.5) * jag)
    px = 2.0 * np.sqrt(count) / n           # edge units per pixel, roughly
    return ss(width_px * px, 0.0, edge)


def cavity(h, radius=4.0, k=3.0, floor=0.55):
    """Darkens what sits below its surroundings: grout, cracks, flaking edges."""
    return np.clip(1.0 - (blur(h, radius) - h) * k, floor, 1.0)


# =====================================================================================
# outputs
# =====================================================================================

# File stem -> (label on the preview, metres one repeat covers as (u, v)). The metres are
# what SurfaceArt.cs tiles each one at; change both together.
META = {
    "wall_two_tone": ("2톤 벽체 (아이보리 / 청록 걸레받이)", (2.6, 2.6)),
    "wall_peeling_paint": ("벗겨진 페인트 (벽체)", (1.0, 1.0)),
    "wall_mould_stain": ("물때 / 곰팡이 (벽, 천장)", (2.0, 2.0)),
    "floor_tile_cracked": ("균열 타일 바닥 (복도, 로비) · 젖은 바닥", (2.4, 2.4)),
    "ceiling_panel_stained": ("천장 마감재 (처짐, 얼룩)", (2.4, 2.4)),
    "concrete_stained": ("오염된 콘크리트 (기둥, 벽)", (2.4, 2.4)),
    "concrete_floor_wet": ("젖은 콘크리트 바닥 (주차장, 물웅덩이)", (3.0, 3.0)),
    "metal_rusted": ("녹슨 금속 (E/V, 우편함, 배관)", (1.0, 1.0)),
    "door_steel_painted": ("세대 현관문 (도장 철판, 긁힘)", (1.0, 1.0)),
    "hazard_stripe": ("기둥 경고 도색 (페인트 박리)", (1.0, 1.0)),
    "bathroom_tile": ("화장실 벽 타일 (습기, 곰팡이)", (0.6, 0.6)),
    "plywood_old": ("낡은 합판 / 판자 (임시 막음)", (1.2, 1.2)),
    "plastic_sheet": ("비닐 시트 (임시 막음, 반투명)", (2.0, 2.0)),
}


class Surface:
    def __init__(self, name, albedo, height, rough, strength, alpha=None):
        self.name = name              # file stem
        self.title, self.metres = META[name]
        self.albedo = np.clip(albedo, 0.0, 1.0)
        self.height = height
        self.rough = np.clip(rough, 0.02, 1.0)
        self.strength = strength      # normal-map strength
        self.alpha = alpha            # opacity, plastic sheet only

    def normal(self):
        h = self.height * self.strength
        dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
        dy_down = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
        # OpenGL: green points up the texture. Up is -row, so -dh/dv_up = +dh/drow.
        nx, ny, nz = -dx, dy_down, np.ones_like(h)
        l = np.sqrt(nx * nx + ny * ny + nz * nz)
        return np.stack([nx / l, ny / l, nz / l], -1) * 0.5 + 0.5


def to8(a):
    return (np.clip(a, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def write(surface, size, game):
    os.makedirs(OUT_DIR, exist_ok=True)
    albedo = to8(surface.albedo)
    normal = to8(surface.normal())
    rough = to8(surface.rough)

    if surface.alpha is not None:
        rgba = np.dstack([albedo, to8(surface.alpha)])
        Image.fromarray(rgba, "RGBA").save(os.path.join(OUT_DIR, surface.name + "_albedo.png"), optimize=True)
    else:
        Image.fromarray(albedo, "RGB").save(os.path.join(OUT_DIR, surface.name + "_albedo.png"), optimize=True)
    Image.fromarray(normal, "RGB").save(os.path.join(OUT_DIR, surface.name + "_normal.png"), optimize=True)
    Image.fromarray(rough, "L").save(os.path.join(OUT_DIR, surface.name + "_roughness.png"), optimize=True)

    if game:
        os.makedirs(GAME_DIR, exist_ok=True)
        smooth = to8(1.0 - surface.rough)
        Image.fromarray(np.dstack([albedo, smooth]), "RGBA").save(
            os.path.join(GAME_DIR, surface.name + "_albedo.png"), optimize=True)
        Image.fromarray(normal, "RGB").save(os.path.join(GAME_DIR, surface.name + "_normal.png"), optimize=True)


def preview(names):
    """Every albedo at 2x2, labelled, so a seam or an obvious repeat is visible at a glance."""
    cell, pad, label = 360, 16, 54
    cols = 4
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (cell + pad) + pad, rows * (cell + pad + label) + pad), (18, 22, 26))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype(FONT, 20)
        small = ImageFont.truetype(FONT, 15)
    except OSError:
        font = small = ImageFont.load_default()
    for k, (name, title, metres) in enumerate(names):
        img = Image.open(os.path.join(OUT_DIR, name + "_albedo.png")).convert("RGB")
        half = img.resize((cell // 2, cell // 2), Image.LANCZOS)
        tile = Image.new("RGB", (cell, cell))
        for ox in (0, 1):
            for oy in (0, 1):
                tile.paste(half, (ox * cell // 2, oy * cell // 2))
        x = pad + (k % cols) * (cell + pad)
        y = pad + (k // cols) * (cell + pad + label)
        sheet.paste(tile, (x, y))
        draw.text((x, y + cell + 6), title, fill=(220, 226, 228), font=font)
        draw.text((x, y + cell + 30), "%s  ·  %.1f x %.1f m / tile, 2x2 shown" % (name, metres[0], metres[1]),
                  fill=(130, 144, 150), font=small)
    sheet.save(os.path.join(OUT_DIR, "tile_preview.png"), optimize=True)


# =====================================================================================
# shared pieces
# =====================================================================================

def grit(n, seed, amount=0.12):
    """Pixel-scale unevenness: what every real surface has and a drawn one does not."""
    a = vnoise(n, n // 4, seed=seed, order=1)
    b = vnoise(n, n // 2, seed=seed + 1, order=1)
    c = vnoise(n, n // 12, seed=seed + 2)
    g = a * 0.4 + b * 0.3 + c * 0.3 - 0.5
    return 1.0 + g * 2.0 * amount


def mottle(n, seed, amount=0.18, cells=10):
    """Mid-scale variation: no painted wall is one colour from a metre away."""
    return 1.0 + (fbm(n, cells, 6, seed, gain=0.6) - 0.5) * 2.0 * amount


def flecks(n, seed, density=0.03, cells=None):
    """Small marks of several sizes and strengths: dirt, splashes, fly-specks."""
    cells = cells or n // 3
    a = ss(1.0 - density * 0.5, 1.0 - density * 0.1, vnoise(n, cells, seed=seed))
    b = ss(1.0 - density * 0.3, 1.0 - density * 0.05, vnoise(n, cells // 3, seed=seed + 1))
    strength = 0.25 + 0.75 * vnoise(n, 24, seed=seed + 2)
    return blur(np.clip(a + b, 0.0, 1.0), 0.5) * strength


def grime(n, seed, cells=4, lo=0.45, hi=0.9):
    """Soft dirt with a ragged inside: a low cloud broken up by finer noise."""
    m = ss(lo, hi, fbm(n, cells, 7, seed, gain=0.62))
    return m * (0.55 + 0.45 * fbm(n, cells * 10, 3, seed + 1))


def runs(n, seed, cells=3, top=0.86):
    """Water runs: thin, uneven, many times longer than wide."""
    long_ = fbm(n, cells, 6, seed, aniso=(0.12, 9.0), gain=0.62)
    long_ = warp(long_, (fbm(n, 5, 3, seed + 1) - 0.5) * 0.012, 0)
    return ss(top - 0.22, top, long_) * (0.6 + 0.4 * fbm(n, cells * 12, 3, seed + 2, aniso=(0.3, 3)))


def desaturate(c, k):
    grey = c.mean(axis=-1, keepdims=True)
    return c + (grey - c) * k


def peel(n, seed, coverage, cells=6, bias=None, jag=0.035, chips=1.0):
    """
    Where paint has come away. Paint does not dissolve in clouds: it cracks into a crackle
    of plates and the plates drop out. So the islands are unions of Voronoi cells - big
    plates and small ones - chosen where a low-frequency field (plus bias) is high, which
    gives the angular edges and the crumbs along them that the reference walls have.
    """
    field = fbm(n, cells, 7, seed, gain=0.6)
    if bias is not None:
        field = field + bias
    thr = np.quantile(field, 1.0 - coverage)
    rng = np.random.default_rng(seed + 5)
    v, u = grid(n)
    hard = np.zeros((n, n))
    for count, spread, k in ((int(cells * cells * 6), 0.10, 0), (int(cells * cells * 40), 0.06, 1)):
        pts = rng.random((count, 2))
        tree = cKDTree(pts, boxsize=1.0)
        du = (fbm(n, cells * 8, 3, seed + 10 + k) - 0.5) * jag * 0.3
        dv = (fbm(n, cells * 8, 3, seed + 20 + k) - 0.5) * jag * 0.3
        q = np.column_stack([(v + dv).ravel(), (u + du).ravel()]) % 1.0
        _, idx = tree.query(q, k=1)
        idx = idx.reshape(n, n)
        site = field[(pts[:, 0] * n).astype(int) % n, (pts[:, 1] * n).astype(int) % n]
        roll = rng.random(count)
        # A plate falls where the field is over the threshold; near it, only some do.
        chosen = roll < np.clip((site - thr) / spread + 0.5, 0.0, 1.0) ** (1.0 / max(0.2, chips))
        hard = np.maximum(hard, chosen[idx].astype(np.float64))
    soft = blur(hard, 0.6)
    lip = np.clip(blur(hard, 1.5) - hard, 0.0, 1.0)          # the paint edge lifting, outside
    shadow = np.clip(hard - blur(hard, 2.4), 0.0, 1.0)       # its shadow on what is underneath
    return soft, lip, shadow


def plaster_under(n, seed, tone=1.0):
    """Under the paint: grey-white plaster, older pale paint in places, dirt in the pores."""
    old = fbm(n, 5, 5, seed)
    c = mix(fill(n, C(0.53, 0.52, 0.48)), fill(n, C(0.47, 0.54, 0.55)), ss(0.6, 0.75, old) * 0.7)
    c = shade(c, tone * mottle(n, seed + 1, 0.16, 14) * grit(n, seed + 2, 0.16))
    c = mix(c, C(0.30, 0.28, 0.24), grime(n, seed + 3, 6) * 0.45)
    c = mix(c, C(0.36, 0.34, 0.29), grime(n, seed + 4, 2, 0.4, 0.9) * 0.3)
    return c


def crack(n, count, seed, width_px, region, jag=0.014):
    """Cracks where region allows, with uneven width and darkness along their length."""
    lines = crack_lines(n, count, seed, width_px, jag)
    vary = 0.45 + 0.55 * fbm(n, 30, 3, seed + 9)
    return lines * region * vary


# =====================================================================================
# surfaces
# =====================================================================================

def wall_two_tone(n):
    """
    The corridor, lobby and stair wall on every sheet: cream paint above a teal dado at
    1.1m, a dark line between them, soot coming down from the ceiling, water marks, and
    the dado flaking worst near the floor where the damp comes up. 2.6m square, so one
    repeat is exactly one storey: v = 0 is the floor.
    """
    s = 100
    v, u = grid(n)
    h_m = (1.0 - v) * 2.6
    band = 1.10 + (fbm(n, 5, 3, s + 1) - 0.5) * 0.010
    below = ss(band + 0.002, band - 0.002, h_m)
    line = ss(0.010, 0.005, np.abs(h_m - band - 0.008))

    cream = C(0.63, 0.60, 0.47)
    teal = C(0.16, 0.29, 0.33)

    # ---- upper paint ------------------------------------------------------------
    up = shade(fill(n, cream), mottle(n, s + 2, 0.13, 6) * grit(n, s + 3, 0.07))
    up = mix(up, C(0.48, 0.43, 0.30), ss(1.6, 2.6, h_m) * 0.45)                       # soot, nicotine
    fall = ss(0.2, 2.5, h_m + (fbm(n, 4, 3, s + 4) - 0.5) * 1.4)
    r = runs(n, s + 5, 3, 0.84) * fall
    up = mix(up, C(0.36, 0.33, 0.25), r * 0.6)
    tide = fbm(n, 2, 6, s + 6, gain=0.58)
    stain = ss(0.60, 0.72, tide)
    ring = ss(0.016, 0.0, np.abs(tide - 0.66)) * (0.5 + 0.5 * fbm(n, 40, 3, s + 7))
    up = mix(up, C(0.50, 0.43, 0.29), stain * 0.30 + ring * 0.5)
    g = grime(n, s + 8, 5)
    up = mix(up, C(0.38, 0.36, 0.30), g * 0.35)
    mould = flecks(n, s + 9, 0.05) * ss(1.9, 2.6, h_m) * ss(0.45, 0.7, fbm(n, 6, 4, s + 10))
    up = mix(up, C(0.13, 0.15, 0.11), mould * 0.8)

    # ---- the dado -----------------------------------------------------------------
    lo = shade(fill(n, teal), mottle(n, s + 11, 0.2, 8) * grit(n, s + 12, 0.1))
    lo = mix(lo, C(0.09, 0.11, 0.10), ss(0.5, 0.0, h_m) * 0.6)                         # kicked-up dirt
    scuff = ss(0.78, 0.95, fbm(n, 3, 5, s + 13, aniso=(14.0, 1.0))) * ss(0.1, 0.9, h_m)
    lo = mix(lo, C(0.30, 0.36, 0.36), scuff * 0.4)
    damp = ss(0.6, 0.0, h_m + (fbm(n, 6, 4, s + 14) - 0.5) * 0.5)
    salt = damp * ss(0.55, 0.8, fbm(n, 24, 3, s + 15))
    lo = mix(lo, C(0.55, 0.57, 0.53), salt * 0.45)                                     # efflorescence
    lo = mix(lo, C(0.10, 0.13, 0.12), grime(n, s + 16, 6) * 0.35)
    lo = mix(lo, C(0.08, 0.10, 0.10), grime(n, s + 24, 2, 0.5, 0.95) * 0.4)
    lo = mix(lo, C(0.11, 0.15, 0.15), runs(n, s + 25, 4, 0.86) * 0.35)

    c = mix(up, lo, below)
    c = mix(c, C(0.09, 0.15, 0.17), line * 0.9)

    # ---- flaking ------------------------------------------------------------------
    bias = (below * (0.10 + 0.14 * ss(0.7, 0.0, h_m)) + 0.08 * ss(0.05, 0.0, np.abs(h_m - band))
            - 0.07 * (1.0 - below))
    flake, lip, shadow = peel(n, s + 17, 0.14, cells=9, bias=bias)
    under = plaster_under(n, s + 18)
    c = mix(c, under, flake)
    c = shade(c, 1.0 - shadow * 0.5)
    c = mix(c, C(0.70, 0.70, 0.64), lip * 0.2)

    cr = crack(n, 30, s + 19, 1.0, ss(0.6, 0.76, fbm(n, 3, 4, s + 20)))
    c = shade(c, 1.0 - cr * 0.6)
    c = shade(c, 1.0 - flecks(n, s + 21, 0.012) * 0.4)
    c = desaturate(c, 0.08)

    height = (0.26 * (1.0 - flake) + 0.10 * fbm(n, 60, 3, s + 22) + 0.05 * (grit(n, s + 3, 1.0) - 1.0)
              + lip * 0.14 - cr * 0.35 - line * 0.02)
    c = shade(c, cavity(height, 3.0, 2.0, 0.7))

    rough = (0.80 - 0.2 * below - 0.12 * r + 0.12 * flake + 0.06 * fbm(n, 30, 3, s + 23)
             - line * 0.2 - damp * 0.06 + g * 0.05)
    return Surface("wall_two_tone", c, height, rough, 9.0)


def wall_peeling_paint(n):
    """The swatch on every sheet: blue-green paint coming off grey plaster, edges crumbling."""
    s = 200
    paint = shade(fill(n, C(0.19, 0.40, 0.46)), mottle(n, s + 1, 0.2, 6) * grit(n, s + 2, 0.1))
    paint = mix(paint, C(0.12, 0.22, 0.24), runs(n, s + 3, 3, 0.86) * 0.45)
    paint = mix(paint, C(0.12, 0.17, 0.17), grime(n, s + 4, 4) * 0.35)
    flake, lip, shadow = peel(n, s + 5, 0.36, cells=6, jag=0.05, chips=1.4)
    under = plaster_under(n, s + 6, 0.9)
    c = mix(paint, under, flake)
    c = shade(c, 1.0 - shadow * 0.55)
    c = mix(c, C(0.66, 0.74, 0.74), lip * 0.3)
    cr = crack(n, 22, s + 7, 1.0, ss(0.55, 0.7, fbm(n, 3, 4, s + 8)))
    c = shade(c, (1.0 - cr * 0.55) * (1.0 - flecks(n, s + 9, 0.015) * 0.4))
    height = 0.30 * (1.0 - flake) + 0.12 * fbm(n, 50, 3, s + 10) + 0.05 * (grit(n, s + 2, 1.0) - 1.0) + lip * 0.18 - cr * 0.3
    c = shade(c, cavity(height, 3.0, 2.2, 0.65))
    rough = 0.62 + 0.28 * flake + 0.06 * fbm(n, 30, 3, s + 11)
    return Surface("wall_peeling_paint", c, height, rough, 10.0)


def wall_mould_stain(n):
    """Plaster under a leak: tea-coloured tide lines, drip runs, green-black mould blooms."""
    s = 300
    c = shade(fill(n, C(0.50, 0.49, 0.41)), mottle(n, s + 1, 0.16, 5) * grit(n, s + 2, 0.12))
    tide = fbm(n, 2, 6, s + 3, gain=0.6)
    for lo_, col in ((0.45, C(0.44, 0.41, 0.31)), (0.57, C(0.38, 0.34, 0.24)), (0.67, C(0.31, 0.27, 0.19))):
        c = mix(c, col, ss(lo_, lo_ + 0.07, tide) * 0.55)
        c = mix(c, C(0.27, 0.22, 0.15), ss(0.012, 0.0, np.abs(tide - lo_ - 0.035)) * 0.45)
    r = runs(n, s + 4, 4, 0.84)
    c = mix(c, C(0.27, 0.25, 0.18), r * 0.55)
    bloom = fbm(n, 7, 6, s + 5, gain=0.6)
    mould = ss(0.52, 0.72, bloom) * (0.35 + 0.65 * flecks(n, s + 6, 0.2))
    c = mix(c, C(0.11, 0.13, 0.09), mould * 0.85)
    c = mix(c, C(0.27, 0.31, 0.18), ss(0.45, 0.58, bloom) * (1 - mould) * 0.35)
    c = mix(c, C(0.20, 0.19, 0.15), grime(n, s + 7, 4) * 0.3)
    height = 0.15 * fbm(n, 50, 3, s + 8) + 0.06 * (grit(n, s + 2, 1.0) - 1.0) + mould * 0.06 - r * 0.02
    c = shade(c, cavity(height, 2.5, 2.0, 0.75))
    rough = 0.88 - r * 0.25 - ss(0.6, 0.7, tide) * 0.1 + mould * 0.05
    return Surface("wall_mould_stain", c, height, rough, 6.0)


def floor_tile_cracked(n):
    """
    The corridor and lobby floor: 400mm grey stone-effect tiles, dark grout, two of the
    thirty-six shattered, corners chipped to the biscuit, grime in the joints, and a film of
    water that turns the floor into a mirror where it pools. 2.4m square = 6 x 6 tiles, so
    the broken ones do not line up down a twenty-metre corridor.
    """
    s = 400
    v, u = grid(n)
    k = 6
    fu, fv = (u * k) % 1.0, (v * k) % 1.0
    ti, tj = np.floor(u * k).astype(int), np.floor(v * k).astype(int)
    de = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))       # tile-local units (0.4m)
    g = 0.0065
    grout = ss(g + 0.004, g, de)

    rng = np.random.default_rng(s)
    tone = rng.normal(0.0, 0.06, (k, k))
    c = fill(n, C(0.40, 0.42, 0.43)) * (1.0 + tone[tj, ti])[..., None]
    stone = fbm(n, 14, 6, s + 1, gain=0.62)
    c = shade(c, (0.86 + 0.28 * stone) * grit(n, s + 2, 0.08))
    c = mix(c, C(0.28, 0.29, 0.29), ss(0.6, 0.8, fbm(n, 40, 3, s + 3)) * 0.35)        # darker inclusions
    c = mix(c, C(0.56, 0.57, 0.56), ss(0.93, 0.99, vnoise(n, 300, seed=s + 4, order=1)) * 0.5)

    # Two tiles shattered into plates, the rest with the odd hairline.
    shattered = np.isin(tj * k + ti, [4, 15, 26]).astype(np.float64)
    plates = crack_lines(n, 420, s + 5, 1.4, 0.006) * shattered
    hair = crack(n, 40, s + 6, 0.9, ss(0.6, 0.72, fbm(n, 5, 4, s + 7)))
    cr = np.clip(plates + hair, 0, 1) * (1 - grout)
    broken_edge = np.clip(blur(plates, 1.6) * 2.2 - plates, 0, 1) * shattered * (1 - grout)
    c = mix(c, C(0.55, 0.55, 0.52), broken_edge * 0.45)                            # crushed edges go pale
    c = shade(c, 1.0 - cr * 0.75)
    chip = ss(0.05, 0.02, de) * ss(0.62, 0.7, fbm(n, 30, 3, s + 8)) * (1 - grout)
    c = mix(c, C(0.50, 0.48, 0.43), chip * 0.8)

    # Joints and dirt.
    c = mix(c, shade(fill(n, C(0.12, 0.115, 0.105)), grit(n, s + 9, 0.2)), grout)
    c = shade(c, 1.0 - ss(0.08, 0.0, de) * 0.2)
    gr = grime(n, s + 10, 4)
    c = mix(c, C(0.22, 0.21, 0.18), gr * 0.45)
    c = mix(c, C(0.30, 0.26, 0.19), ss(0.66, 0.76, fbm(n, 3, 6, s + 11)) * 0.3)
    c = shade(c, 1.0 - flecks(n, s + 12, 0.01) * 0.35)

    wet = ss(0.56, 0.64, fbm(n, 2, 6, s + 13, gain=0.6))
    c = shade(c, 1.0 - wet * 0.15)
    c = desaturate(c, 0.1)

    bevel = ss(g, g + 0.02, de)
    height = (0.55 * bevel + 0.05 * stone + 0.03 * (grit(n, s + 2, 1.0) - 1.0)
              - cr * 0.45 - chip * 0.25 - broken_edge * 0.05)
    c = shade(c, cavity(height, 2.0, 1.6, 0.72))
    rough = (0.30 + 0.12 * fbm(n, 12, 4, s + 14) + 0.2 * gr + grout * 0.6 + chip * 0.5 + cr * 0.4)
    rough = rough * (1.0 - wet * 0.9) + wet * 0.03
    return Surface("floor_tile_cracked", c, height, rough, 7.0)


def ceiling_panel_stained(n):
    """600mm mineral board in a T-bar grid: fissured, water-ringed, one panel gone yellow."""
    s = 500
    v, u = grid(n)
    k = 4
    fu, fv = (u * k) % 1.0, (v * k) % 1.0
    ti, tj = np.floor(u * k).astype(int), np.floor(v * k).astype(int)
    de = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))       # 0.6m units
    bar = ss(0.022, 0.017, de)

    c = shade(fill(n, C(0.60, 0.59, 0.54)), mottle(n, s + 1, 0.12, 8) * grit(n, s + 2, 0.08))
    yellowed = (((tj == 1) & (ti == 0)) | ((tj == 3) & (ti == 2))).astype(np.float64)
    c = mix(c, C(0.52, 0.47, 0.33), yellowed * 0.5)

    f1, f2, _ = worley(n, 6000, s + 3)
    worm = ss(0.10, 0.0, f2 - f1) * ss(0.45, 0.65, vnoise(n, 160, seed=s + 4))
    pin = ss(0.93, 0.985, vnoise(n, 340, seed=s + 5, order=1))
    c = shade(c, 1.0 - worm * 0.25 - pin * 0.35)

    tide = fbm(n, 3, 6, s + 6, gain=0.6)
    water = ss(0.60, 0.70, tide) * (1 - bar)
    ring = ss(0.015, 0.0, np.abs(tide - 0.655)) * (1 - bar) * (0.5 + 0.5 * fbm(n, 40, 3, s + 7))
    c = mix(c, C(0.48, 0.40, 0.27), water * 0.5 + ring * 0.65)
    c = mix(c, C(0.24, 0.25, 0.21), ss(0.72, 0.9, fbm(n, 10, 4, s + 8)) * water * 0.6)
    c = mix(c, C(0.33, 0.32, 0.28), grime(n, s + 9, 3) * 0.3)

    grid_c = shade(fill(n, C(0.64, 0.64, 0.62)), grit(n, s + 10, 0.1))
    grid_c = mix(grid_c, C(0.40, 0.27, 0.16), ss(0.6, 0.78, tide) * 0.6)
    grid_c = mix(grid_c, C(0.30, 0.30, 0.28), grime(n, s + 11, 6) * 0.4)
    c = mix(c, grid_c, bar)
    c = shade(c, 1.0 - ss(0.05, 0.022, de) * (1 - bar) * 0.3)              # panel edge in shadow
    c = desaturate(c, 0.06)

    height = 0.5 * bar + (1 - bar) * (0.12 * fbm(n, 60, 3, s + 12) - worm * 0.25 - pin * 0.2) \
        - ss(0.07, 0.022, de) * (1 - bar) * 0.1
    rough = 0.93 - bar * 0.45 - water * 0.05
    return Surface("ceiling_panel_stained", c, height, rough, 6.0)


def concrete_stained(n):
    """Car park, stair and plant room concrete: formwork ties, blowholes, runs, damp, salt."""
    s = 600
    v, u = grid(n)
    c = shade(fill(n, C(0.47, 0.47, 0.45)), mottle(n, s + 1, 0.2, 5) * grit(n, s + 2, 0.16))
    board = np.abs(np.sin(v * 8 * np.pi + (fbm(n, 8, 3, s + 3) - 0.5) * 0.6))     # 300mm boards
    c = shade(c, 0.96 + 0.05 * ss(0.0, 0.08, board))
    r = runs(n, s + 4, 3, 0.84)
    c = mix(c, C(0.26, 0.26, 0.24), r * 0.5)
    damp = ss(0.58, 0.75, fbm(n, 2, 6, s + 5, gain=0.6))
    c = mix(c, C(0.30, 0.31, 0.28), damp * 0.45)
    salt = ss(0.65, 0.85, fbm(n, 16, 3, s + 6)) * damp
    c = mix(c, C(0.64, 0.64, 0.60), salt * 0.35)
    c = mix(c, C(0.25, 0.25, 0.23), grime(n, s + 7, 4) * 0.35)
    f1, _, idx = worley(n, 140, s + 8)
    hole = ss(0.16, 0.06, f1) * (np.random.default_rng(s).random(140)[idx] < 0.3)
    c = shade(c, 1.0 - hole * 0.5)
    q = 4                                                                     # ties on a 600mm grid
    tu, tv = (u * q) % 1.0 - 0.5, (v * q) % 1.0 - 0.5
    tie = ss(0.033, 0.021, np.sqrt(tu * tu + tv * tv))
    c = mix(c, C(0.20, 0.20, 0.19), tie * 0.8)
    cr = crack(n, 12, s + 9, 1.1, ss(0.6, 0.75, fbm(n, 3, 4, s + 10)), 0.02)
    c = shade(c, (1.0 - cr * 0.6) * (1.0 - flecks(n, s + 11, 0.015) * 0.4))
    height = (0.2 * fbm(n, 40, 4, s + 12) + 0.08 * (grit(n, s + 2, 1.0) - 1.0)
              - hole * 0.4 - tie * 0.5 - cr * 0.4)
    c = shade(c, cavity(height, 3.0, 1.8, 0.7))
    rough = 0.9 - damp * 0.18 - r * 0.1
    return Surface("concrete_stained", c, height, rough, 7.0)


def concrete_floor_wet(n):
    """B1's floor: dark trowelled concrete, a saw-cut joint every 3m, oil, tyre smears, water."""
    s = 700
    v, u = grid(n)
    c = shade(fill(n, C(0.33, 0.34, 0.34)), mottle(n, s + 1, 0.22, 4) * grit(n, s + 2, 0.15))
    agg = ss(0.8, 0.95, vnoise(n, 300, seed=s + 3, order=1))
    c = mix(c, C(0.46, 0.46, 0.44), agg * 0.35)
    swirl = fbm(n, 10, 4, s + 4)
    c = shade(c, 0.95 + 0.08 * swirl)
    tyre = ss(0.6, 0.88, warp(fbm(n, 2, 5, s + 5, aniso=(1.0, 0.18)), (fbm(n, 3, 3, s + 6) - 0.5) * 0.3, 0))
    c = mix(c, C(0.14, 0.14, 0.14), tyre * 0.4)
    oil = ss(0.68, 0.78, fbm(n, 6, 6, s + 7, gain=0.6))
    c = mix(c, C(0.08, 0.08, 0.09), oil * 0.6)
    c = mix(c, C(0.20, 0.19, 0.17), grime(n, s + 8, 3) * 0.35)
    joint = ss(0.004, 0.0015, np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)))
    c = mix(c, C(0.06, 0.06, 0.06), joint)
    cr = crack(n, 10, s + 9, 1.2, ss(0.6, 0.72, fbm(n, 3, 4, s + 10)), 0.02)
    c = shade(c, (1.0 - cr * 0.6) * (1.0 - flecks(n, s + 11, 0.02) * 0.4))
    wet = ss(0.54, 0.62, fbm(n, 2, 6, s + 12, gain=0.6))
    c = shade(c, 1.0 - wet * 0.25)
    height = (0.15 * fbm(n, 50, 4, s + 13) + agg * 0.05 + 0.04 * (grit(n, s + 2, 1.0) - 1.0)
              - joint * 0.6 - cr * 0.4 - wet * 0.05)
    c = shade(c, cavity(height, 3.0, 1.8, 0.7))
    rough = (0.82 - 0.1 * swirl - oil * 0.4) * (1 - wet * 0.95) + wet * 0.02 + joint * 0.1
    return Surface("concrete_floor_wet", c, height, rough, 6.0)


def metal_rusted(n):
    """Lift doors, mailboxes, pipes, plant-room steel: paint giving way to rust, which runs."""
    s = 800
    v, u = grid(n)
    rng = np.random.default_rng(s)
    paint = shade(fill(n, C(0.22, 0.28, 0.31)), mottle(n, s + 1, 0.18, 6) * grit(n, s + 2, 0.08))
    scratches = random_scratches(n, rng, 200, 0.07, angle=0.1, spread=0.7, width=1.5)
    spots = flecks(n, s + 3, 0.05)
    rust_f = fbm(n, 6, 7, s + 4, gain=0.6)
    rust = np.clip(ss(0.6, 0.72, rust_f) + spots * 0.8 + scratches * 0.35 * ss(0.4, 0.6, rust_f), 0, 1)
    rust_col = mix(fill(n, C(0.40, 0.21, 0.10)), fill(n, C(0.22, 0.11, 0.06)), fbm(n, 20, 3, s + 5))
    rust_col = mix(rust_col, C(0.55, 0.33, 0.15), ss(0.65, 0.85, fbm(n, 40, 2, s + 6)) * 0.5)
    rust_col = shade(rust_col, grit(n, s + 7, 0.2))
    streak = np.clip(blur_v(np.roll(rust, int(n * 0.04), axis=0), n * 0.05) * 2.0, 0, 1)
    streak = streak * (0.3 + 0.7 * fbm(n, 3, 4, s + 8, aniso=(0.15, 8)))
    c = mix(paint, C(0.36, 0.22, 0.12), streak * (1 - rust) * 0.65)
    c = mix(c, C(0.50, 0.50, 0.48), scratches * (1 - rust) * 0.55)
    c = mix(c, rust_col, rust)
    c = mix(c, C(0.12, 0.12, 0.11), grime(n, s + 9, 4) * 0.3)
    pits = ss(0.6, 0.9, vnoise(n, 260, seed=s + 10)) * rust
    height = 0.25 * (1 - rust) + rust * (0.1 + 0.2 * fbm(n, 60, 3, s + 11)) - pits * 0.15 - scratches * 0.08
    c = shade(c, cavity(height, 2.0, 1.6, 0.75))
    rough = 0.5 + 0.4 * rust + 0.1 * fbm(n, 20, 3, s + 12) - scratches * 0.2 + streak * 0.1
    return Surface("metal_rusted", c, height, rough, 8.0)


def door_steel_painted(n):
    """A 현관문's face: dark blue-grey enamel, orange peel, scratches, chips to the primer."""
    s = 900
    v, u = grid(n)
    rng = np.random.default_rng(s)
    c = shade(fill(n, C(0.20, 0.25, 0.28)), mottle(n, s + 1, 0.15, 5) * grit(n, s + 2, 0.05))
    c = mix(c, C(0.14, 0.16, 0.16), runs(n, s + 3, 3, 0.86) * 0.35)
    c = mix(c, C(0.12, 0.13, 0.13), grime(n, s + 4, 4) * 0.3)
    scr = random_scratches(n, rng, 260, 0.05, width=1.3)
    flake, lip, shadow = peel(n, s + 5, 0.05, cells=10, jag=0.02, chips=0.7)
    primer = mix(fill(n, C(0.44, 0.41, 0.36)), fill(n, C(0.38, 0.23, 0.13)), ss(0.5, 0.75, fbm(n, 12, 3, s + 6)))
    c = mix(c, C(0.44, 0.46, 0.46), scr * 0.5)
    c = mix(c, primer, flake)
    c = shade(c, 1 - shadow * 0.4)
    c = shade(c, 1.0 - flecks(n, s + 7, 0.02) * 0.35)
    orange = 0.08 * fbm(n, 220, 2, s + 8)
    height = 0.22 * (1 - flake) + orange + lip * 0.1 - scr * 0.12
    rough = 0.42 + 0.1 * fbm(n, 15, 3, s + 9) + 0.35 * flake
    return Surface("door_steel_painted", c, height, rough, 8.0)


def hazard_stripe(n):
    """Pillar and barrier paint: yellow and black at 45 degrees, scraped by bumpers."""
    s = 1000
    v, u = grid(n)
    w = (fbm(n, 6, 3, s + 1) - 0.5) * 0.006
    band = ((u + v + w) * 4.0) % 1.0
    yellow = np.clip(ss(0.505, 0.495, band) * ss(-0.005, 0.005, band), 0, 1)
    yel = shade(fill(n, C(0.74, 0.57, 0.10)), mottle(n, s + 2, 0.15, 5) * grit(n, s + 3, 0.08))
    blk = shade(fill(n, C(0.07, 0.07, 0.07)), grit(n, s + 4, 0.15))
    c = mix(blk, yel, yellow)
    flake, lip, shadow = peel(n, s + 5, 0.14, cells=7, jag=0.035)
    under = shade(fill(n, C(0.45, 0.45, 0.43)), mottle(n, s + 6, 0.15, 20) * grit(n, s + 7, 0.15))
    c = mix(c, under, flake)
    c = shade(c, 1 - shadow * 0.4)
    scuff = ss(0.75, 0.92, fbm(n, 3, 5, s + 8, aniso=(10, 1)))
    c = mix(c, C(0.28, 0.27, 0.25), scuff * 0.45)
    gr = grime(n, s + 9, 4)
    c = mix(c, C(0.18, 0.17, 0.14), gr * 0.4)
    c = shade(c, 1.0 - flecks(n, s + 10, 0.03) * 0.4)
    height = 0.25 * (1 - flake) + 0.1 * fbm(n, 60, 3, s + 11) + lip * 0.1 - scuff * 0.05
    rough = 0.55 + 0.3 * flake + 0.1 * gr
    return Surface("hazard_stripe", c, height, rough, 7.0)


def bathroom_tile(n):
    """The 1F toilet: 100mm white tiles, grout gone brown and green, a few cracked."""
    s = 1100
    v, u = grid(n)
    k = 6
    fu, fv = (u * k) % 1.0, (v * k) % 1.0
    ti, tj = np.floor(u * k).astype(int), np.floor(v * k).astype(int)
    de = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))
    g = 0.017
    grout = ss(g + 0.008, g, de)
    rng = np.random.default_rng(s)
    tone = rng.normal(0, 0.035, (k, k))
    c = fill(n, C(0.74, 0.75, 0.73)) * (1 + tone[tj, ti])[..., None]
    c = shade(c, mottle(n, s + 1, 0.06, 6) * grit(n, s + 2, 0.03))
    craze = crack_lines(n, 300, s + 3, 0.8) * 0.3
    c = shade(c, 1 - craze * 0.3)
    broken = crack(n, 16, s + 4, 1.2, ss(0.6, 0.72, fbm(n, 3, 4, s + 5)), 0.01) * (1 - grout)
    c = shade(c, 1 - broken * 0.6)
    gc = mix(fill(n, C(0.32, 0.27, 0.21)), fill(n, C(0.14, 0.18, 0.12)), ss(0.5, 0.8, fbm(n, 10, 3, s + 6)))
    c = mix(c, shade(gc, grit(n, s + 7, 0.2)), grout)
    gr = grime(n, s + 8, 4)
    c = mix(c, C(0.50, 0.45, 0.35), gr * 0.3 * (1 - grout))
    r = runs(n, s + 9, 3, 0.88) * ss(0.6, 0.7, fbm(n, 2, 3, s + 10))
    c = mix(c, C(0.46, 0.30, 0.17), r * 0.5)
    c = mix(c, C(0.12, 0.15, 0.10), flecks(n, s + 11, 0.05) * ss(0.05, 0.0, de) * 0.6)    # mould at the joints
    height = 0.5 * ss(g, g + 0.05, de) - broken * 0.3 - craze * 0.05
    c = shade(c, cavity(height, 2.0, 1.5, 0.75))
    rough = 0.14 + 0.15 * gr + grout * 0.75 + broken * 0.4
    return Surface("bathroom_tile", c, height, rough, 6.0)


def plywood_old(n):
    """The boards nailed over the fourth floor's end: rotary-cut grain, rain marks, nails."""
    s = 1200
    v, u = grid(n)
    wv = fbm(n, 2, 4, s + 1, aniso=(1.0, 0.25)) - 0.5
    figure = v * 16 + wv * 1.2 + (fbm(n, 6, 3, s + 2, aniso=(4, 0.5)) - 0.5) * 0.35
    ring = 0.5 + 0.5 * np.sin(figure * 2 * np.pi)
    c = mix(fill(n, C(0.56, 0.45, 0.31)), fill(n, C(0.44, 0.33, 0.21)), ss(0.25, 0.95, ring) * 0.65)
    fibre = fbm(n, 6, 4, s + 3, aniso=(24, 0.4))
    c = shade(c, (0.88 + 0.18 * fibre) * grit(n, s + 4, 0.06))
    patch_f1, _, pidx = worley(n, 9, s + 5)
    patch = ss(0.16, 0.14, patch_f1) * (np.random.default_rng(s).random(9)[pidx] < 0.5)
    c = mix(c, C(0.60, 0.50, 0.35), patch * 0.5)
    grey = ss(0.45, 0.8, fbm(n, 3, 6, s + 6, gain=0.6))
    c = mix(c, C(0.36, 0.34, 0.30), grey * 0.6)                                     # weathered silver
    water = ss(0.6, 0.7, fbm(n, 2, 6, s + 7, gain=0.6))
    c = mix(c, C(0.24, 0.20, 0.15), water * 0.5)
    c = mix(c, C(0.20, 0.17, 0.13), runs(n, s + 8, 2, 0.9) * 0.25)
    nu = ((u * 6) % 1.0 - 0.5) * 0.2                                             # metres, 200mm pitch
    nv = ((v * 2) % 1.0 - 0.06) * 0.6                                            # two rows per board
    nail = ss(0.004, 0.0022, np.sqrt(nu * nu + nv * nv))
    c = mix(c, C(0.13, 0.11, 0.09), nail)
    c = mix(c, C(0.30, 0.18, 0.10), ss(0.012, 0.004, np.sqrt(nu * nu + nv * nv)) * 0.35)   # rust ring
    height = 0.12 * ring + 0.1 * fibre - nail * 0.4
    rough = 0.82 - water * 0.1 + grey * 0.08
    return Surface("plywood_old", c, height, rough, 6.0)


def plastic_sheet(n):
    """Milky polythene hung over the deleted unit's wall: folds, creases, dirt, see-through."""
    s = 1300
    folds = fbm(n, 3, 5, s + 1, aniso=(0.25, 4.0))
    crease = np.abs(np.sin(warp(fbm(n, 4, 3, s + 2), (fbm(n, 6, 3, s + 3) - 0.5) * 0.05, 0) * 7 * np.pi))
    crease = ss(0.05, 0.0, crease)
    c = shade(fill(n, C(0.74, 0.76, 0.74)), (0.85 + 0.2 * folds) * grit(n, s + 4, 0.03))
    dirt = runs(n, s + 5, 3, 0.84)
    c = mix(c, C(0.42, 0.41, 0.36), dirt * 0.5)
    c = mix(c, C(0.40, 0.39, 0.35), grime(n, s + 6, 3) * 0.3)
    alpha = np.clip(0.42 + 0.3 * folds + 0.25 * dirt + crease * 0.15, 0, 1)
    height = folds * 0.9 + crease * 0.1
    rough = 0.32 + 0.3 * dirt
    return Surface("plastic_sheet", c, height, rough, 14.0, alpha=alpha)


SURFACES = [
    wall_two_tone, wall_peeling_paint, wall_mould_stain, floor_tile_cracked,
    ceiling_panel_stained, concrete_stained, concrete_floor_wet, metal_rusted,
    door_steel_painted, hazard_stripe, bathroom_tile, plywood_old, plastic_sheet,
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--size", type=int, default=1024)
    ap.add_argument("--only", default="")
    ap.add_argument("--game", action="store_true", help="also write the game's copies")
    args = ap.parse_args()

    only = {x.strip() for x in args.only.split(",") if x.strip()}
    done = []
    for make in SURFACES:
        if only and make.__name__ not in only:
            continue
        t0 = time.time()
        surface = make(args.size)
        write(surface, args.size, args.game)
        print("%-24s %5.1fs" % (surface.name, time.time() - t0))

    # The preview shows every surface on disk, not only the ones drawn this run.
    rows = [(stem,) + META[stem] for stem in META
            if os.path.exists(os.path.join(OUT_DIR, stem + "_albedo.png"))]
    preview(rows)
    print("preview -> " + os.path.join(OUT_DIR, "tile_preview.png"))


if __name__ == "__main__":
    main()
