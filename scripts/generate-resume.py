from __future__ import annotations

import html
import re
import shutil
import sys
from pathlib import Path

try:
    from reportlab.lib import colors
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
    from reportlab.lib.units import mm
    from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer
except ModuleNotFoundError as exc:
    missing = exc.name or "reportlab"
    print(
        f"Missing Python package: {missing}. Install dependencies with: "
        "python -m pip install reportlab",
        file=sys.stderr,
    )
    raise SystemExit(1)


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "resume.md"
SITE_RESUME = ROOT / "src" / "content" / "resume" / "resume.md"
ROOT_PDF = ROOT / "alistair-obrien-resume.pdf"
PUBLIC_PDF = ROOT / "public" / "alistair-obrien-resume.pdf"


def markdown_inline(text: str) -> str:
    escaped = html.escape(text)
    escaped = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", escaped)
    return escaped


def read_resume() -> list[str]:
    if not SOURCE.exists():
        raise FileNotFoundError(f"Resume source not found: {SOURCE}")
    return SOURCE.read_text(encoding="utf-8").splitlines()


def split_source(lines: list[str]) -> tuple[str, list[str], list[str]]:
    name = ""
    contact: list[str] = []
    body_start = 0

    for index, line in enumerate(lines):
        if line.startswith("# "):
            name = line[2:].strip()
            body_start = index + 1
            break

    while body_start < len(lines) and lines[body_start].strip() == "":
        body_start += 1

    while body_start < len(lines) and lines[body_start].strip() != "":
        contact.append(lines[body_start].rstrip("  "))
        body_start += 1

    while body_start < len(lines) and lines[body_start].strip() == "":
        body_start += 1

    return name, contact, lines[body_start:]


def write_site_markdown(name: str, body_lines: list[str]) -> None:
    SITE_RESUME.parent.mkdir(parents=True, exist_ok=True)
    body = "\n".join(body_lines).strip() + "\n"
    SITE_RESUME.write_text(f"---\ntitle: {name}\n---\n{body}", encoding="utf-8")


def build_pdf(lines: list[str], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    name, contact_lines, body_lines = split_source(lines)

    styles = getSampleStyleSheet()
    base_font = "Helvetica"
    bold_font = "Helvetica-Bold"

    title = ParagraphStyle(
        "ResumeTitle",
        parent=styles["Title"],
        fontName=bold_font,
        fontSize=16,
        leading=18,
        alignment=1,
        spaceAfter=2,
        textColor=colors.HexColor("#111827"),
    )
    contact = ParagraphStyle(
        "ResumeContact",
        parent=styles["Normal"],
        fontName=base_font,
        fontSize=7.8,
        leading=9.2,
        alignment=1,
        textColor=colors.HexColor("#374151"),
        spaceAfter=5,
    )
    section = ParagraphStyle(
        "ResumeSection",
        parent=styles["Heading2"],
        fontName=bold_font,
        fontSize=9,
        leading=10,
        spaceBefore=5,
        spaceAfter=2,
        textTransform="uppercase",
        textColor=colors.HexColor("#111827"),
    )
    role = ParagraphStyle(
        "ResumeRole",
        parent=styles["Heading3"],
        fontName=bold_font,
        fontSize=8.8,
        leading=9.8,
        spaceBefore=3,
        spaceAfter=1,
        textColor=colors.HexColor("#111827"),
    )
    normal = ParagraphStyle(
        "ResumeNormal",
        parent=styles["Normal"],
        fontName=base_font,
        fontSize=7.7,
        leading=8.8,
        spaceAfter=1.8,
        textColor=colors.HexColor("#1F2937"),
    )
    bullet = ParagraphStyle(
        "ResumeBullet",
        parent=normal,
        leftIndent=10,
        firstLineIndent=-7,
        bulletIndent=0,
        spaceAfter=1.0,
    )
    nested_bullet = ParagraphStyle(
        "ResumeNestedBullet",
        parent=normal,
        leftIndent=20,
        firstLineIndent=-7,
        bulletIndent=10,
        spaceAfter=0.8,
    )

    story = [
        Paragraph(markdown_inline(name), title),
        Paragraph(markdown_inline(" | ".join(contact_lines)), contact),
    ]
    paragraph_buffer: list[str] = []

    def flush_paragraph() -> None:
        nonlocal paragraph_buffer
        if paragraph_buffer:
            text = " ".join(item.strip() for item in paragraph_buffer)
            story.append(Paragraph(markdown_inline(text), normal))
            paragraph_buffer = []

    for line in body_lines:
        stripped = line.strip()
        if not stripped:
            flush_paragraph()
            continue

        if stripped.startswith("## "):
            flush_paragraph()
            story.append(Paragraph(markdown_inline(stripped[3:].strip()), section))
            continue

        if stripped.startswith("### "):
            flush_paragraph()
            story.append(Paragraph(markdown_inline(stripped[4:].strip()), role))
            continue

        bullet_match = re.match(r"^(\s*)-\s+(.*)$", line)
        if bullet_match:
            flush_paragraph()
            indent, text = bullet_match.groups()
            level = 1 if len(indent) >= 2 else 0
            style = nested_bullet if level else bullet
            story.append(Paragraph(markdown_inline(text), style, bulletText="-"))
            continue

        paragraph_buffer.append(stripped)

    flush_paragraph()

    doc = SimpleDocTemplate(
        str(output_path),
        pagesize=A4,
        rightMargin=11 * mm,
        leftMargin=11 * mm,
        topMargin=10 * mm,
        bottomMargin=10 * mm,
        title="Alistair O'Brien Resume",
        author="Alistair O'Brien",
    )
    doc.build(story)


def main() -> None:
    lines = read_resume()
    name, _, body = split_source(lines)
    write_site_markdown(name, body)
    build_pdf(lines, ROOT_PDF)
    PUBLIC_PDF.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT_PDF, PUBLIC_PDF)
    print(f"Generated {SITE_RESUME.relative_to(ROOT)}")
    print(f"Generated {ROOT_PDF.relative_to(ROOT)}")
    print(f"Generated {PUBLIC_PDF.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
