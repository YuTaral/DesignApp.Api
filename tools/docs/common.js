// Shared styles and helpers for the two DesignApp documents.
const d = require('docx')
const { Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType, BorderStyle, AlignmentType, HeadingLevel, LevelFormat, Footer, PageNumber } = d

const FONT = 'Calibri'
const ACCENT = 'C2410C' // warm orange, like the app's primary color
const HEAD_FILL = 'FDEBDD'
const ZEBRA = 'F8F6F3'
const GRID = 'D9D4CE'

const styles = {
  default: { document: { run: { font: FONT, size: 21 }, paragraph: { spacing: { after: 100, line: 276 } } } },
  paragraphStyles: [
    { id: 'Title', name: 'Title', basedOn: 'Normal', next: 'Normal', run: { font: FONT, size: 48, bold: true, color: '1F1B18' },
      paragraph: { spacing: { before: 0, after: 120 }, keepNext: true } },
    { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
      run: { font: FONT, size: 32, bold: true, color: ACCENT }, paragraph: { spacing: { before: 360, after: 140 }, keepNext: true, outlineLevel: 0 } },
    { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
      run: { font: FONT, size: 26, bold: true, color: '1F1B18' }, paragraph: { spacing: { before: 280, after: 100 }, keepNext: true, outlineLevel: 1 } },
    { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true,
      run: { font: FONT, size: 22, bold: true, color: '57514B' }, paragraph: { spacing: { before: 200, after: 80 }, keepNext: true, outlineLevel: 2 } },
  ],
}

const numbering = {
  config: [{
    reference: 'bullets',
    levels: [
      { level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 240 } } } },
      { level: 1, format: LevelFormat.BULLET, text: '–', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 240 } } } },
    ],
  }, {
    reference: 'steps',
    levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 360, hanging: 300 } } } }],
  }],
}

/** Text with **bold** and `code` spans. */
function runs(text, base = {}) {
  const out = []
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g
  let last = 0, m
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...base }))
    const t = m[0]
    if (t.startsWith('**')) out.push(new TextRun({ text: t.slice(2, -2), bold: true, ...base }))
    else out.push(new TextRun({ text: t.slice(1, -1), font: 'Consolas', size: (base.size ?? 21) - 2, color: '7C2D12', ...base, ...(base.font ? {} : {}) }))
    last = m.index + t.length
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...base }))
  return out
}

const p = (text, opts = {}) => new Paragraph({ children: runs(text), ...opts })
const h1 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(text)] })
const h2 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(text)] })
const h3 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(text)] })
const bullet = (text, level = 0) => new Paragraph({ numbering: { reference: 'bullets', level }, children: runs(text), spacing: { after: 60 } })
const step = (text) => new Paragraph({ numbering: { reference: 'steps', level: 0 }, children: runs(text), spacing: { after: 60 } })
const title = (text, subtitle) => [
  new Paragraph({ style: 'Title', children: [new TextRun(text)] }),
  new Paragraph({ children: [new TextRun({ text: subtitle, color: '6B645D', size: 24 })], spacing: { after: 240 },
    border: { bottom: { style: BorderStyle.SINGLE, size: 12, color: ACCENT, space: 6 } } }),
]

const cellBorders = { top: { style: BorderStyle.SINGLE, size: 4, color: GRID }, bottom: { style: BorderStyle.SINGLE, size: 4, color: GRID },
  left: { style: BorderStyle.SINGLE, size: 4, color: GRID }, right: { style: BorderStyle.SINGLE, size: 4, color: GRID } }

/** A table with a shaded header row and zebra rows. widths in DXA must sum to the table width. */
function table(headers, rows, widths) {
  const total = widths.reduce((a, b) => a + b, 0)
  const cell = (text, i, fill, bold = false) => new TableCell({
    width: { size: widths[i], type: WidthType.DXA },
    borders: cellBorders,
    shading: fill ? { type: ShadingType.CLEAR, color: 'auto', fill } : undefined,
    margins: { top: 70, bottom: 70, left: 100, right: 100 },
    children: [new Paragraph({ spacing: { after: 0, line: 252 }, children: runs(text, { size: 18, ...(bold ? { bold: true } : {}) }) })],
  })
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: [
      new TableRow({ tableHeader: true, cantSplit: true, children: headers.map((h, i) => cell(h, i, HEAD_FILL, true)) }),
      ...rows.map((r, n) => new TableRow({ cantSplit: true, children: r.map((t, i) => cell(t, i, n % 2 ? ZEBRA : undefined, i === 0)) })),
    ],
  })
}

const footer = (label) => ({
  default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [
    new TextRun({ text: `${label}  ·  page `, color: '8A837C', size: 16 }),
    new TextRun({ children: [PageNumber.CURRENT], color: '8A837C', size: 16 }),
  ] })] }),
})

const spacer = () => new Paragraph({ children: [], spacing: { after: 120 } })

module.exports = { d, styles, numbering, p, h1, h2, h3, bullet, step, title, table, footer, spacer, runs, ACCENT }
