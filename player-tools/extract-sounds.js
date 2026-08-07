// NeoScavenger SWF sound extractor (R42)
// Parses DefineSound (tag 14/11-MP3) + SymbolClass (tag 76) from the game SWF,
// extracts every embedded MP3 named by its linkage name (cue* names used by
// aSounds / strSnd), writes <outDir>/<name>.mp3 + <outDir>/index.json.
//
// Usage: node extract-sounds.js <game.swf> <outDir>
// (node built-in zlib handles CWS compression; no external deps)

const fs = require('fs');
const zlib = require('zlib');

const swfPath = process.argv[2];
const outDir = process.argv[3];
if (!swfPath || !outDir) {
  console.error('usage: node extract-sounds.js <game.swf> <outDir>');
  process.exit(1);
}

const data = fs.readFileSync(swfPath);
if (data.length < 8) { console.error('not a swf'); process.exit(1); }
const sig = data.toString('ascii', 0, 3);
let raw;
if (sig === 'FWS') raw = data.subarray(8);
else if (sig === 'CWS') {
  // CWS: zlib payload decompresses to the tag stream — but some builds carry a
  // small prefix before it, so detect the real stream start below.
  raw = zlib.inflateSync(data.subarray(8));
} else { console.error('unsupported swf signature', sig); process.exit(1); }

// ── detect the tag-stream start: first offset where N consecutive tags parse ──
let start = -1;
for (let off = 0; off < 64 && start < 0; off++) {
  let pos = off, ok = true;
  for (let i = 0; i < 6 && ok; i++) {
    if (pos + 2 > raw.length) { ok = false; break; }
    const hdr = raw.readUInt16LE(pos);
    const code = hdr >> 6;
    let len = hdr & 0x3f;
    let p2 = pos + 2;
    if (len === 0x3f) {
      if (p2 + 4 > raw.length) { ok = false; break; }
      len = raw.readUInt32LE(p2);
      p2 += 4;
    }
    if (code > 100 || pos + len > raw.length) { ok = false; break; }
    pos = p2 + len;
  }
  if (ok) start = off;
}
if (start < 0) { console.error('could not locate tag stream'); process.exit(1); }
const body = raw.subarray(start);

// ── tag stream walk ──────────────────────────────────────────────
const sounds = new Map();       // soundId -> Buffer (mp3 payload)
const streamSounds = [];        // SoundStreamBlock audio (bgm) — count only
const symbolClass = new Map();  // charId -> export name

let pos = 0;
while (pos + 2 <= body.length) {
  const hdr = body.readUInt16LE(pos);
  let code = hdr >> 6;
  let len = hdr & 0x3f;
  pos += 2;
  if (len === 0x3f) {
    if (pos + 4 > body.length) break;
    len = body.readUInt32LE(pos);
    pos += 4;
  }
  if (pos + len > body.length) break;
  const tagData = body.subarray(pos, pos + len);
  pos += len;

  if (code === 14) {
    // DefineSound: SoundId(2) + packed byte [SoundFormat:4|Rate:2|Size:1|Type:1]
    //              + SampleCount(4) [+ SeekSamples(2) if MP3] then sound data
    // (verified: format byte 0x2F = MP3/44k/16bit/stereo, MP3 data starts at 9)
    if (tagData.length < 8) continue;
    const soundId = tagData.readUInt16LE(0);
    const format = tagData.readUInt8(2) >> 4;
    let payloadStart;
    if (format === 2 || format === 11) {
      payloadStart = 2 + 1 + 4 + 2; // MP3: +SeekSamples
    } else {
      payloadStart = 2 + 1 + 4;
    }
    if (payloadStart < tagData.length) {
      sounds.set(soundId, Buffer.from(tagData.subarray(payloadStart)));
    }
  } else if (code === 19) {
    streamSounds.push(tagData.length);
  } else if (code === 76) {
    // SymbolClass: count(2) then [TagID(2) Name(NULL-terminated C string)]*
    // (verified against the game SWF — NOT a length-prefixed string!)
    if (tagData.length < 2) continue;
    const count = tagData.readUInt16LE(0);
    let p = 2;
    for (let i = 0; i < count && p + 2 <= tagData.length; i++) {
      const charId = tagData.readUInt16LE(p);
      p += 2;
      const end = tagData.indexOf(0, p);
      if (end < 0 || end >= tagData.length) break;
      const name = tagData.toString('utf8', p, end);
      p = end + 1;
      symbolClass.set(charId, name);
    }
  }
  // tag 0 = End
  if (code === 0) break;
}

// ── write out ─────────────────────────────────────────────────────
fs.mkdirSync(outDir, { recursive: true });
const index = [];
let written = 0;
for (const [soundId, mp3] of sounds) {
  const name = symbolClass.get(soundId);
  const fileName = name && name.length > 0 ? name : `sound_${soundId}`;
  const safe = fileName.replace(/[^A-Za-z0-9._-]/g, '_');
  const file = `${safe}.mp3`;
  fs.writeFileSync(`${outDir}/${file}`, mp3);
  index.push({ id: soundId, name: fileName, file, bytes: mp3.length });
  written++;
}

index.sort((a, b) => a.name.localeCompare(b.name));
fs.writeFileSync(`${outDir}/index.json`, JSON.stringify(index, null, 1));

console.log(`sounds: ${written} embedded (+${streamSounds.length} stream blocks) | symbolClass: ${symbolClass.size}`);
const cues = index.filter(e => e.name.startsWith('cue')).slice(0, 12);
console.log('cue samples:', cues.map(c => c.name).join(', '));
