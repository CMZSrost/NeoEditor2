/* lso-expand-web.js —— 浏览器版 LSO 存档展开器（v2.49）
 *
 * 背景：Ruffle nightly 反序列化 AMF3 引用（Amf3ObjectReference）有 bug，
 * 受伤存档重启必崩（"m_fDate not found on Number"，Ruffle #1069）。
 * 方案：SWF 加载前把 localStorage 里的 LSO 存档解析 → 展开全部引用 →
 * 重新编码为完全内联（无引用）的字节流，Ruffle 读取时就无引用可崩。
 *
 * v2.49（存档修改工具）：新增 LsoExpand.toTree(b64) / fromTree(jsonText)——
 * LSO ↔ JSON 树双向转换，宿主「存档修改工具」用（加载显示/编辑/保存回写）。
 *
 * v2.45 两遍法（精确写端表语义）：
 *   - 第一遍：完整解析构建树（引用=占位），按外层先顺序记录复杂值
 *   - 第二遍：值相等去重建表（flash-lso write.rs 的 store 语义：
 *     Object/VectorObject/Dictionary/ECMAArray 入表，值相等不重复；
 *     VectorInt/UInt/Double/Date/XML/ByteArray 不入表）
 *   - 类型不匹配回退：引用标记与表项类型不符（读写端表错位）时，
 *     回退到包含该表项的容器对象（修复受伤档的 vCurrentStates 错位）
 * 用法：LsoExpand.expand(base64String) → base64String
 */
(function (global) {
    "use strict";

    var te = new TextEncoder();
    var td = new TextDecoder("utf-8");

    function bytesFromB64(b64) {
        var bin = atob(b64);
        var out = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
        return out;
    }
    function b64FromBytes(bytes) {
        var bin = "";
        for (var i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
        return btoa(bin);
    }
    function readU29(b, i) {
        var v = b[i] & 0x7f, n = 1;
        if (!(b[i] & 0x80)) return { v: v, n: n };
        v = (v << 7) | (b[i + 1] & 0x7f); n = 2;
        if (!(b[i + 1] & 0x80)) return { v: v, n: n };
        v = (v << 7) | (b[i + 2] & 0x7f); n = 3;
        if (!(b[i + 2] & 0x80)) return { v: v, n: n };
        v = (v << 8) | b[i + 3]; n = 4;
        return { v: v, n: n };
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

    // 第一遍解析器：构建树，复杂值按外层先顺序记录到 ordered[]，引用记 refRecs[]
    function P1(b) {
        this.b = b;
        this.strTable = [];
        this.traitTable = [];
        this.ordered = [];
        this.refRecs = [];
    }
    P1.prototype.parseString = function (i) {
        var b = this.b;
        var r = readU29(b, i);
        if ((r.v & 1) === 0) {
            return { str: this.strTable[r.v >> 1] || "", n: r.n, ref: true };
        }
        var len = r.v >> 1;
        if (len === 0) return { str: "", n: r.n, ref: false };
        var str = td.decode(b.subarray(i + r.n, i + r.n + len));
        this.strTable.push(str);
        return { str: str, n: r.n + len, ref: false };
    };
    P1.prototype.parseValue = function (i) {
        var b = this.b;
        var t = b[i];
        switch (t) {
            case 0x00: return { v: "undefined", n: 1 };
            case 0x01: return { v: null, n: 1 };
            case 0x02: return { v: false, n: 1 };
            case 0x03: return { v: true, n: 1 };
            case 0x04: {
                var r4 = readU29(b, i + 1);
                return { v: { __i: (r4.v & 0x10000000) ? r4.v - 0x20000000 : r4.v }, n: 1 + r4.n };
            }
            case 0x05: {
                var dv5 = new DataView(b.buffer, b.byteOffset + i + 1, 8);
                return { v: { __n: dv5.getFloat64(0, false) }, n: 9 };
            }
            case 0x06: { var s6 = this.parseString(i + 1); return { v: s6.str, n: 1 + s6.n }; }
            case 0x08: {
                var r8 = readU29(b, i + 1);
                if ((r8.v & 1) === 0) { this.refRecs.push({ pos: i, idx: r8.v >> 1, t: t }); return { v: { __ref: r8.v >> 1, __rt: t }, n: 1 + r8.n }; }
                var dv8 = new DataView(b.buffer, b.byteOffset + i + 1 + r8.n, 8);
                return { v: { __amf: "date", ms: dv8.getFloat64(0, false) }, n: 1 + r8.n + 8 };
            }
            case 0x09: case 0x0a: case 0x10: case 0x11: {
                var r = readU29(b, i + 1);
                if ((r.v & 1) === 0) { this.refRecs.push({ pos: i, idx: r.v >> 1, t: t }); return { v: { __ref: r.v >> 1, __rt: t }, n: 1 + r.n }; }
                var rec = { __amf: t === 0x09 ? "array" : t === 0x0a ? "object" : t === 0x10 ? "vecobject" : "dict" };
                this.ordered.push(rec);
                var len = r.v >> 1;
                var j = i + 1 + r.n;
                if (t === 0x09) {
                    rec.dense = [];
                    rec.assoc = {};
                    for (var e9 = 0; e9 < len; e9++) { var v9 = this.parseValue(j); j += v9.n; rec.dense.push(v9.v); }
                    for (;;) {
                        var s9 = this.parseString(j); j += s9.n;
                        if (s9.str === "") break;
                        var vv9 = this.parseValue(j); j += vv9.n;
                        rec.assoc[s9.str] = vv9.v;
                    }
                } else if (t === 0x0a) {
                    var traits = r.v >> 1;
                    var encoding, names, className;
                    if ((traits & 1) === 0) {
                        var def = this.traitTable[traits >> 1];
                        if (!def) throw new Error("traits 引用越界 #" + (traits >> 1));
                        encoding = def.encoding; names = def.names; className = def.className;
                    } else {
                        traits >>= 1;
                        encoding = traits & 0x03;
                        var count = traits >> 2;
                        var cn = this.parseString(j); j += cn.n;
                        className = cn.str;
                        names = [];
                        for (var m = 0; m < count; m++) { var sn = this.parseString(j); j += sn.n; names.push(sn.str); }
                        this.traitTable.push({ encoding: encoding, names: names, className: className });
                    }
                    rec.className = className;
                    rec.names = names;
                    rec.values = [];
                    rec.dynamic = [];
                    rec.isDynamic = (encoding & 0b10) === 0b10;
                    for (var m2 = 0; m2 < names.length; m2++) {
                        var vv = this.parseValue(j); j += vv.n;
                        rec.values.push(vv.v);
                    }
                    if ((encoding & 0b10) === 0b10) {
                        for (;;) {
                            var sd = this.parseString(j); j += sd.n;
                            if (sd.str === "") break;
                            var vd = this.parseValue(j); j += vd.n;
                            rec.dynamic.push({ name: sd.str, value: vd.v });
                        }
                    }
                } else if (t === 0x10) {
                    rec.fixed = b[j++] === 1;
                    var cn10 = this.parseString(j); j += cn10.n;
                    rec.className = cn10.str || "";
                    rec.values = [];
                    for (var e10 = 0; e10 < len; e10++) {
                        var v10 = this.parseValue(j); j += v10.n;
                        rec.values.push(v10.v);
                    }
                } else {
                    rec.weak = b[j++] === 1;
                    rec.entries = [];
                    for (var e11 = 0; e11 < len; e11++) {
                        var k11 = this.parseValue(j); j += k11.n;
                        var vv11 = this.parseValue(j); j += vv11.n;
                        rec.entries.push([k11.v, vv11.v]);
                    }
                }
                return { v: rec, n: j - i };
            }
            case 0x0b: {
                var rB = readU29(b, i + 1);
                if ((rB.v & 1) === 0) { this.refRecs.push({ pos: i, idx: rB.v >> 1, t: t }); return { v: { __ref: rB.v >> 1, __rt: t }, n: 1 + rB.n }; }
                var lenB = rB.v >> 1;
                return { v: { __amf: "xml", s: td.decode(b.subarray(i + 1 + rB.n, i + 1 + rB.n + lenB)) }, n: 1 + rB.n + lenB };
            }
            case 0x0c: {
                var rC = readU29(b, i + 1);
                if ((rC.v & 1) === 0) { this.refRecs.push({ pos: i, idx: rC.v >> 1, t: t }); return { v: { __ref: rC.v >> 1, __rt: t }, n: 1 + rC.n }; }
                var lenC = rC.v >> 1;
                return { v: { __amf: "bytes", b: Array.prototype.slice.call(b.subarray(i + 1 + rC.n, i + 1 + rC.n + lenC)) }, n: 1 + rC.n + lenC };
            }
            case 0x0d: case 0x0e: case 0x0f: {
                var rV = readU29(b, i + 1);
                if ((rV.v & 1) === 0) { this.refRecs.push({ pos: i, idx: rV.v >> 1, t: t }); return { v: { __ref: rV.v >> 1, __rt: t }, n: 1 + rV.n }; }
                var lenV = rV.v >> 1;
                var jV = i + 1 + rV.n;
                var fixedV = b[jV++];
                var width = t === 0x0f ? 8 : 4;
                var dv = new DataView(b.buffer, b.byteOffset + jV, lenV * width);
                var vals = [];
                for (var eV = 0; eV < lenV; eV++) {
                    if (t === 0x0d) vals.push(dv.getInt32(eV * 4, false));
                    else if (t === 0x0e) vals.push(dv.getUint32(eV * 4, false));
                    else vals.push(dv.getFloat64(eV * 8, false));
                }
                var amf = t === 0x0d ? "vecint" : t === 0x0e ? "vecuint" : "vecdouble";
                return { v: { __amf: amf, fixed: fixedV === 1, values: vals }, n: jV - i + lenV * width };
            }
            default: throw new Error("未知 AMF3 标记 0x" + t.toString(16) + " @" + i);
        }
    };

    // 第二遍：值相等去重建表
    function buildTable(ordered) {
        var table = [];
        var dedup = {};
        for (var i = 0; i < ordered.length; i++) {
            var rec = ordered[i];
            var key = JSON.stringify(rec);
            if (!(key in dedup)) {
                dedup[key] = table.length;
                table.push(rec);
            }
        }
        return table;
    }

    // 展开：树中所有 {__ref} 替换为 table[idx] 的深拷贝（递归）
    function expandValue(v, table, depth, inflight, propName, propTypes) {
        if (v === null || typeof v !== "object") return v;
        if (v.__i !== undefined || v.__n !== undefined) return v;
        if (v.__ref !== undefined) {
            if (depth > 64) return "undefined";
            if (inflight.has(v.__ref)) return "undefined";
            var target = table[v.__ref];
            if (target === undefined) return "undefined";
            // 类型匹配检查：引用标记 vs 表项类型（读写端表错位回退）
            if (v.__rt === 0x10 && target.__amf !== "vecobject") {
                var fb = null;
                for (var fi = 0; fi < table.length; fi++) {
                    var t = table[fi];
                    if (t.__amf === "vecobject" && t.values.some(function (x) { return x === target; })) { fb = t; break; }
                }
                if (fb) target = fb;
            } else if (v.__rt === 0x11 && target.__amf !== "dict") {
                var fb2 = null;
                for (var fi2 = 0; fi2 < table.length; fi2++) {
                    var t2 = table[fi2];
                    if (t2.__amf === "dict" && t2.entries.some(function (e) { return e[0] === target || e[1] === target; })) { fb2 = t2; break; }
                }
                if (fb2) target = fb2;
            } else if (v.__rt === 0x0a && target.__amf !== "object") {
                var fb3 = null;
                for (var fi3 = 0; fi3 < table.length; fi3++) {
                    var t3 = table[fi3];
                    if (t3.__amf === "object" && (t3.values.some(function (x) { return x === target; }) || t3.dynamic.some(function (d) { return d.value === target; }))) { fb3 = t3; break; }
                }
                if (fb3) target = fb3;
            }
            // 期望类型修正：Vector.<int> 属性被去重成空 vecobject → 还原为空 vecint
            var VECINT_PROPS = ["m_vWaypoints", "m_vEncQueue", "m_vFactions", "m_vEncounterTriggersRemaining", "m_vEventQueue", "m_vKnownRecipes", "m_vSearchStates", "m_vUsedRecipes"];
            if (target && target.__amf === "vecobject" && target.values.length === 0 && VECINT_PROPS.indexOf(propName) !== -1) {
              target = { __amf: "vecint", fixed: false, values: [] };
            }
            inflight.add(v.__ref);
            try { return expandValue(target, table, depth + 1, inflight, propName, propTypes); }
            finally { inflight.delete(v.__ref); }
        }
        if (v.__amf === "object") {
            return {
                __amf: "object", className: v.className, names: v.names, isDynamic: v.isDynamic,
                values: v.values.map(function (x, i) { return expandValue(x, table, depth, inflight, v.names[i], propTypes); }),
                dynamic: v.dynamic.map(function (d) { return { name: d.name, value: expandValue(d.value, table, depth, inflight, d.name, propTypes) }; }),
            };
        }
        if (v.__amf === "vecobject") {
            return {
                __amf: "vecobject", fixed: v.fixed, className: v.className,
                values: v.values.map(function (x) { return expandValue(x, table, depth, inflight, propName, propTypes); }),
            };
        }
        if (v.__amf === "dict") {
            return {
                __amf: "dict", weak: v.weak,
                entries: v.entries.map(function (e) { return [expandValue(e[0], table, depth, inflight, propName, propTypes), expandValue(e[1], table, depth, inflight, propName, propTypes)]; }),
            };
        }
        if (v.__amf === "array") {
            return {
                __amf: "array", dense: v.dense.map(function (x) { return expandValue(x, table, depth, inflight, propName, propTypes); }),
                assoc: Object.keys(v.assoc).reduce(function (o, k) { o[k] = expandValue(v.assoc[k], table, depth, inflight); return o; }, {}),
            };
        }
        return v;
    }

    // ── 编码（全内联）────────────────────────────────────────────
    function encString(out, s) {
        var bytes = te.encode(s);
        writeU29(out, (bytes.length << 1) | 1);
        for (var i = 0; i < bytes.length; i++) out.push(bytes[i]);
    }
    function encValue(out, v) {
        if (v === "undefined") { out.push(0x00); return; }
        if (v === null) { out.push(0x01); return; }
        if (v === false) { out.push(0x02); return; }
        if (v === true) { out.push(0x03); return; }
        if (v && v.__i !== undefined) { out.push(0x04); writeU29(out, v.__i & 0x1fffffff); return; }
        if (v && v.__n !== undefined) {
            out.push(0x05);
            var b5 = new ArrayBuffer(8);
            new DataView(b5).setFloat64(0, v.__n, false);
            var u5 = new Uint8Array(b5);
            for (var i5 = 0; i5 < 8; i5++) out.push(u5[i5]);
            return;
        }
        if (typeof v === "number") {
            if (Number.isInteger(v) && v >= -268435456 && v <= 268435455) {
                out.push(0x04); writeU29(out, v & 0x1fffffff);
            } else {
                out.push(0x05);
                var bN = new ArrayBuffer(8);
                new DataView(bN).setFloat64(0, v, false);
                var uN = new Uint8Array(bN);
                for (var iN = 0; iN < 8; iN++) out.push(uN[iN]);
            }
            return;
        }
        if (typeof v === "string") { out.push(0x06); encString(out, v); return; }
        if (v.__amf === "date") {
            out.push(0x08); writeU29(out, 1);
            var bD = new ArrayBuffer(8);
            new DataView(bD).setFloat64(0, v.ms, false);
            var uD = new Uint8Array(bD);
            for (var iD = 0; iD < 8; iD++) out.push(uD[iD]);
            return;
        }
        if (v.__amf === "xml") { out.push(0x0b); writeU29(out, (v.s.length << 1) | 1); var ex = te.encode(v.s); for (var ix = 0; ix < ex.length; ix++) out.push(ex[ix]); return; }
        if (v.__amf === "bytes") { out.push(0x0c); writeU29(out, (v.b.length << 1) | 1); for (var ib = 0; ib < v.b.length; ib++) out.push(v.b[ib]); return; }
        if (v.__amf === "vecint" || v.__amf === "vecuint") {
            out.push(v.__amf === "vecint" ? 0x0d : 0x0e);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            var bI = new ArrayBuffer(4);
            var dI = new DataView(bI);
            for (var iI = 0; iI < v.values.length; iI++) {
                if (v.__amf === "vecint") dI.setInt32(0, v.values[iI], false);
                else dI.setUint32(0, v.values[iI], false);
                var uI = new Uint8Array(bI);
                for (var kI = 0; kI < 4; kI++) out.push(uI[kI]);
            }
            return;
        }
        if (v.__amf === "vecdouble") {
            out.push(0x0f);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            var bF = new ArrayBuffer(8);
            var dF = new DataView(bF);
            for (var iF = 0; iF < v.values.length; iF++) {
                dF.setFloat64(0, v.values[iF], false);
                var uF = new Uint8Array(bF);
                for (var kF = 0; kF < 8; kF++) out.push(uF[kF]);
            }
            return;
        }
        if (v.__amf === "vecobject") {
            out.push(0x10);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            encString(out, v.className || "");
            for (var iO = 0; iO < v.values.length; iO++) encValue(out, v.values[iO]);
            return;
        }
        if (v.__amf === "dict") {
            out.push(0x11);
            writeU29(out, (v.entries.length << 1) | 1);
            out.push(v.weak ? 1 : 0);
            for (var eD = 0; eD < v.entries.length; eD++) { encValue(out, v.entries[eD][0]); encValue(out, v.entries[eD][1]); }
            return;
        }
        if (v.__amf === "array") {
            var dense = v.dense || [];
            out.push(0x09);
            writeU29(out, (dense.length << 1) | 1);
            for (var iA = 0; iA < dense.length; iA++) encValue(out, dense[iA]);
            var keys = Object.keys(v.assoc || {});
            for (var kA = 0; kA < keys.length; kA++) { encString(out, keys[kA]); encValue(out, v.assoc[keys[kA]]); }
            encString(out, "");
            return;
        }
        if (v.__amf === "object") {
            out.push(0x0a);
            var dynamic = v.dynamic || [];
            var isDyn = v.isDynamic || dynamic.length > 0;
            var enc = (isDyn ? 0b10 : 0) | 0b00;
            writeU29(out, ((v.names.length << 4) | (enc << 2) | 3));
            encString(out, v.className || "");
            for (var nO = 0; nO < v.names.length; nO++) encString(out, v.names[nO]);
            for (var sO = 0; sO < v.values.length; sO++) encValue(out, v.values[sO]);
            if (isDyn) {
                for (var dO = 0; dO < dynamic.length; dO++) { encString(out, dynamic[dO].name); encValue(out, dynamic[dO].value); }
                encString(out, "");
            }
            return;
        }
        throw new Error("无法编码的值: " + JSON.stringify(v).slice(0, 80));
    }

    // 解析完整 LSO（base64 → 树 + 表）
    function parseLso(b64) {
        var buf = bytesFromB64(b64);
        var h = 0;
        if (buf[h] !== 0x00 || buf[h + 1] !== 0xbf) throw new Error("LSO 头版本不符");
        h += 2;
        var dvH = new DataView(buf.buffer, buf.byteOffset, buf.length);
        h += 4;   // u32 len
        var sig = td.decode(buf.subarray(h, h + 10)); h += 10;
        if (sig.indexOf("TCSO") !== 0) throw new Error("LSO 签名不符");
        var nameLen = dvH.getUint16(h, false); h += 2;
        var name = td.decode(buf.subarray(h, h + nameLen)); h += nameLen;
        h += 3;
        var formatVersion = buf[h++];
        var p1 = new P1(buf);
        var body = [];
        while (h < buf.length) {
            if (buf[h] === 0) { h++; continue; }
            var s = p1.parseString(h);
            h += s.n;
            if (s.str === "") { h += 1; continue; }
            var val = p1.parseValue(h);
            h += val.n;
            body.push({ name: s.str, value: val.v });
        }
        var table = buildTable(p1.ordered);
        return { name: name, formatVersion: formatVersion, body: body, table: table };
    }

    function encodeLso(lso) {
        var out = [];
        out.push(0x00, 0xbf);
        var nameBytes = te.encode(lso.name);
        var bodyOut = [];
        for (var i = 0; i < lso.body.length; i++) {
            encString(bodyOut, lso.body[i].name);
            encValue(bodyOut, lso.body[i].value);
            bodyOut.push(0x00);
        }
        var bodyLen = 10 + 2 + nameBytes.length + 3 + 1 + bodyOut.length;
        var lenBuf = new ArrayBuffer(4);
        new DataView(lenBuf).setUint32(0, bodyLen, false);
        var uL = new Uint8Array(lenBuf);
        out.push(uL[0], uL[1], uL[2], uL[3]);
        var sigBytes = te.encode("TCSO\u0000\u0004\u0000\u0000\u0000\u0000");
        for (var sI = 0; sI < 10; sI++) out.push(sigBytes[sI]);
        var nb = new ArrayBuffer(2);
        new DataView(nb).setUint16(0, nameBytes.length, false);
        var uN = new Uint8Array(nb);
        out.push(uN[0], uN[1]);
        for (var nI = 0; nI < nameBytes.length; nI++) out.push(nameBytes[nI]);
        out.push(0x00, 0x00, 0x00, 0x03);
        for (var bI = 0; bI < bodyOut.length; bI++) out.push(bodyOut[bI]);
        return b64FromBytes(new Uint8Array(out));
    }

    // ── v2.48 存档形态归一化 ────────────────────────────────────────
    // 游戏运行期缺陷：Creature.SaveData getter 把 m_dictFactions 的每个值
    // push 进 Vector.<int> m_vFactions；若活体 m_dictFactions 在运行期混入
    // 非数字值（游戏自己的 faction 逻辑在空字典上运算产生的异常值），
    // 保存时崩溃（cannot convert false to Vector.<int>）。
    // 玩家存档的 m_vFactions 为空是异常形态（生物均为 14×-100 默认声望）：
    // 补全为与生物一致的 14×-100，使运行期 faction 运算作用在已存在的数字条目上。
    function normalizeFactions(v) {
        if (!v || typeof v !== "object" || v.__amf !== "object") return;
        var names = v.names || [];
        var i = names.indexOf("m_vFactions");
        var dyn = v.dynamic || [];
        var dynIdx = -1;
        for (var di = 0; di < dyn.length; di++) if (dyn[di].name === "m_vFactions") { dynIdx = di; break; }
        var mvf = i >= 0 ? v.values[i] : dynIdx >= 0 ? dyn[dynIdx].value : null;
        if (mvf && mvf.__amf === "vecint" && mvf.values.length === 0) {
            var filled = { __amf: "vecint", fixed: false, values: [-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100,-100] };
            if (i >= 0) v.values[i] = filled;
            else dyn[dynIdx].value = filled;
        }
    }

    function getProp(v, name) {
        if (!v || typeof v !== "object") return undefined;
        if (v.__amf === "object") {
            var i = (v.names || []).indexOf(name);
            if (i >= 0) return v.values[i];
            var d = (v.dynamic || []).find(function(x){ return x.name === name; });
            if (d) return d.value;
        }
        return undefined;
    }

    // 主入口：展开 base64 存档 → base64（全内联）
    function expand(b64) {
        if (!b64 || typeof b64 !== "string") return b64;
        if (b64.indexOf("AL8A") !== 0) return b64;   // 非 LSO（00bf 开头 base64）
        var parsed = parseLso(b64);
        for (var i = 0; i < parsed.body.length; i++) {
            parsed.body[i].value = expandValue(parsed.body[i].value, parsed.table, 0, new Set());
        }
        // v2.48: 玩家/生物 m_vFactions 空 → 补全 14×-100 默认声望
        for (var bi = 0; bi < parsed.body.length; bi++) {
            var r = parsed.body[bi].value;
            var p = getProp(r, "m_objPlayer");
            if (p) normalizeFactions(p);
            var vc = getProp(r, "m_vCreatures");
            if (vc && vc.__amf === "vecobject") for (var ci = 0; ci < vc.values.length; ci++) normalizeFactions(vc.values[ci]);
        }
        return encodeLso(parsed);
    }

    // ── v2.49 存档编辑工具：LSO ↔ JSON 树双向转换 ───────────────────
    // toTree: base64 → { name, formatVersion, body:[{name,value}] }，引用已全内联、
    // __amf 类型标记保留（object/array/vecint/vecobject/dict/date/xml/bytes…）——
    // JSON.stringify 直接可读可编辑。
    // fromTree: 上述 JSON 文本 → base64（全内联重编码），编码后立即回验 parseLso，
    // 改坏的树在写入 localStorage 之前就报错。
    function toTree(b64) {
        if (!b64 || typeof b64 !== "string") return { error: "空输入" };
        if (b64.indexOf("AL8A") !== 0) return { error: "非 LSO 存档（缺少 AL8A 前缀）" };
        var parsed = parseLso(b64);
        for (var i = 0; i < parsed.body.length; i++) {
            parsed.body[i].value = expandValue(parsed.body[i].value, parsed.table, 0, new Set());
        }
        sanitizeTree(parsed.body);
        return { name: parsed.name, formatVersion: parsed.formatVersion, body: parsed.body };
    }

    // v2.49: JSON 无法表达 NaN/±Infinity（stringify 会变 null 且不可逆）。
    // 存档里大量未初始化 double 是 NaN——转成字符串标记保留语义，
    // fromTree 后 encValue 的 setFloat64 会自动还原（ToNumber("NaN") = NaN）。
    function sanitizeTree(v) {
        if (v === null || typeof v !== "object") return;
        if (Array.isArray(v)) {
            for (var i = 0; i < v.length; i++) sanitizeTree(v[i]);
            return;
        }
        if (v.__n !== undefined && typeof v.__n === "number" && !isFinite(v.__n)) {
            v.__n = String(v.__n);   // "NaN" / "Infinity" / "-Infinity"
            return;
        }
        for (var k in v) {
            if (Object.prototype.hasOwnProperty.call(v, k)) sanitizeTree(v[k]);
        }
    }

    function fromTree(jsonText) {
        var tree;
        try { tree = JSON.parse(jsonText); }
        catch (e) { return { error: "JSON 解析失败: " + (e && e.message ? e.message : e) }; }
        if (!tree || typeof tree !== "object" || !Array.isArray(tree.body)) {
            return { error: "结构不符（缺少 body 数组）" };
        }
        try {
            var b64 = encodeLso({
                name: typeof tree.name === "string" ? tree.name : "",
                formatVersion: tree.formatVersion,
                body: tree.body,
            });
            parseLso(b64);   // 回验：编码结果必须能重新解析
            return { b64: b64 };
        } catch (e) {
            return { error: "编码失败: " + (e && e.message ? e.message : e) };
        }
    }

    globalThis.LsoExpand = { expand: expand, toTree: toTree, fromTree: fromTree };
})(typeof window !== "undefined" ? window : globalThis);
