// R31: replace the 3-line raw-data pattern in all visualizers with BuildRawData(entity).
// Line-based approach — immune to backslash-escaping mistakes of big regexes.
const fs = require('fs');
const path = require('path');

const dir = 'D:/RiderProjects/NeoEditor/NeoEditor.Plugins.EntityEditor/Visualizers';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.cs'));

let changed = 0;
for (const f of files) {
  const fp = path.join(dir, f);
  const lines = fs.readFileSync(fp, 'utf8').split('\n');
  const out = [];
  let varName = null;
  let replaced = false;
  let dropNext = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const t = line.trim();

    // drop the AttackMode indirection line: `var rawContent = _vis.BuildRawDataTable(am);`
    if (t.startsWith('var rawContent = _vis.BuildRawDataTable')) {
      continue;
    }

    // locate `var rawBody = new Border [...]`
    if (varName === null && !replaced && t.startsWith('var rawBody = new Border')) {
      const isSingleLine = t.includes('};');
      const probe = isSingleLine ? line : line + ' ' + lines[i + 1];
      const m = probe.match(/BuildRawDataTable\((\w+)\)/);
      if (m) {
        varName = m[1];
        if (isSingleLine) {
          continue; // drop this line
        }
        dropNext = true; // drop the continuation line
        continue;
      }
    }
    if (dropNext) {
      dropNext = false;
      continue;
    }

    // the BuildExpander + Add(rawBody) pair → single BuildRawData call
    if (varName !== null && !replaced && t.startsWith('root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody))')) {
      out.push(line.replace(/^(\s*).*$/, `$1root.Children.Add(_vis.BuildRawData(${varName}));`));
      replaced = true;
      continue;
    }
    if (varName !== null && replaced && t.startsWith('root.Children.Add(rawBody)')) {
      continue; // drop
    }

    out.push(line);
  }

  const res = out.join('\n');
  if (res !== fs.readFileSync(fp, 'utf8')) {
    fs.writeFileSync(fp, res);
    changed++;
    const leftover = (res.match(/rawBody|rawContent/g) || []).length;
    console.log(`${f}: replaced -> BuildRawData(${varName}) | leftover refs: ${leftover}`);
  } else {
    console.log(`${f}: NO CHANGE`);
  }
}
console.log(`\n${changed}/${files.length} files changed`);
