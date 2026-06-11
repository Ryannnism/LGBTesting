#!/usr/bin/env python3
"""Render Mermaid diagrams and export USER_ROLES.md to PDF."""

from __future__ import annotations

import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Pillow is required: pip install Pillow", file=sys.stderr)
    sys.exit(1)

try:
    import markdown
except ImportError:
    print("markdown is required: pip install markdown", file=sys.stderr)
    sys.exit(1)

DOCS = Path(__file__).resolve().parent
DIAGRAMS = DOCS / "diagrams"
MD_FILE = DOCS / "USER_ROLES.md"
OUT_FLOWCHARTS_PDF = DOCS / "USER_ROLES_FLOWCHARTS.pdf"
OUT_FULL_PDF = DOCS / "USER_ROLES.pdf"
MMDC = DOCS / "node_modules" / ".bin" / "mmdc"
CHROME = Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")

# Order matches mermaid blocks in USER_ROLES.md (sections 2, 4, 5, 6, 7, 8).
DIAGRAM_ORDER = [
    "01-role-hierarchy",
    "02-customer-onboarding",
    "03-moi-moa-pipeline",
    "04-job-types-visibility",
    "05-status-lifecycle",
    "06-signatory-types",
]

TITLES = {
    "01-role-hierarchy": "1. Role hierarchy (4 roles + permission overlays)",
    "02-customer-onboarding": "2. Customer onboarding & auto-provisioned accounts",
    "03-moi-moa-pipeline": "3. End-to-end MOI/MOA pipeline with roles",
    "04-job-types-visibility": "4. Job types & visibility by role",
    "05-status-lifecycle": "5. Display status lifecycle",
    "06-signatory-types": "6. Client vs internal signatory",
}

MERMAID_RE = re.compile(r"```mermaid\n(.*?)```", re.DOTALL)


def chrome_env() -> dict[str, str]:
    env = os.environ.copy()
    if CHROME.exists():
        env["PUPPETEER_EXECUTABLE_PATH"] = str(CHROME)
    return env


def ensure_mmdc() -> Path:
    if MMDC.exists():
        return MMDC
    print("Installing @mermaid-js/mermaid-cli locally in docs/ …")
    subprocess.run(
        ["npm", "install", "@mermaid-js/mermaid-cli@11.4.0", "--no-save"],
        cwd=DOCS,
        check=True,
    )
    if not MMDC.exists():
        raise SystemExit("mmdc not found after npm install")
    return MMDC


def render_png(mmdc: Path, mmd_file: Path, png_file: Path) -> None:
    subprocess.run(
        [
            str(mmdc),
            "-i",
            str(mmd_file),
            "-o",
            str(png_file),
            "-w",
            "3600",
            "-H",
            "2800",
            "-s",
            "2",
            "-b",
            "white",
        ],
        check=True,
        env=chrome_env(),
    )


def add_title_banner(img: Image.Image, title: str) -> Image.Image:
    from PIL import ImageDraw, ImageFont

    w = img.size[0]
    banner = Image.new("RGB", (w, 120), "white")
    draw = ImageDraw.Draw(banner)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 48)
    except OSError:
        font = ImageFont.load_default()
    draw.text((40, 30), title, fill="black", font=font)
    return banner


def build_flowcharts_pdf(png_by_stem: dict[str, Path]) -> None:
    pages: list[Image.Image] = []
    for stem in DIAGRAM_ORDER:
        png = png_by_stem[stem]
        title = TITLES.get(stem, stem)
        img = Image.open(png).convert("RGB")
        pages.append(add_title_banner(img, title))
        pages.append(img)

    pages[0].save(
        OUT_FLOWCHARTS_PDF,
        "PDF",
        resolution=200.0,
        save_all=True,
        append_images=pages[1:],
    )
    print(f"Wrote {OUT_FLOWCHARTS_PDF}")


def md_with_rendered_diagrams(png_by_stem: dict[str, Path]) -> str:
    text = MD_FILE.read_text(encoding="utf-8")
    diagram_idx = 0

    def replace_mermaid(match: re.Match[str]) -> str:
        nonlocal diagram_idx
        if diagram_idx >= len(DIAGRAM_ORDER):
            return match.group(0)
        stem = DIAGRAM_ORDER[diagram_idx]
        diagram_idx += 1
        png = png_by_stem[stem]
        title = TITLES.get(stem, stem)
        return (
            f'\n<div class="diagram-page">\n'
            f'  <p class="diagram-caption">{title}</p>\n'
            f'  <img src="file://{png.resolve()}" alt="{title}" />\n'
            f"</div>\n"
        )

    text = MERMAID_RE.sub(replace_mermaid, text)
    # Drop self-referential PDF links from the printable doc.
    text = re.sub(
        r"\*\*Enlarged flowcharts \(PDF\):\*\*.*\n"
        r"\*\*Regenerate PDF:\*\*.*\n\n",
        "",
        text,
    )
    return text


HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>User roles, hierarchy &amp; system flow</title>
  <style>
    @page {{ size: A4 landscape; margin: 14mm; }}
    body {{
      font-family: -apple-system, BlinkMacSystemFont, "Helvetica Neue", Helvetica, Arial, sans-serif;
      font-size: 11pt;
      line-height: 1.45;
      color: #111;
      max-width: 100%;
    }}
    h1 {{ font-size: 22pt; margin-top: 0; page-break-after: avoid; }}
    h2 {{
      font-size: 15pt;
      margin-top: 1.4em;
      page-break-after: avoid;
      border-bottom: 1px solid #ccc;
      padding-bottom: 0.2em;
    }}
    h3 {{ font-size: 12pt; page-break-after: avoid; }}
    table {{
      border-collapse: collapse;
      width: 100%;
      margin: 0.8em 0;
      font-size: 9.5pt;
      page-break-inside: avoid;
    }}
    th, td {{
      border: 1px solid #bbb;
      padding: 5px 8px;
      text-align: left;
      vertical-align: top;
    }}
    th {{ background: #f0f0f0; }}
    code, pre {{
      font-family: "SF Mono", Menlo, Monaco, Consolas, monospace;
      font-size: 9pt;
    }}
    pre {{
      background: #f6f6f6;
      border: 1px solid #ddd;
      padding: 10px;
      white-space: pre-wrap;
      page-break-inside: avoid;
    }}
    hr {{ border: none; border-top: 1px solid #ddd; margin: 1.5em 0; }}
    .diagram-page {{
      page-break-before: always;
      page-break-inside: avoid;
      margin: 0;
      padding: 0;
      text-align: center;
    }}
    .diagram-caption {{
      font-size: 13pt;
      font-weight: 600;
      margin: 0 0 8px 0;
      text-align: left;
    }}
    .diagram-page img {{
      width: 100%;
      max-width: 100%;
      height: auto;
      display: block;
      margin: 0 auto;
    }}
    p {{ orphans: 3; widows: 3; }}
    ul {{ margin: 0.4em 0; }}
  </style>
</head>
<body>
{body}
</body>
</html>
"""


def build_full_md_pdf(png_by_stem: dict[str, Path]) -> None:
    if not CHROME.exists():
        raise SystemExit(f"Chrome not found at {CHROME} — required for full MD PDF")

    md_text = md_with_rendered_diagrams(png_by_stem)
    body = markdown.markdown(
        md_text,
        extensions=["tables", "fenced_code", "sane_lists"],
    )
    html = HTML_TEMPLATE.format(body=body)

    with tempfile.TemporaryDirectory() as tmp:
        html_path = Path(tmp) / "USER_ROLES.html"
        pdf_path = Path(tmp) / "USER_ROLES.pdf"
        html_path.write_text(html, encoding="utf-8")

        subprocess.run(
            [
                str(CHROME),
                "--headless=new",
                "--disable-gpu",
                "--no-pdf-header-footer",
                f"--print-to-pdf={pdf_path}",
                html_path.as_uri(),
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        OUT_FULL_PDF.write_bytes(pdf_path.read_bytes())

    print(f"Wrote {OUT_FULL_PDF}")


def main() -> None:
    mmdc = ensure_mmdc()
    png_by_stem: dict[str, Path] = {}

    for stem in DIAGRAM_ORDER:
        mmd = DIAGRAMS / f"{stem}.mmd"
        png = DIAGRAMS / f"{stem}.png"
        if not mmd.exists():
            raise SystemExit(f"Missing diagram source: {mmd}")
        print(f"Rendering {mmd.name} …")
        render_png(mmdc, mmd, png)
        png_by_stem[stem] = png

    build_flowcharts_pdf(png_by_stem)
    build_full_md_pdf(png_by_stem)


if __name__ == "__main__":
    main()
