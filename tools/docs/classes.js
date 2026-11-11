// Builds "DesignApp - Class Reference.docx" from the code itself: every project and folder in the
// order of the solution, every top-level class, record, interface and enum with its XML summary,
// and every exported component, hook, function, constant and type of the web app with its JSDoc.
// Run after each commit so the document matches what was committed (npm run docs builds both documents).
const fs = require('fs')
const path = require('path')
const { d, styles, numbering, p, h1, h2, h3, bullet, title, table, footer } = require('./common')
const { Document, Packer, PageOrientation } = d

// The folder that holds both repositories: tools/docs -> DesignApp.Api -> DesignApp.
const ROOT = path.resolve(__dirname, '..', '..', '..')
const API = path.join(ROOT, 'DesignApp.Api')
const WEB = path.join(ROOT, 'DesignApp.Web')
const W = [2500, 2700, 1300, 8500] // landscape A4 usable width, 15000
const HEAD = ['File', 'Name', 'Kind', 'What it does']

const read = (f) => fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n')
const skipDir = (name) => ['bin', 'obj', 'node_modules', '.vs', 'Properties'].includes(name)

/** Files under a folder, grouped by their folder relative to it (root first, then alphabetical). */
function filesByFolder(base, ext) {
  const groups = new Map()
  const walk = (dir) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const full = path.join(dir, e.name)
      if (e.isDirectory()) { if (!skipDir(e.name)) walk(full); continue }
      if (!ext.some((x) => e.name.endsWith(x))) continue
      const folder = path.relative(base, dir).replace(/\\/g, '/') || ''
      if (!groups.has(folder)) groups.set(folder, [])
      groups.get(folder).push(full)
    }
  }
  walk(base)
  return [...groups.entries()].sort(([a], [b]) => (a === '' ? -1 : b === '' ? 1 : a.localeCompare(b)))
}

/** Turns an XML doc fragment into plain text with `code` spans. */
function xmlText(xml) {
  const shortCref = (c) => c.replace(/^[A-Z]:/, '').replace(/^DesignApp\.(Domain|Application|Infrastructure|Api|ServiceDefaults|AppHost)\./, '')
  return xml
    .replace(/<see\s+cref="([^"]+)"\s*\/>/g, (_, c) => '`' + shortCref(c) + '`')
    .replace(/<see\s+langword="([^"]+)"\s*\/>/g, '`$1`')
    .replace(/<(paramref|typeparamref)\s+name="([^"]+)"\s*\/>/g, '`$2`')
    .replace(/<c>([^<]*)<\/c>/g, '`$1`')
    .replace(/<\/?(para|list|item|description|term|br)\s*\/?>/g, ' ')
    .replace(/<[^>]+>/g, '')
    .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&').replace(/&quot;/g, '"')
    .replace(/\s+/g, ' ')
    .trim()
}

const CS_TYPE = /^(?:public|internal)\s+((?:(?:sealed|static|abstract|partial|readonly)\s+)*)(record\s+struct|record|class|interface|enum|struct)\s+(\w+)/

/** The top-level types of a C# file, each with its summary. */
function csTypes(file) {
  const lines = read(file).split('\n')
  const out = []
  lines.forEach((line, i) => {
    const m = CS_TYPE.exec(line)
    if (!m) return
    let j = i
    while (j > 0 && lines[j - 1].startsWith('///')) j--
    const docs = lines.slice(j, i).map((l) => l.replace(/^\/\/\/ ?/, '')).join('\n')
    const summary = /<summary>([\s\S]*?)<\/summary>/.exec(docs)
    const mods = m[1].trim().split(/\s+/).filter((x) => x === 'static' || x === 'abstract')
    const kind = [...mods, m[2].replace(/\s+/, ' ')].join(' ')
    const text = summary ? xmlText(summary[1]) : /<inheritdoc/.test(docs) ? 'See its base type or interface.' : ''
    out.push([m[3], kind, text])
  })
  return out
}

/** Rows for one C# project: a heading per folder, a row per type. */
function csProject(dir) {
  const children = []
  for (const [folder, files] of filesByFolder(dir, ['.cs'])) {
    if (/(^|\/)Migrations$/.test(folder)) {
      const names = files.map((f) => path.basename(f, '.cs')).filter((n) => /^\d{14}_/.test(n) && !n.endsWith('.Designer'))
      children.push(h3(folder + '/'), table(HEAD, [[
        `${names.length} migrations`, 'EF Core migrations', 'generated',
        `The database schema, step by step, oldest first: ${names.map((n) => '`' + n.replace(/^\d{14}_/, '') + '`').join(', ')}. ` +
        '`AppDbContextModelSnapshot` is EF Core\'s record of the current model. Each migration has a generated `.Designer.cs` partner.',
      ]], W))
      continue
    }
    const rows = []
    for (const f of files) for (const [name, kind, text] of csTypes(f)) { rows.push([path.basename(f), name, kind, text]); count.types++; if (!text) console.warn('no summary:', path.relative(API, f), name) }
    if (rows.length === 0) continue
    children.push(h3(folder === '' ? 'Project root' : folder + '/'), table(HEAD, rows, W))
  }
  return children
}

/** The text of a JSDoc block, without its @tags. */
function jsdocText(block) {
  const body = block.replace(/^\/\*\*/, '').replace(/\*\/$/, '').split('\n').map((l) => l.replace(/^\s*\* ?/, ''))
  const until = body.findIndex((l) => /^@/.test(l.trim()))
  const text = (until < 0 ? body : body.slice(0, until)).join(' ')
  return text.replace(/\{@link\s+([^}\s|]+)[^}]*\}/g, '`$1`').replace(/\s+/g, ' ').trim()
}

const TS_EXPORT = /^export\s+(?:default\s+)?(?:async\s+)?(function|const|class|type|interface)\s+(\w+)/

/** The exports of a TypeScript file, each with its JSDoc. */
function tsExports(file) {
  const src = read(file)
  const lines = src.split('\n')
  const out = []
  lines.forEach((line, i) => {
    const m = TS_EXPORT.exec(line)
    if (!m) return
    let text = ''
    let j = i - 1
    while (j >= 0 && lines[j].trim().startsWith('//')) j-- // lint directives between the comment and the export
    if (j >= 0 && lines[j].trim().endsWith('*/')) {
      let k = j
      while (k >= 0 && !lines[k].trim().startsWith('/**')) k--
      if (k >= 0) text = jsdocText(lines.slice(k, j + 1).join('\n'))
    }
    const [, decl, name] = m
    const kind = decl === 'type' || decl === 'interface' ? 'type'
      : decl === 'class' ? 'class'
      : /^use[A-Z]/.test(name) ? 'hook'
      : file.endsWith('.tsx') && /^[A-Z][a-z]/.test(name) && decl === 'function' ? 'component'
      : decl === 'function' ? 'function'
      : 'constant'
    out.push([name, kind, text])
  })
  return out
}

/** Rows for the web app: a heading per folder, a row per export. */
function webProject() {
  const children = []
  for (const [folder, files] of filesByFolder(path.join(WEB, 'src'), ['.ts', '.tsx'])) {
    if (folder === 'components/ui') {
      children.push(h3('src/components/ui/'), table(HEAD, [[
        `${files.length} files`, files.map((f) => path.basename(f).replace(/\.tsx?$/, '')).join(', '), 'components',
        'shadcn/ui primitives, generated by its CLI and styled by the design tokens in `index.css`. Not documented one by one.',
      ]], W))
      continue
    }
    const rows = []
    for (const f of files) {
      if (f.endsWith('.d.ts')) continue
      // Like the API's Constants classes, the constants module is one row; its header says what it holds.
      if (path.basename(f) === 'constants.ts') {
        const head = /^\s*\/\*\*([\s\S]*?)\*\//.exec(read(f))
        const names = tsExports(f).filter(([, kind]) => kind !== 'type').map(([n]) => '`' + n + '`')
        rows.push([path.basename(f), 'constants module', 'module', (head ? jsdocText(head[0]) + ' ' : '') + 'It exports ' + names.join(', ') + ', each with the type derived from it where it is an enum.'])
        count.types++
        continue
      }
      const exports = tsExports(f)
      if (exports.length === 0) {
        const head = /^\s*\/\*\*([\s\S]*?)\*\//.exec(read(f))
        rows.push([path.basename(f), '(no exports)', 'module', head ? jsdocText(head[0]) : 'Entry point; it has no exports.'])
      }
      for (const [name, kind, text] of exports) { rows.push([path.basename(f), name, kind, text]); count.types++; if (!text) console.warn('no jsdoc:', path.relative(WEB, f), name) }
    }
    if (rows.length) children.push(h3(folder === '' ? 'src/' : `src/${folder}/`), table(HEAD, rows, W))
  }
  return children
}

const count = { types: 0 }

const PROJECTS = [
  ['src/DesignApp.Domain', 'The core rules, with no dependencies, grouped by type: Entities/, Objects/ (value objects), Extensions/, Exceptions/.'],
  ['src/DesignApp.Application', 'Use cases and the interfaces they need, grouped by type: every port in Interfaces/, one class per use case in UseCases/ (split into Designs/ and Rooms/), the records in Objects/.'],
  ['src/DesignApp.Infrastructure', 'The adapters behind those interfaces: database, storage, imaging and the AI services.'],
  ['src/DesignApp.Api', 'HTTP endpoints, accounts, SignalR and the composition root, grouped by type: Endpoints/, Requests/, Responses/, Hubs/, Services/, Middleware/, Setup/.'],
  ['src/DesignApp.AppHost', 'Aspire orchestration for local development.'],
  ['src/DesignApp.ServiceDefaults', 'Telemetry, health checks and HTTP resilience shared by the services.'],
  ['tests/DesignApp.UnitTests', 'Fast tests of the domain, use cases, imaging and AI request handling, with fakes.'],
  ['tests/DesignApp.IntegrationTests', 'The real API, worker and SignalR against throwaway Postgres and RustFS containers.'],
]

const apiChildren = []
for (const [rel, about] of PROJECTS) {
  const section = csProject(path.join(API, rel))
  apiChildren.push(h2(path.basename(rel)), p(about), ...section)
}

// The hand-written closing section (patterns) is kept from the previous edition.
const patterns = fs.readFileSync(path.join(__dirname, 'patterns.txt'), 'utf8').replace(/\r\n/g, '\n').split('\n').filter(Boolean)
const intro = eval(patterns.find((l) => l.trim().startsWith("p('DesignApp is an AI")).trim().replace(/,$/, ''))
const patternBullets = patterns.filter((l) => l.trim().startsWith('bullet(')).map((l) => eval(l.trim().replace(/,$/, '')))

const children = [
  ...title('DesignApp: Class Reference', 'Every class and module of the API and the web app, project by project and folder by folder'),
  intro,
  p('This document follows the solution: one section per project in the order of the layers, one table per folder, and one row per class, record, interface or enum (for the web app, per exported component, hook, function, constant or type). "What it does" is the class\'s own documentation comment, so the document says exactly what the code says. Types nested inside another class, such as the groups and enums inside each `Constants` class, are described in that class\'s row.'),
  h1('API (DesignApp.Api)'),
  p('Dependencies point inward: Api → Infrastructure → Application → Domain, and the Domain depends on nothing.'),
  ...apiChildren,
  h1('Web (DesignApp.Web)'),
  p('A React single-page app organized by feature. The only real class is `RoomRenderer`; the rest are components, hooks, functions, constants and types.'),
  ...webProject(),
  h1('Patterns in one place'),
  ...patternBullets,
]

const doc = new Document({
  creator: 'DesignApp', title: 'DesignApp: Class Reference', styles, numbering,
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838, orientation: PageOrientation.LANDSCAPE }, margin: { top: 900, bottom: 900, left: 900, right: 900 } } },
    footers: footer('DesignApp: Class Reference'),
    children,
  }],
})
/** The document's bytes, and how many classes and exports it lists. */
module.exports = async () => ({ buffer: await Packer.toBuffer(doc), entries: count.types })
