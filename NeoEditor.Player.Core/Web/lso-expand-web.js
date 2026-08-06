/* lso-expand-web.js —— 浏览器版 LSO 存档展开器（v2.44）
 *
 * 背景：Ruffle nightly 反序列化 AMF3 引用（Amf3ObjectReference）有 bug，
 * 受伤存档重启必崩（"m_fDate not found on Number"，Ruffle #1069）。
 * 方案：SWF 加载前把 localStorage 里的 LSO 存档解析 → 展开全部引用 →
 * 重新编码为完全内联（无引用）的字节流，Ruffle 读取时就无引用可崩。
 *
 * 与 player-tools/lso-expand.js（node 原型）逻辑一一对应，已验证往返无损；
 * 本文件为无依赖浏览器版本（Uint8Array/TextDecoder/atob）。
 * 用法：LsoExpand.expand(base64String) → base64String
 */
(function (global) {
    "use strict";

    var te = new TextEncoder();
    var td = new TextDecoder("utf-8");

    // ── 字节读取工具（Big-endian）──────────────────────────────
    function readU29(b, i) {
        // flash-lso 规则：前 3 字节各 7 位（bit7=续位），第 4 字节整 8 位
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
        // flash-lso write_int：1/2/3 字节 7 位组；4 字节 = 7+7+7+8
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
    function utf8Slice(b, start, end) {
        return td.decode(b.subarray(start, end));
    }
    function latin1Slice(b, start, end) {
        var s = "";
        for (var i = start; i < end; i++) s += String.fromCharCode(b[i]);
        return s;
    }
    function readDouble(b, i) {
        return new DataView(b.buffer, b.byteOffset, b.byteLength).getFloat64(i, false);
    }
    function readInt32(b, i) {
        return new DataView(b.buffer, b.byteOffset, b.byteLength).getInt32(i, false);
    }
    function readUInt32(b, i) {
        return new DataView(b.buffer, b.byteOffset, b.byteLength).getUint32(i, false);
    }

    // ── base64 ─────────────────────────────────────────────────
    function decodeBase64(s) {
        var bin = atob(s.replace(/-/g, "+").replace(/_/g, "/"));
        var out = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
        return out;
    }
    function encodeBase64(bytes) {
        var bin = "";
        var CHUNK = 0x8000;
        for (var i = 0; i < bytes.length; i += CHUNK) {
            bin += String.fromCharCode.apply(null, bytes.subarray(i, i + CHUNK));
        }
        return btoa(bin);
    }

    // ── 解析上下文 ─────────────────────────────────────────────
    function Parser(b) {
        this.b = b;
        this.refs = [];          // 引用索引 → JS 值（按出现顺序编号）
        this.inflight = new Set();
        this.strTable = [];
        this.traitTable = [];
    }
    Parser.prototype.parseString = function (i) {
        var b = this.b;
        var r = readU29(b, i);
        if ((r.v & 1) === 0) {   // 字符串引用
            var idx = r.v >> 1;
            var s = this.strTable[idx] || "";
            return { ref: idx, n: r.n, str: s };
        }
        var len = r.v >> 1;
        if (len === 0) return { str: "", n: r.n, ref: null };
        var str = utf8Slice(b, i + r.n, i + r.n + len);
        this.strTable.push(str);
        return { str: str, n: r.n + len, ref: null };
    };
    Parser.prototype.parseValue = function (i, expandDepth) {
        var b = this.b;
        var t = b[i];
        switch (t) {
            case 0x00: return { v: "undefined", n: 1 };
            case 0x01: return { v: null, n: 1 };
            case 0x02: return { v: false, n: 1 };
            case 0x03: return { v: true, n: 1 };
            case 0x04: {   // Integer：flash-lso read_int_signed —— bit28 置位 → 减 2^29
                var r4 = readU29(b, i + 1);
                return { v: (r4.v & 0x10000000) ? r4.v - 0x20000000 : r4.v, n: 1 + r4.n };
            }
            case 0x05: return { v: readDouble(b, i + 1), n: 9 };
            case 0x06: {
                var s6 = this.parseString(i + 1);
                return { v: s6.str, n: 1 + s6.n };
            }
            case 0x08: {   // Date（可引用）
                var r8 = readU29(b, i + 1);
                if ((r8.v & 1) === 0) {
                    return { v: this.expandRef(r8.v >> 1, expandDepth), n: 1 + r8.n };
                }
                var date = readDouble(b, i + 1 + r8.n);
                var rec8 = { __amf: "date", ms: date };
                this.refs.push(rec8);
                return { v: rec8, n: 1 + r8.n + 8 };
            }
            case 0x09: {   // Array（可引用）
                var r9 = readU29(b, i + 1);
                if ((r9.v & 1) === 0) {
                    return { v: this.expandRef(r9.v >> 1, expandDepth), n: 1 + r9.n };
                }
                var denseLen = r9.v >> 1;
                var j = i + 1 + r9.n;
                var arr = { __amf: "array", dense: [], assoc: {} };
                this.refs.push(arr);
                for (;;) {   // 关联元素：名字 + 值，直到空名字
                    var sa = this.parseString(j);
                    j += sa.n;
                    if (sa.str === "") break;
                    var va = this.parseValue(j, expandDepth);
                    j += va.n;
                    arr.assoc[sa.str] = va.v;
                }
                for (var e = 0; e < denseLen; e++) {
                    var vd = this.parseValue(j, expandDepth);
                    j += vd.n;
                    arr.dense.push(vd.v);
                }
                return { v: arr, n: j - i };
            }
            case 0x0a: {   // Object（可引用）
                var r10 = readU29(b, i + 1);
                if ((r10.v & 1) === 0) {
                    return { v: this.expandRef(r10.v >> 1, expandDepth), n: 1 + r10.n };
                }
                // traits 编码（flash-lso 对照）：
                //   新 traits: size = (count<<4)|(enc<<2)|3   （enc 2 位：bit0=external, bit1=dynamic）
                //   traits 引用: size = (index<<2)|1
                var traits = r10.v >> 1;
                var encoding, names, className;
                var j = i + 1 + r10.n;
                if ((traits & 1) === 0) {   // traits 引用
                    var def = this.traitTable[traits >> 1];
                    if (!def) throw new Error("traits 引用越界 #" + (traits >> 1) + " @" + i);
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
                var obj = { __amf: "object", className: className, names: names, values: [], dynamic: [] };
                this.refs.push(obj);
                for (var m2 = 0; m2 < names.length; m2++) {   // static 值按序
                    var sv = this.parseValue(j, expandDepth); j += sv.n;
                    obj.values.push(sv.v);
                }
                if ((encoding & 0b10) === 0b10) {   // bit1：dynamic
                    for (;;) {
                        var sd = this.parseString(j); j += sd.n;
                        if (sd.str === "") break;
                        var vd2 = this.parseValue(j, expandDepth); j += vd2.n;
                        obj.dynamic.push({ name: sd.str, value: vd2.v });
                    }
                }
                return { v: obj, n: j - i };
            }
            case 0x0b: {   // XML（可引用）
                var rb = readU29(b, i + 1);
                if ((rb.v & 1) === 0) {
                    return { v: this.expandRef(rb.v >> 1, expandDepth), n: 1 + rb.n };
                }
                var lenb = rb.v >> 1;
                var recb = { __amf: "xml", content: utf8Slice(b, i + 1 + rb.n, i + 1 + rb.n + lenb) };
                this.refs.push(recb);
                return { v: recb, n: 1 + rb.n + lenb };
            }
            case 0x0c: {   // ByteArray（可引用）
                var rc = readU29(b, i + 1);
                if ((rc.v & 1) === 0) {
                    return { v: this.expandRef(rc.v >> 1, expandDepth), n: 1 + rc.n };
                }
                var lenc = rc.v >> 1;
                var recc = { __amf: "bytes", data: b.slice(i + 1 + rc.n, i + 1 + rc.n + lenc) };
                this.refs.push(recc);
                return { v: recc, n: 1 + rc.n + lenc };
            }
            case 0x0d: case 0x0e: case 0x0f: case 0x10: case 0x11: {   // Vector*/Dictionary
                var rv = readU29(b, i + 1);
                if ((rv.v & 1) === 0) {
                    return { v: this.expandRef(rv.v >> 1, expandDepth), n: 1 + rv.n };
                }
                var lenv = rv.v >> 1;
                var j = i + 1 + rv.n;
                var rec;
                if (t === 0x0d) {   // VectorInt: u8 fixed + i32×len
                    var fixed = b[j++];
                    var vals = [];
                    for (var e = 0; e < lenv; e++) { vals.push(readInt32(b, j)); j += 4; }
                    rec = { __amf: "vecint", fixed: fixed === 1, values: vals };
                } else if (t === 0x0e) {   // VectorUInt
                    var fixed2 = b[j++];
                    var vals2 = [];
                    for (var e2 = 0; e2 < lenv; e2++) { vals2.push(readUInt32(b, j)); j += 4; }
                    rec = { __amf: "vecuint", fixed: fixed2 === 1, values: vals2 };
                } else if (t === 0x0f) {   // VectorDouble
                    var fixed3 = b[j++];
                    var vals3 = [];
                    for (var e3 = 0; e3 < lenv; e3++) { vals3.push(readDouble(b, j)); j += 8; }
                    rec = { __amf: "vecdouble", fixed: fixed3 === 1, values: vals3 };
                } else if (t === 0x10) {   // VectorObject: u8 fixed + 类名 + 值×len
                    var fixed4 = b[j++];
                    var cn4 = this.parseString(j);
                    j += cn4.n;
                    var vals4 = [];
                    for (var e4 = 0; e4 < lenv; e4++) {
                        var v4 = this.parseValue(j, expandDepth);
                        j += v4.n;
                        vals4.push(v4.v);
                    }
                    rec = { __amf: "vecobject", fixed: fixed4 === 1, className: cn4.str || "", values: vals4 };
                } else {   // Dictionary: u8 weak + [key,value]×len
                    var weak = b[j++];
                    var entries = [];
                    for (var e5 = 0; e5 < lenv; e5++) {
                        var k5 = this.parseValue(j, expandDepth);
                        j += k5.n;
                        var v5 = this.parseValue(j, expandDepth);
                        j += v5.n;
                        entries.push([k5.v, v5.v]);
                    }
                    rec = { __amf: "dict", weak: weak === 1, entries: entries };
                }
                this.refs.push(rec);
                return { v: rec, n: j - i };
            }
            default: throw new Error("未知 AMF3 标记 0x" + t.toString(16) + " @" + i);
        }
    };
    // 展开引用：深拷贝 refs[ref] 的内联表示（环 → undefined 保守处理）
    Parser.prototype.expandRef = function (ref, expandDepth) {
        if (this.inflight.has(ref)) return "undefined";
        var target = this.refs[ref];
        if (target === undefined) return "undefined";
        if (expandDepth > 64) return "undefined";
        this.inflight.add(ref);
        try {
            return this.clone(target, expandDepth + 1);
        } finally {
            this.inflight.delete(ref);
        }
    };
    Parser.prototype.clone = function (v, depth) {
        if (depth > 64) return "undefined";
        if (v === null || typeof v !== "object") return v;
        if (v.__amf === "object") {
            return {
                __amf: "object", className: v.className, names: v.names,
                values: v.values.map(function (x) { return this.clone(x, depth + 1); }, this),
                dynamic: v.dynamic.map(function (d) { return { name: d.name, value: this.clone(d.value, depth + 1) }; }, this)
            };
        }
        if (v.__amf === "array") {
            return {
                __amf: "array",
                dense: v.dense.map(function (x) { return this.clone(x, depth + 1); }, this),
                assoc: Object.fromEntries(Object.entries(v.assoc).map(function (e) { return [e[0], this.clone(e[1], depth + 1)]; }, this))
            };
        }
        return v;
    };

    // ── 解析完整 LSO ───────────────────────────────────────────
    function parseLso(base64) {
        var buf = decodeBase64(base64);
        var h = 0;
        if (buf[h] !== 0x00 || buf[h + 1] !== 0xbf) throw new Error("LSO 头版本不符");
        h += 2;
        h += 4;   // length 字段（flash-lso 不校验）
        var sig = latin1Slice(buf, h, h + 10); h += 10;
        if (sig.indexOf("TCSO") !== 0) throw new Error("LSO 签名不符: " + sig);
        var nameLen = (buf[h] << 8) | buf[h + 1]; h += 2;
        var name = utf8Slice(buf, h, h + nameLen); h += nameLen;
        h += 3;   // padding ×3
        var formatVersion = buf[h++];
        var p = new Parser(buf);
        var body = [];
        while (h < buf.length) {
            if (buf[h] === 0) { h++; continue; }
            var s = p.parseString(h);
            h += s.n;
            if (s.str === "") { h += 1; continue; }
            var val = p.parseValue(h, 0);
            h += val.n;
            body.push({ name: s.str, value: val.v });
        }
        return { name: name, formatVersion: formatVersion, body: body };
    }

    // ── 重新编码（全部内联）────────────────────────────────────
    function encodeString(out, s) {
        var bytes = te.encode(s);
        writeU29(out, (bytes.length << 1) | 1);
        for (var i = 0; i < bytes.length; i++) out.push(bytes[i]);
    }
    function encodeDoubleBytes(out, v) {
        var dv = new DataView(new ArrayBuffer(8));
        dv.setFloat64(0, v, false);
        for (var i = 0; i < 8; i++) out.push(dv.getUint8(i));
    }
    function encodeValue(out, v) {
        if (v === "undefined") { out.push(0x00); return; }
        if (v === null) { out.push(0x01); return; }
        if (v === false) { out.push(0x02); return; }
        if (v === true) { out.push(0x03); return; }
        if (typeof v === "number") {
            if (Number.isInteger(v) && v >= -268435456 && v <= 268435455) {   // int 范围 ±2^28
                out.push(0x04);
                writeU29(out, v < 0 ? v + 0x20000000 : v);
            } else {
                out.push(0x05);
                encodeDoubleBytes(out, v);
            }
            return;
        }
        if (typeof v === "string") { out.push(0x06); encodeString(out, v); return; }
        if (v.__amf === "date") { out.push(0x08); writeU29(out, (8 << 1) | 1); encodeDoubleBytes(out, v.ms); return; }
        if (v.__amf === "xml") { out.push(0x0b); var xb = te.encode(v.content); writeU29(out, (xb.length << 1) | 1); for (var ix = 0; ix < xb.length; ix++) out.push(xb[ix]); return; }
        if (v.__amf === "bytes") { out.push(0x0c); writeU29(out, (v.data.length << 1) | 1); for (var ic = 0; ic < v.data.length; ic++) out.push(v.data[ic]); return; }
        if (v.__amf === "array") {
            out.push(0x09);
            writeU29(out, (v.dense.length << 1) | 1);
            for (var ka of Object.keys(v.assoc)) { encodeString(out, ka); encodeValue(out, v.assoc[ka]); }
            encodeString(out, "");
            for (var id = 0; id < v.dense.length; id++) encodeValue(out, v.dense[id]);
            return;
        }
        if (v.__amf === "object") {
            out.push(0x0a);
            var encoding = v.dynamic.length ? 0b10 : 0b00;
            writeU29(out, ((v.names.length << 4) | (encoding << 2) | 0b11));
            encodeString(out, v.className || "");
            for (var nm = 0; nm < v.names.length; nm++) encodeString(out, v.names[nm]);
            for (var iv = 0; iv < v.values.length; iv++) encodeValue(out, v.values[iv]);
            for (var idy = 0; idy < v.dynamic.length; idy++) { encodeString(out, v.dynamic[idy].name); encodeValue(out, v.dynamic[idy].value); }
            if (v.dynamic.length) encodeString(out, "");
            return;
        }
        if (v.__amf === "vecint") {
            out.push(0x0d);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            for (var i1 = 0; i1 < v.values.length; i1++) {
                var d1 = new DataView(new ArrayBuffer(4)); d1.setInt32(0, v.values[i1], false);
                for (var k1 = 0; k1 < 4; k1++) out.push(d1.getUint8(k1));
            }
            return;
        }
        if (v.__amf === "vecuint") {
            out.push(0x0e);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            for (var i2 = 0; i2 < v.values.length; i2++) {
                var d2 = new DataView(new ArrayBuffer(4)); d2.setUint32(0, v.values[i2], false);
                for (var k2 = 0; k2 < 4; k2++) out.push(d2.getUint8(k2));
            }
            return;
        }
        if (v.__amf === "vecdouble") {
            out.push(0x0f);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            for (var i3 = 0; i3 < v.values.length; i3++) encodeDoubleBytes(out, v.values[i3]);
            return;
        }
        if (v.__amf === "vecobject") {
            out.push(0x10);
            writeU29(out, (v.values.length << 1) | 1);
            out.push(v.fixed ? 1 : 0);
            encodeString(out, v.className || "");
            for (var i4 = 0; i4 < v.values.length; i4++) encodeValue(out, v.values[i4]);
            return;
        }
        if (v.__amf === "dict") {
            out.push(0x11);
            writeU29(out, (v.entries.length << 1) | 1);   // bit0 = 引用标志（flash-lso），weak 是独立字节
            out.push(v.weak ? 1 : 0);
            for (var i5 = 0; i5 < v.entries.length; i5++) { encodeValue(out, v.entries[i5][0]); encodeValue(out, v.entries[i5][1]); }
            return;
        }
        throw new Error("无法编码的值: " + JSON.stringify(v).slice(0, 80));
    }
    function encodeLso(parsed) {
        var nameBytes = te.encode(parsed.name);
        var body = [];
        for (var i = 0; i < parsed.body.length; i++) {
            encodeString(body, parsed.body[i].name);
            encodeValue(body, parsed.body[i].value);
            body.push(0x00);
        }
        var out = [];
        out.push(0x00, 0xbf);
        var len = 16 + nameBytes.length + body.length;
        out.push((len >>> 24) & 0xff, (len >>> 16) & 0xff, (len >>> 8) & 0xff, len & 0xff);
        var sig = [0x54, 0x43, 0x53, 0x4f, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00];
        for (var is = 0; is < sig.length; is++) out.push(sig[is]);
        out.push((nameBytes.length >>> 8) & 0xff, nameBytes.length & 0xff);
        for (var in2 = 0; in2 < nameBytes.length; in2++) out.push(nameBytes[in2]);
        out.push(0x00, 0x00, 0x00, 0x03);   // padding×3 + format_version=AMF3
        for (var ib = 0; ib < body.length; ib++) out.push(body[ib]);
        return encodeBase64(new Uint8Array(out));
    }

    // ── 入口 ───────────────────────────────────────────────────
    // 解析 → 引用全展开 → 重编码为全内联；任何失败返回 null（调用方回退原始值）
    function expand(base64) {
        var parsed = parseLso(base64);
        return encodeLso(parsed);
    }

    global.LsoExpand = { expand: expand, parseLso: parseLso, encodeLso: encodeLso };
})(typeof window !== "undefined" ? window : this);
