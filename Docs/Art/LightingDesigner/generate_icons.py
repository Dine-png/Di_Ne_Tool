"""Render the editable SVG menu icons to transparent 256px Unity textures.

Run with Python + CairoSVG from any directory. These sources stay outside the
Unity package; PNGs and their stable .meta GUIDs are the shipped assets.
"""
from pathlib import Path
import cairosvg

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT.parents[2] / 'Packages/com.dinetool.avatar-tools/Assets/LightingDesigner'

def split(path, extra=""):
    return (f'<path d="{path}" fill="white" fill-rule="evenodd" stroke="white" stroke-width="10" '
            f'clip-path="url(#left)"/>'
            f'<path d="{path}" fill="none" stroke="white" stroke-width="10" '
            f'clip-path="url(#right)"/>' + extra)

def stroke(path, width=12):
    return f'<path d="{path}" fill="none" stroke="white" stroke-width="{width}"/>'

def circle(x, y, r, fill="none", width=10):
    return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="white" stroke-width="{width}"/>'

def orb(x, y, r):
    return f'M {x-r} {y} a {r} {r} 0 1 0 {2*r} 0 a {r} {r} 0 1 0 {-2*r} 0 Z'

icons = {}
icons["Light"] = split(
    'M 92 176 C 92 149 66 142 66 104 C 66 68 92 44 128 44 '
    'C 164 44 190 68 190 104 C 190 142 164 149 164 176 Z',
    stroke('M 98 195 H 158 M 106 214 H 150 M 46 101 H 28 M 59 55 L 45 41 '
           'M 210 101 H 228 M 197 55 L 211 41'))
icons["Saturation"] = (
    '<path d="M 90 42 C 80 61 48 98 48 126 A 42 42 0 0 0 132 126 '
    'C 132 98 100 61 90 42 Z" fill="white"/>'
    '<path d="M 169 95 C 159 114 133 144 133 169 A 36 36 0 0 0 205 169 '
    'C 205 144 179 114 169 95 Z" fill="none" stroke="white" stroke-width="11"/>')
icons["Hue"] = split(orb(128, 128, 84) + orb(128, 128, 30)) + stroke(
    'M 128 44 V 98 M 128 158 V 212 M 55 86 L 102 113 M 154 143 L 201 170 '
    'M 55 170 L 102 143 M 154 113 L 201 86', 8) + circle(128, 128, 30)
icons["Brightness"] = split(orb(128,128,52), stroke(
    'M 128 30 V 48 M 128 208 V 226 M 30 128 H 48 M 208 128 H 226 '
    'M 59 59 L 72 72 M 184 184 L 197 197 M 59 197 L 72 184 M 184 72 L 197 59'))
icons["Gamma"] = stroke('M 46 42 V 207 H 216', 10) + stroke(
    'M 62 190 C 104 190 91 64 199 56', 17)
icons["ColorTemp"] = split(
    'M 110 147 V 56 A 18 18 0 0 1 146 56 V 147 '
    'A 39 39 0 1 1 110 147 Z',
    stroke('M 128 88 V 173', 10) + circle(128,181,13,"white",0)
    + stroke('M 190 61 V 93 M 176 69 L 204 85 M 176 85 L 204 69 '
             'M 42 155 V 193 M 23 174 H 61', 7))
icons["Monochrome"] = split(orb(128,128,80))
star = 'M 128 35 Q 146 102 212 128 Q 146 148 128 216 Q 110 148 44 128 Q 110 102 128 35 Z'
icons["Emission"] = split(star, stroke('M 203 38 V 66 M 189 52 H 217 M 46 197 V 221 M 34 209 H 58',7))
icons["ShadowStrength"] = split(orb(128,96,56)) + (
    '<ellipse cx="128" cy="190" rx="86" ry="21" fill="white" clip-path="url(#left)"/>'
    '<ellipse cx="128" cy="190" rx="86" ry="21" fill="none" stroke="white" '
    'stroke-width="9" clip-path="url(#right)"/>')
icons["OutlineTint"] = split(
    'M 125 43 C 70 43 40 82 40 126 C 40 179 80 213 129 213 C 146 213 149 196 136 188 '
    'C 123 180 134 161 153 164 C 185 170 215 146 215 112 '
    'C 215 70 174 43 125 43 Z' + orb(81,106,14) + orb(103,72,12)
    + orb(74,145,13) + orb(96,178,11))
icons["OutlineWidth"] = split(
    'M 63 42 H 193 Q 213 42 213 62 V 194 Q 213 214 193 214 '
    'H 63 Q 43 214 43 194 V 62 Q 43 42 63 42 Z '
    'M 71 62 H 185 Q 193 62 193 70 V 186 Q 193 194 185 194 '
    'H 71 Q 63 194 63 186 V 70 Q 63 62 71 62 Z') + stroke(
    'M 101 89 H 158 Q 167 89 167 98 V 158 Q 167 167 158 167 '
    'H 101 Q 89 167 89 158 V 98 Q 89 89 101 89 Z', 9)
icons["Reflectance"] = split(orb(115,143,69)) + (
    '<path d="M 192 30 Q 199 58 225 66 Q 199 74 192 102 '
    'Q 185 74 159 66 Q 185 58 192 30 Z" fill="white"/>')
icons["LightDir"] = split(
    'M 77 43 L 170 136 L 188 118 L 198 187 L 130 176 L 148 158 L 55 65 Z',
    stroke('M 63 140 A 76 76 0 0 0 180 222', 10))
icons["Power"] = stroke('M 78 66 A 78 78 0 1 0 178 66', 15) + stroke('M 128 35 V 125', 18)
icons["Presets"] = stroke('M 43 68 H 213 M 43 128 H 213 M 43 188 H 213', 10) + (
    '<g fill="white"><circle cx="86" cy="68" r="17"/>'
    '<circle cx="170" cy="128" r="17"/><circle cx="113" cy="188" r="17"/></g>')
icons["Group"] = stroke('M 53 149 H 43 Q 34 149 34 140 V 43 Q 34 34 43 34 '
    'H 140 Q 149 34 149 43 V 53 M 82 181 H 72 Q 63 181 63 172 '
    'V 72 Q 63 63 72 63 H 172 Q 181 63 181 72 V 82', 9) + split(
    'M 104 94 H 208 Q 220 94 220 106 V 208 Q 220 220 208 220 '
    'H 104 Q 94 220 94 208 V 106 Q 94 94 104 94 Z')
icons["Next"] = split('M 77 44 L 164 108 L 190 128 L 164 148 L 77 212 '
    'L 77 169 L 131 128 L 77 87 Z')

DEFS = '''<defs>
  <clipPath id="left"><rect width="128" height="256"/></clipPath>
  <clipPath id="right"><rect x="128" width="128" height="256"/></clipPath>
</defs>'''

if __name__ == "__main__":
    for name, body in icons.items():
        svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" '
               'viewBox="0 0 256 256" stroke-linecap="round" stroke-linejoin="round">'
               + DEFS + body + '</svg>')
        source = ROOT / f'Menu{name}.svg'
        source.write_text(svg, encoding="utf-8")
        cairosvg.svg2png(bytestring=svg.encode(), write_to=str(ASSETS / f'Menu{name}.png'))
    print(f'Rendered {len(icons)} transparent menu icons.')
