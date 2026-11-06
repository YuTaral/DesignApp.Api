// Regenerates the two Word documents in the folder that holds both repositories:
// "DesignApp - Class Reference.docx" (from the code of both repos) and
// "DesignApp - Architecture Overview.docx" (hand-written in architecture.js).
// Usage, from DesignApp.Api/tools/docs: npm install (once), then npm run docs.
const fs = require('fs')
const path = require('path')

const OUT = path.resolve(__dirname, '..', '..', '..')
const DOCS = [
  ['DesignApp - Class Reference.docx', './classes'],
  ['DesignApp - Architecture Overview.docx', './architecture'],
]

async function main() {
  let failed = false
  for (const [name, module] of DOCS) {
    const { buffer, entries } = await require(module)()
    const file = path.join(OUT, name)
    try {
      fs.writeFileSync(file, buffer)
      console.log(`written ${file}${entries ? ` (${entries} entries)` : ''}`)
    } catch (error) {
      failed = true
      const locked = ['EBUSY', 'EPERM', 'EACCES'].includes(error.code)
      console.error(locked ? `${name} is open in Word: close it and run again.` : `${name}: ${error.message}`)
    }
  }
  process.exitCode = failed ? 1 : 0
}

main()
