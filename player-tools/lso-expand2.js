// LSO 展开器 v2.45 —— 两遍法（写端表语义精确重建）
// 第一遍：完整解析构建树（引用=占位），按外层先顺序记录复杂值+引用位置
// 第二遍：按外层先顺序值相等去重建表 → 引用号 → 目标 → 展开
// 写端语义（flash-lso write.rs 确认）：
//   - store 时机：值开始写时（外层先，write_object_element 的 store 在写内容前）
//   - store 去重：值相等（PartialEq）不重复入表
//   - 入表类型：Object / VectorObject / Dictionary / ECMAArray / StrictArray（VectorInt/UInt/Double/Date/XML/ByteArray 不入表）
const fs = require('fs');

function readU29(b, i) {
  let v = b[i] & 0x7f, n = 1;
  if (!(b[i] & 0x80)) return { v, n };
  v = (v << 7) | (b[i + 1] & 0x7f); n = 2;
  if (!(b[i + 1] & 0x80)) return { v, n };
  v = (v << 7) | (b[i + 2] & 0x7f); n = 3;
  if (!(b[i + 2] & 0x80)) return { v, n };
  v = (v << 8) | b[i + 3]; n = 4;
  return { v, n };
}
function writeU29(out, v) {
  if (v > 0x1fffff) {
    out.push(((v >>> 22) | 0x80) & 0xff, ((v >>> 15) | 0x80) & 0xff, ((v >>> 8) | 0x80) & 0xff, v & 0xff);
  } else if (v > 0x3fff) {
    out.push(((v >>> 14) | 0x80) & 0xff, ((v >>> 7) | 0x80) & 0xff, v & 0x7f);
  } else if (v > 0x7f) {
    out.push(((v >>> 7) | 0x80) & 0xff, v & 0x7f);
  } else {
    out.push(v);
  }
}

// 第一遍解析器：构建树，复杂值按外层先顺序记录到 ordered[]，引用记 refRecs[]（位置+号）
class P1 {
  constructor(b) {
    this.b = b;
    this.strTable = [];
    this.traitTable = [];
    this.ordered = [];     // 外层先顺序的复杂值（写端 store 顺序）
    this.refRecs = [];     // 引用记录 {pos, idx, t}
    this.propTypes = {};   // 属性名 -> AMF 类型（从完整写值推断期望类型）
  }
  parseString(i) {
    const { v, n } = readU29(this.b, i);
    if ((v & 1) === 0) {
      const idx = v >> 1;
      return { str: this.strTable[idx] ?? '', n, isRef: true };
    }
    const len = v >> 1;
    if (len === 0) return { str: '', n, isRef: false };
    const str = this.b.toString('utf8', i + n, i + n + len);
    this.strTable.push(str);
    return { str, n: n + len, isRef: false };
  }
  // 值解析：引用记到 refRecs（不展开），复杂值记到 ordered
  parseValue(i) {
    const b = this.b;
    const t = b[i];
    this._ctx = this._ctx || null;
    switch (t) {
      case 0x00: return { v: 'undefined', n: 1 };
      case 0x01: return { v: null, n: 1 };
      case 0x02: return { v: false, n: 1 };
      case 0x03: return { v: true, n: 1 };
      case 0x04: {
        const { v, n } = readU29(b, i + 1);
        return { v: { __i: (v & 0x10000000) ? v - 0x20000000 : v }, n: 1 + n };
      }
      case 0x05: return { v: { __n: b.readDoubleBE(i + 1) }, n: 9 };
      case 0x06: { const s = this.parseString(i + 1); return { v: s.str, n: 1 + s.n }; }
      case 0x08: {   // Date —— 写端不入表
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        return { v: { __amf: 'date', ms: b.readDoubleBE(i + 1 + n) }, n: 1 + n + 8 };
      }
      case 0x09: case 0x0a: case 0x10: case 0x11: {   // Array/Object/VO/Dict —— 写端入表
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const rec = { __amf: t === 0x09 ? 'array' : t === 0x0a ? 'object' : t === 0x10 ? 'vecobject' : 'dict' };
        this.ordered.push(rec);   // 占位式：先记顺序
        const len = v >> 1;
        let j = i + 1 + n;
        if (t === 0x09) {   // ECMAArray
          rec.dense = [];
          rec.assoc = {};
          for (let e = 0; e < len; e++) {
            const val = this.parseValue(j); j += val.n;
            rec.dense.push(val.v);
          }
          for (;;) {
            const s = this.parseString(j); j += s.n;
            if (s.str === '') break;
            const val = this.parseValue(j); j += val.n;
            rec.assoc[s.str] = val.v;
          }
        } else if (t === 0x0a) {   // Object
          let traits = v >> 1;
          let encoding, names, className;
          if ((traits & 1) === 0) {
            const def = this.traitTable[traits >> 1];
            if (!def) throw new Error('traits 引用越界 #' + (traits >> 1) + ' @' + i);
            encoding = def.encoding; names = def.names; className = def.className;
          } else {
            traits >>= 1;
            encoding = traits & 0x03;
            const count = traits >> 2;
            const cn = this.parseString(j); j += cn.n;
            className = cn.str;
            names = [];
            for (let m = 0; m < count; m++) { const s = this.parseString(j); j += s.n; names.push(s.str); }
            this.traitTable.push({ encoding, names, className });
          }
          rec.className = className;
          rec.names = names;
          rec.values = [];
          rec.dynamic = [];
          rec.isDynamic = (encoding & 0b10) === 0b10;
          for (let m = 0; m < names.length; m++) {
            const val = this.parseValue(j); j += val.n;
            rec.values.push(val.v);
            this.propTypes[names[m]] = val.v && val.v.__amf ? val.v.__amf : (val.v && val.v.__i !== undefined ? "int" : (val.v && val.v.__n !== undefined ? "number" : (val.v && val.v.__ref !== undefined ? "ref:" + val.v.__ref : "other")));
          }
          if ((encoding & 0b10) === 0b10) {
            for (;;) {
              const s = this.parseString(j); j += s.n;
              if (s.str === '') break;
              const val = this.parseValue(j); j += val.n;
              rec.dynamic.push({ name: s.str, value: val.v });
            }
          }
        } else if (t === 0x10) {   // VectorObject
          rec.fixed = b[j++] === 1;
          const cn = this.parseString(j); j += cn.n;
          rec.className = cn.str ?? '';
          rec.values = [];
          for (let e = 0; e < len; e++) {
            const val = this.parseValue(j); j += val.n;
            rec.values.push(val.v);
          }
        } else {   // Dictionary
          rec.weak = b[j++] === 1;
          rec.entries = [];
          for (let e = 0; e < len; e++) {
            const k = this.parseValue(j); j += k.n;
            const vv = this.parseValue(j); j += vv.n;
            rec.entries.push([k.v, vv.v]);
          }
        }
        return { v: rec, n: j - i };
      }
      case 0x0b: {   // XML —— 写端不入表
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const len = v >> 1;
        return { v: { __amf: 'xml', s: this.b.toString('utf8', i + 1 + n, i + 1 + n + len) }, n: 1 + n + len };
      }
      case 0x0c: {   // ByteArray —— 写端不入表
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const len = v >> 1;
        return { v: { __amf: 'bytes', b: Array.from(b.subarray(i + 1 + n, i + 1 + n + len)) }, n: 1 + n + len };
      }
      case 0x0d: {   // VectorInt —— 写端不入表
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const len = v >> 1;
        let j = i + 1 + n;
        const fixed = b[j++];
        const values = [];
        for (let e = 0; e < len; e++) { values.push(b.readInt32BE(j)); j += 4; }
        return { v: { __amf: 'vecint', fixed: fixed === 1, values }, n: j - i };
      }
      case 0x0e: {   // VectorUInt
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const len = v >> 1;
        let j = i + 1 + n;
        const fixed = b[j++];
        const values = [];
        for (let e = 0; e < len; e++) { values.push(b.readUInt32BE(j)); j += 4; }
        return { v: { __amf: 'vecuint', fixed: fixed === 1, values }, n: j - i };
      }
      case 0x0f: {   // VectorDouble
        const { v, n } = readU29(b, i + 1);
        if ((v & 1) === 0) { this.refRecs.push({ pos: i, idx: v >> 1, t }); return { v: { __ref: v >> 1, __rt: t }, n: 1 + n }; }
        const len = v >> 1;
        let j = i + 1 + n;
        const fixed = b[j++];
        const values = [];
        for (let e = 0; e < len; e++) { values.push(b.readDoubleBE(j)); j += 8; }
        return { v: { __amf: 'vecdouble', fixed: fixed === 1, values }, n: j - i };
      }
      default: throw new Error('未知 AMF3 标记 0x' + t.toString(16) + ' @' + i);
    }
  }
}

// 第二遍：去重建表
function buildTable(ordered) {
  const table = [];
  const dedup = new Map();
  for (const rec of ordered) {
    const key = JSON.stringify(rec);
    if (process.env.DUMP_DEDUP && dedup.has(key)) {
      let d = rec.__amf;
      if (rec.__amf === "object") d += "[" + (rec.names || []).join(",") + "]" + (rec.isDynamic ? " dyn" : "");
      if (rec.__amf === "vecobject") d += " len=" + rec.values.length + (rec.values[0] && rec.values[0].__amf === "object" ? " el0=" + (rec.values[0].names || []).slice(0,2).join(",") : "");
      if (rec.__amf === "dict") d += " e=" + rec.entries.length;
      console.log("  去重命中: " + d + " -> 表[" + dedup.get(key) + "]");
    }
    if (!dedup.has(key)) {
      dedup.set(key, table.length);
      table.push(rec);
    }
  }
  return table;
}

// 展开：树中所有 {__ref} 替换为 table[idx] 的深拷贝（递归）
function expandValue(v, table, depth, inflight, propName, propTypes) {
  if (v === null || typeof v !== 'object') return v;
  if (v.__i !== undefined || v.__n !== undefined) return v;
  if (v.__ref !== undefined) {
    if (depth > 64) return 'undefined';
    if (inflight.has(v.__ref)) return 'undefined';
    let target = table[v.__ref];
    if (target === undefined) return 'undefined';
    // 类型匹配检查：引用标记 0x10(VO)/0x11(Dict) vs 表项类型
    if (v.__rt === 0x10 && target.__amf !== 'vecobject') {
      // VO 引用指向非 VO：错位 → 找'包含该目标的对象'（值相等回退）
      const fallback = table.find(t => t.__amf === 'vecobject' && t.values.some(x => x === target));
      if (fallback) { if (process.env.DEBUG) console.log("REF#" + v.__ref + " 类型回退: " + target.__amf + " -> vecobject"); target = fallback; }
    } else if (v.__rt === 0x11 && target.__amf !== 'dict') {
      const fallback = table.find(t => t.__amf === 'dict' && t.entries.some(([k, x]) => x === target || k === target));
      if (fallback) { if (process.env.DEBUG) console.log("REF#" + v.__ref + " 类型回退: " + target.__amf + " -> dict"); target = fallback; }
    } else if (v.__rt === 0x0a && target.__amf !== 'object') {
      const fallback = table.find(t => t.__amf === 'object' && (t.values.some(x => x === target) || t.dynamic.some(d => d.value === target)));
      if (fallback) { if (process.env.DEBUG) console.log("REF#" + v.__ref + " 类型回退: " + target.__amf + " -> object"); target = fallback; }
    }
    // 期望类型修正：Vector.<int> 属性被去重成空 vecobject → 还原为空 vecint
    // （游戏字段知识：这些属性在 SaveGameData 中类型强制为 Vector.<int>）
    var VECINT_PROPS = ["m_vWaypoints", "m_vEncQueue", "m_vFactions", "m_vEncounterTriggersRemaining", "m_vEventQueue", "m_vKnownRecipes", "m_vSearchStates", "m_vUsedRecipes"];
    if (target && target.__amf === 'vecobject' && target.values.length === 0 && VECINT_PROPS.indexOf(propName) !== -1) {
      target = { __amf: 'vecint', fixed: false, values: [] };
    }
    inflight.add(v.__ref);
    try { return expandValue(target, table, depth + 1, inflight, propName, propTypes); }
    finally { inflight.delete(v.__ref); }
  }
  if (v.__amf === 'object') {
    return {
      __amf: 'object', className: v.className, names: v.names, isDynamic: v.isDynamic,
      values: v.values.map((x, i) => expandValue(x, table, depth, inflight, v.names[i], propTypes)),
      dynamic: v.dynamic.map(d => ({ name: d.name, value: expandValue(d.value, table, depth, inflight, d.name, propTypes) })),
    };
  }
  if (v.__amf === 'vecobject') {
    return {
      __amf: 'vecobject', fixed: v.fixed, className: v.className,
      values: v.values.map(x => expandValue(x, table, depth, inflight, propName, propTypes)),
    };
  }
  if (v.__amf === 'dict') {
    return {
      __amf: 'dict', weak: v.weak,
      entries: v.entries.map(([k, x]) => [expandValue(k, table, depth, inflight), expandValue(x, table, depth, inflight, propName, propTypes)]),
    };
  }
  if (v.__amf === 'array') {
    return {
      __amf: 'array', dense: v.dense.map(x => expandValue(x, table, depth, inflight, propName, propTypes)),
      assoc: Object.fromEntries(Object.entries(v.assoc).map(([k, x]) => [k, expandValue(x, table, depth, inflight, propName, propTypes)])),
    };
  }
  return v;
}

// ── v2.48 存档形态归一化 ────────────────────────────────────────
// 游戏运行期缺陷：Creature.SaveData getter 把 m_dictFactions 的每个值
// push 进 Vector.<int> m_vFactions；若活体 m_dictFactions 在运行期混入
// 非数字值（游戏自己的 faction 逻辑在空字典上运算产生的异常值），
// 保存时崩溃（cannot convert false to Vector.<int>）。
// 玩家存档的 m_vFactions 为空是异常形态（生物均为 14×-100 默认声望）：
// 补全为与生物一致的 14×-100，使运行期 faction 运算作用在已存在的数字条目上。
function normalizeFactions(v) {
  if (!v || typeof v !== 'object' || v.__amf !== 'object') return;
  const names = v.names || [];
  const i = names.indexOf('m_vFactions');
  const dyn = v.dynamic || [];
  const dynIdx = dyn.findIndex(d => d.name === 'm_vFactions');
  const mvf = i >= 0 ? v.values[i] : dynIdx >= 0 ? dyn[dynIdx].value : null;
  if (mvf && mvf.__amf === 'vecint' && mvf.values.length === 0) {
    const filled = { __amf: 'vecint', fixed: false, values: [-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100] };
    if (i >= 0) v.values[i] = filled;
    else dyn[dynIdx].value = filled;
  }
}

// ── 编码（全内联）────────────────────────────────────────────
function encString(out, s) {
  const bytes = Buffer.from(s, 'utf8');
  writeU29(out, (bytes.length << 1) | 1);
  for (const x of bytes) out.push(x);
}
function encValue(out, v) {
  if (v === 'undefined') { out.push(0x00); return; }
  if (v === null) { out.push(0x01); return; }
  if (v === false) { out.push(0x02); return; }
  if (v === true) { out.push(0x03); return; }
  if (typeof v === 'number') {
    if (Number.isInteger(v) && v >= -268435456 && v <= 268435455) {
      out.push(0x04); writeU29(out, v & 0x1fffffff);
    } else { out.push(0x05); const b = Buffer.alloc(8); b.writeDoubleBE(v); for (const x of b) out.push(x); }
    return;
  }
  if (v && v.__i !== undefined) { out.push(0x04); writeU29(out, v.__i & 0x1fffffff); return; }
  if (v && v.__n !== undefined) { out.push(0x05); const b = Buffer.alloc(8); b.writeDoubleBE(v.__n); for (const x of b) out.push(x); return; }
  if (typeof v === 'string') { out.push(0x06); encString(out, v); return; }
  if (v.__amf === 'date') { out.push(0x08); writeU29(out, 1); const b = Buffer.alloc(8); b.writeDoubleBE(v.ms); for (const x of b) out.push(x); return; }
  if (v.__amf === 'xml') { out.push(0x0b); writeU29(out, (v.s.length << 1) | 1); out.push(...Buffer.from(v.s, 'utf8')); return; }
  if (v.__amf === 'bytes') { out.push(0x0c); writeU29(out, (v.b.length << 1) | 1); out.push(...v.b); return; }
  if (v.__amf === 'vecint') {
    out.push(0x0d); writeU29(out, (v.values.length << 1) | 1); out.push(v.fixed ? 1 : 0);
    const b = Buffer.alloc(4);
    for (const x of v.values) { b.writeInt32BE(x); out.push(b[0], b[1], b[2], b[3]); }
    return;
  }
  if (v.__amf === 'vecuint') {
    out.push(0x0e); writeU29(out, (v.values.length << 1) | 1); out.push(v.fixed ? 1 : 0);
    const b = Buffer.alloc(4);
    for (const x of v.values) { b.writeUInt32BE(x); out.push(b[0], b[1], b[2], b[3]); }
    return;
  }
  if (v.__amf === 'vecdouble') {
    out.push(0x0f); writeU29(out, (v.values.length << 1) | 1); out.push(v.fixed ? 1 : 0);
    const b = Buffer.alloc(8);
    for (const x of v.values) { b.writeDoubleBE(x); out.push(...b); }
    return;
  }
  if (v.__amf === 'vecobject') {
    out.push(0x10); writeU29(out, (v.values.length << 1) | 1); out.push(v.fixed ? 1 : 0);
    encString(out, v.className ?? '');
    for (const x of v.values) encValue(out, x);
    return;
  }
  if (v.__amf === 'dict') {
    out.push(0x11); writeU29(out, (v.entries.length << 1) | 1); out.push(v.weak ? 1 : 0);
    for (const [k, x] of v.entries) { encValue(out, k); encValue(out, x); }
    return;
  }
  if (v.__amf === 'array') {
    const dense = v.dense ?? [];
    out.push(0x09); writeU29(out, (dense.length << 1) | 1);
    for (const x of dense) encValue(out, x);
    for (const [k, x] of Object.entries(v.assoc ?? {})) { encString(out, k); encValue(out, x); }
    encString(out, '');
    return;
  }
  if (v.__amf === 'object') {
    out.push(0x0a);
    const dynamic = v.dynamic ?? [];
    const isDyn = v.isDynamic || dynamic.length > 0;
    const enc = (isDyn ? 0b10 : 0) | 0b00;
    writeU29(out, ((v.names.length << 4) | (enc << 2) | 3));
    encString(out, v.className ?? '');
    for (const n of v.names) encString(out, n);
    for (const x of v.values) encValue(out, x);
    if (isDyn) {
      for (const d of dynamic) { encString(out, d.name); encValue(out, d.value); }
      encString(out, '');
    }
    return;
  }
  throw new Error('无法编码的值: ' + JSON.stringify(v).slice(0, 80));
}

function parseLso(base64) {
  const buf = Buffer.from(base64, 'base64');
  let h = 0;
  if (buf[h] !== 0x00 || buf[h + 1] !== 0xbf) throw new Error('LSO 头版本不符');
  h += 2;
  const lsoLen = buf.readUInt32BE(h); h += 4;
  const sig = buf.toString('latin1', h, h + 10); h += 10;
  if (!sig.startsWith('TCSO')) throw new Error('LSO 签名不符');
  const nameLen = buf.readUInt16BE(h); h += 2;
  const name = buf.toString('utf8', h, h + nameLen); h += nameLen;
  h += 3;
  const formatVersion = buf[h++];
  const p1 = new P1(buf);
  const body = [];
  while (h < buf.length) {
    if (buf[h] === 0) { h++; continue; }
    const s = p1.parseString(h);
    h += s.n;
    if (s.str === '') { h += 1; continue; }
    const val = p1.parseValue(h);
    h += val.n;
    body.push({ name: s.str, value: val.v });
  }
  const table = buildTable(p1.ordered);
  if (process.env.DEBUG) {
    console.log('外层先记录:', p1.ordered.length, '| 去重后表:', table.length, '| 引用:', p1.refRecs.length);
    if (process.env.DUMP_ORDERED) { p1.ordered.forEach((r, ri) => { let d = r.__amf; if (r.__amf === "object") d += "[" + (r.names || []).join(",") + "]" + (r.isDynamic ? " dyn" : ""); if (r.__amf === "vecobject") d += " len=" + r.values.length + (r.values[0] && r.values[0].__amf === "object" ? " el0=" + (r.values[0].names || []).slice(0,2).join(",") : ""); if (r.__amf === "dict") d += " e=" + r.entries.length; console.log("  ord[" + ri + "] " + d); }); }
    const byIdx = {};
    for (const r of p1.refRecs) byIdx[r.idx] = (byIdx[r.idx] || 0) + 1;
    Object.entries(byIdx).forEach(([k, c]) => {
      const target = table[k];
      let desc = target ? target.__amf : '越界';
      if (target && target.__amf === 'vecobject') desc += ' len=' + target.values.length;
      if (target && target.__amf === 'object') desc += ' [' + (target.names || []).slice(0, 3).join(',') + ']';
      console.log('  REF#' + k + ' x' + c + ' -> ' + desc);
    });
  }
  return { name, formatVersion, body, table };
}

function encodeLso(lso) {
  const out = [];
  out.push(0x00, 0xbf);
  const nameBytes = Buffer.from(lso.name, 'utf8');
  const bodyOut = [];
  for (const b of lso.body) {
    encString(bodyOut, b.name);
    encValue(bodyOut, b.value);
    bodyOut.push(0x00);
  }
  const bodyLen = 10 + 2 + nameBytes.length + 3 + 1 + bodyOut.length;
  const lenBuf = Buffer.alloc(4); lenBuf.writeUInt32BE(bodyLen);
  out.push(...lenBuf);
  out.push(...Buffer.from('TCSO\0\4\0\0\0\0', 'latin1'));
  const nb = Buffer.alloc(2); nb.writeUInt16BE(nameBytes.length);
  out.push(...nb, ...nameBytes);
  out.push(0x00, 0x00, 0x00, 0x03);
  out.push(...bodyOut);
  return Buffer.from(out).toString('base64');
}

// 主流程
const input = process.argv[2];
const output = process.argv[3];
if (!input) { console.error('用法: node lso-expand2.js <in.json> [out.json]'); process.exit(1); }
const data = JSON.parse(fs.readFileSync(input, 'utf8'));
const raw = data.Value || data.value;
const parsed = parseLso(raw);
console.log('LSO 名:', parsed.name, '| 根元素:', parsed.body.length);
// 展开引用
for (const b of parsed.body) {
  b.value = expandValue(b.value, parsed.table, 0, new Set());
}
// v2.48: 形态归一化——玩家/生物的 m_vFactions 为空 → 补全 14×-100 默认声望
// （防游戏运行期 faction 逻辑在空字典上运算产生异常值 → 保存崩溃）
(function () {
  function getProp(v, name) {
    if (!v || typeof v !== 'object') return undefined;
    if (v.__amf === 'object') {
      const i = (v.names || []).indexOf(name);
      if (i >= 0) return v.values[i];
      const d = (v.dynamic || []).find(x => x.name === name);
      if (d) return d.value;
    }
    return undefined;
  }
  for (const b of parsed.body) {
    const root = b.value;
    normalizeFactions(getProp(root, 'm_objPlayer'));
    const vc = getProp(root, 'm_vCreatures');
    if (vc && vc.__amf === 'vecobject') vc.values.forEach(c => normalizeFactions(c));
  }
})();
const expanded = encodeLso(parsed);
if (output) {
  fs.writeFileSync(output, JSON.stringify({ ...data, Value: expanded }, null, 2));
  console.log('写出:', output, '长度:', expanded.length);
}
try {
  const reparse = parseLso(expanded);
  console.log('展开后重新解析: OK | 长度:', raw.length, '→', expanded.length);
} catch (e) {
  console.log('展开后重新解析失败:', e.message);
}
