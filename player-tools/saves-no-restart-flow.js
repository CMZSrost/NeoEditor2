#!/usr/bin/env node
// 模拟 host.html 存档拦截逻辑（v2.72 墓碑/保护）→ 验证 删除→恢复→重启 流程：
//  游戏内存副本 (mem) + localStorage (ls)；Ruffle 读走 getItem、写走 setItem、卸载 flush 走 setItem。
// 场景：1) 游戏保存  2) 存档管理删除  3) 游戏自动保存（应被墓碑拦截）  4) 存档管理恢复
//       5) 卸载 flush（应被保护拦截）  6) 重启（新页面）  7) 游戏读取（应读到恢复的存档）
const assert = require("assert");

// ── 沙箱：localStorage + 包装器（与 host.html 相同的逻辑）──
function makePage() {
  const ls = new Map();   // 真实存储（新页面共享 → 用外部 map）
  return { ls };
}
const realStorage = new Map();
let logs = [];

function newPage(realStorage) {
  // localStorage 实例方法（与浏览器 Storage 一致）
  const storage = {
    get length() { return realStorage.size; },
    key(i) { return [...realStorage.keys()][i] ?? null; },
    getItem(k) { return realStorage.has(k) ? realStorage.get(k) : null; },
    setItem(k, v) { realStorage.set(k, String(v)); },
    removeItem(k) { realStorage.delete(k); },
    clear() { realStorage.clear(); },
  };
  const page = { storage, __savesCleared: false, __blockSaves: false };
  page.__deletedKeys = {};
  page.__protectedKeys = {};
  page.__managerOp = false;
  page.__blockLogged = {};
  const origSetItem = storage.setItem.bind(storage);
  const origRemoveItem = storage.removeItem.bind(storage);
  page.__unmarkKey = function (k) { if (!k) return; delete page.__deletedKeys[k]; delete page.__protectedKeys[k]; };
  storage.setItem = function (key, value) {
    if (page.__blockSaves) return;
    if (page.__deletedKeys[key] || page.__protectedKeys[key]) {
      logs.push("BLOCKED setItem " + key);
      return;
    }
    logs.push("setItem " + key + " len=" + value.length);
    return origSetItem(key, value);
  };
  storage.removeItem = function (key) {
    const old = storage.getItem(key);
    if (old !== null) logs.push("backup(old) " + key + " len=" + old.length);
    if (key && key.indexOf("nsSGv1") !== -1) page.__savesCleared = true;
    if (!page.__managerOp) page.__unmarkKey(key);
    return origRemoveItem(key);
  };
  return page;
}
function hasSave(page) {
  for (let i = 0; i < page.storage.length; i++) {
    const k = page.storage.key(i);
    if (k && k.indexOf("nsSGv1") !== -1) return true;
  }
  return false;
}
function unloadFlush(page) {
  // Ruffle drop → flush_shared_objects → 把内存副本写回（走包装器）
  if (page.memSave !== undefined) page.storage.setItem(page.memKey, page.memSave);
}

// ── 场景 ──
const KEY = "http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1";

// 第 1 页：游戏运行中
let page = newPage(realStorage);
// 游戏启动读档（无档）
let gameSave = page.storage.getItem(KEY);          // null
// 玩家开始游戏并自动保存 → 内存副本 + 写盘
page.memKey = KEY;
page.memSave = "AL8A-RAW-LSO-1";
page.storage.setItem(KEY, page.memSave);           // 正常保存
assert.equal(page.storage.getItem(KEY), "AL8A-RAW-LSO-1");

// 存档管理：删除（墓碑）
page.__managerOp = true;
page.__deletedKeys[KEY] = true;
page.storage.removeItem(KEY);                       // 包装器：备份旧值 + savesCleared
page.__managerOp = false;
assert.equal(page.storage.getItem(KEY), null);
assert.equal(page.__savesCleared, true);

// 游戏回合结束自动保存 → 墓碑拦截
logs = [];
page.storage.setItem(KEY, page.memSave);
assert.equal(page.storage.getItem(KEY), null, "删除后自动保存应被墓碑拦截");
assert.ok(logs.some(l => l.startsWith("BLOCKED")), "应有拦截日志");

// 存档管理：恢复（解除墓碑 → 写入 → 保护）
logs = [];
page.__managerOp = true;
page.__unmarkKey(KEY);
page.storage.setItem(KEY, "AL8A-RESTORED");
page.__protectedKeys[KEY] = true;
page.__managerOp = false;
assert.equal(page.storage.getItem(KEY), "AL8A-RESTORED", "恢复应写入");
assert.equal(page.__protectedKeys[KEY], true);

// 游戏自动保存 → 保护拦截（内存旧档覆盖不了恢复的存档）
logs = [];
page.storage.setItem(KEY, page.memSave);
assert.equal(page.storage.getItem(KEY), "AL8A-RESTORED", "恢复后自动保存应被保护拦截");

// 重启游戏：卸载 flush → 保护拦截 → 恢复的存档保留
logs = [];
unloadFlush(page);
assert.equal(page.storage.getItem(KEY), "AL8A-RESTORED", "卸载 flush 不能覆盖恢复的存档");

// 第 2 页（重启后）：游戏读取 → 应读到恢复的存档
page = newPage(realStorage);
const read = page.storage.getItem(KEY);
assert.equal(read, "AL8A-RESTORED", "重启后游戏应读到恢复的存档");
console.log("✅ 删除→恢复→重启流程全部通过；日志：" + logs.length + " 条");

// ── v2.74 场景：游戏从未读取过存档（__saveTouched=false，主菜单/新开档早期）──
// 反编译确认（DataHandler.as）：主菜单启动不读存档，首次 LoadGame/SaveGame/DeleteSave
// 才 SharedObject.getLocal 创建实例。包装器在 get/set/remove 上标记 __saveTouched。
realStorage.clear();
page = newPage(realStorage);
page.__saveTouched = false;
// 存档管理：恢复（未触碰 → 直接写回，无保护/无墓碑/无重启）
logs = [];
page.storage.setItem(KEY, "AL8A-RESTORED-UNTOUCHED");
assert.equal(page.storage.getItem(KEY), "AL8A-RESTORED-UNTOUCHED");
assert.equal(page.__saveTouched, false, "未触碰（无任何游戏访问）");
// 游戏首次读档（LoadGame → SharedObject.getLocal → getItem）→ 创建实例 → 读到恢复的存档
const gameLoad = page.storage.getItem(KEY);
assert.equal(gameLoad, "AL8A-RESTORED-UNTOUCHED", "游戏首次读档读到恢复的存档（无需重启）");
page.__saveTouched = true;   // 此后已触碰
// 游戏自动保存 → 正常写回（未触碰路径不设保护/墓碑 → 新档正常保存）
logs = [];
page.storage.setItem(KEY, "AL8A-NEW-GAME-SAVE");
assert.equal(page.storage.getItem(KEY), "AL8A-NEW-GAME-SAVE", "读档后自动保存正常");
console.log("✅ v2.74 未触碰免重启流程通过；日志：" + logs.length + " 条");
