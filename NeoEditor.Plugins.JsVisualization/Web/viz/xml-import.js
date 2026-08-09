'use strict';
/* D09: 页面内 XML 语义提取（通用渲染器：XML 文本传入 → 快照对象，零后端依赖）。
 *
 * 与 C# EncounterSemanticsExtractor 产出**同构**的快照对象（页面渲染器零改动）；
 * 差异（单文件限制，UI 上有提示）：
 *  - 无前驱反查（需要全表）→ 前驱层显示 ⛳ 入口 + 单文件提示
 *  - 无触发器反查（需要 EncounterTrigger 表）
 *  - 物品/条件未解析 → 灰色 "Item #id" / "Condition #id" 徽章
 *
 * 应用内主通道仍是 /viz/data（C# 全量语义含全表反查）；本模块用于：
 *  - 调试：把游戏 XML 文本直接传入页面渲染（NeoViz.renderXml / 拖拽 / ?xml=）
 *  - 静态/浏览器独立环境兜底
 */

// 表名 → 实体类型（v1 仅 encounters 全语义；其余类型返回 null 走 rawXml 兜底）
function tableNameToType(tableName) {
  const map = { encounters: 'Encounter' };
  return map[String(tableName || '').toLowerCase()] ?? null;
}

function xmlTypeChip(rawType) {
  switch (Number(rawType) || 0) {
    case 0: return { label: '剧情', bg: '#E3F2FD', fg: '#1565C0' };
    case 1: return { label: '搜刮', bg: '#FFF3E0', fg: '#E65100' };
    case 2: return { label: '战斗', bg: '#FFEBEE', fg: '#C62828' };
    case 3: return { label: '破解', bg: '#F3E5F5', fg: '#6A1B9A' };
    default: return { label: '类型 ' + rawType, bg: '#F5F5F5', fg: '#999' };
  }
}

function xmlFmtP(p) {
  const clamped = Math.max(0, Math.min(1, Number(p) || 0));
  return (clamped * 100).toFixed(clamped * 100 % 1 === 0 ? 0 : 1).replace(/\.0$/, '') + '%';
}

// D07 响应语法：段 = [物品x数量(+物品x数量)]=[目标]x[权重]x[p2]x[p3]x[p4]
function xmlParseResponses(raw, currentId) {
  if (!raw) return [];
  const segments = [];
  let totalWeight = 0;

  for (const seg of String(raw).split(',')) {
    const s = seg.trim();
    if (!s) continue;
    const eqIdx = s.indexOf('=');
    let targetId, weight = 1, destroy = false;
    const items = [];

    if (eqIdx < 0) { // 无 '=' 兼容
      const parts = s.split('x');
      if (parts.length < 2) continue;
      targetId = parseInt(parts[0], 10);
      weight = parseFloat(parts[1]) || 1;
    } else {
      if (eqIdx > 0) { // 物品前缀（+ 连接 = AND）
        let itemPart = s.slice(0, eqIdx).trim();
        if (itemPart.endsWith('x')) itemPart = itemPart.slice(0, -1);
        for (const piece of itemPart.split('+')) {
          const p = piece.trim();
          if (!p) continue;
          const ip = p.split('x');
          const id = ip[0].trim();
          const mult = parseFloat(ip[1]) || 1;
          items.push({ itemId: id, mult, isAnd: items.length > 0 });
        }
      }
      const ep = s.slice(eqIdx + 1).trim().split('x');
      if (ep.length < 2) continue;
      targetId = parseInt(ep[0], 10);
      weight = parseFloat(ep[1]) || 1;
      destroy = ep[2] === '1';
    }
    if (Number.isNaN(targetId)) continue;

    // 无物品段 = 默认响应（占位 null）
    if (items.length === 0) items.push({ itemId: null, mult: 1, isAnd: false });

    segments.push({ targetId, weight, destroy, items });
    totalWeight += weight;
  }

  // 合并同目标 + 概率归一 + 终止语义（D07 §3.1：自指=停留优先，目标1=无后续）
  const merged = new Map();
  for (const seg of segments) {
    if (!merged.has(seg.targetId)) merged.set(seg.targetId, { targetId: seg.targetId, weight: 0, items: [] });
    const m = merged.get(seg.targetId);
    m.weight += seg.weight;
    m.items.push(...seg.items);
  }

  return [...merged.values()].map(m => {
    const endKind = m.targetId === currentId ? 'stay'
      : (m.targetId === 1 && currentId !== 1) ? 'blank' : 'none';
    const badges = m.items.filter(i => i.itemId).map(i => ({
      icon: '🛡',
      text: `Item #${i.itemId}${i.mult > 1 ? ' ×' + i.mult : ''}${m.destroy ? '（消耗）' : ''}`,
      bg: '#F5F5F5', fg: '#999', targetType: null, targetId: null, tooltip: null,
    }));
    const annotation = badges.map(b => b.text).join(' ｜ ') || null;
    return {
      targetId: m.targetId,
      entityId: null,
      displayName: `Enc #${m.targetId}`,
      typeChip: { label: `Enc #${m.targetId}`, bg: '#F5F5F5', fg: '#999' },
      resolved: false,
      endKind,
      weight: m.weight,
      effectiveProb: totalWeight > 0 ? m.weight / totalWeight : 0,
      successProb: null,
      annotation,
      itemBadges: badges,
      preConds: [],
    };
  });
}

// 效果区（D08 §五）：单文件模式徽章全部未解析（灰色）
function xmlBuildEffects(cols) {
  const rows = [];
  const chip = (label, bg, fg) => ({ label, bg, fg });
  const badge = (text, bg, fg) => ({ icon: null, text, bg, fg, targetType: null, targetId: null, tooltip: null });

  if (cols.vLoot && cols.vLoot !== '0' && cols.vLoot !== '3')
    rows.push({ label: chip('🎁 获得战利品', '#E8F5E9', '#2E7D32'), badges: [badge('TT #' + cols.vLoot, '#E8F5E9', '#2E7D32')], text: null });
  if (cols.nTreasureID && cols.nTreasureID !== '3')
    rows.push({ label: chip('🎁 战利品池', '#E8F5E9', '#2E7D32'), badges: [badge('TT #' + cols.nTreasureID, '#E8F5E9', '#2E7D32')], text: null });
  if (cols.nItemsID && cols.nItemsID !== '3')
    rows.push({ label: chip('📦 给予物品', '#E3F2FD', '#1565C0'), badges: [badge('Item #' + cols.nItemsID, '#E3F2FD', '#1565C0')], text: null });
  if (Number(cols.fPrice))
    rows.push({ label: chip('💰 费用', '#F3E5F5', '#6A1B9A'), badges: [], text: '$' + Number(cols.fPrice).toFixed(2) });
  if (cols.nRemoveTreasureID && cols.nRemoveTreasureID !== '3')
    rows.push({ label: chip('🗑 移除战利品', '#FFEBEE', '#C62828'), badges: [badge('TT #' + cols.nRemoveTreasureID, '#FFEBEE', '#C62828')], text: null });
  if (cols.ptTeleport && cols.ptTeleport !== '0,0')
    rows.push({ label: chip('📍 传送至', '#E0F2F1', '#00695C'), badges: [], text: '(' + cols.ptTeleport + ')' });
  if (cols.nCreatureID && cols.nCreatureID !== '0') {
    const radius = (cols.ptCreatureHex && cols.ptCreatureHex !== '0,0') ? '（半径 ' + cols.ptCreatureHex + '）' : null;
    rows.push({ label: chip('🐾 刷出', '#FFF3E0', '#E65100'), badges: [badge('Creature #' + cols.nCreatureID, '#E8EAF6', '#283593')], text: radius });
  }
  if (cols.vAccidents && cols.vAccidents !== '1') {
    const badges = String(cols.vAccidents).split(',').map(s => s.trim()).filter(Boolean)
      .map(s => badge(s, '#FFEBEE', '#C62828'));
    rows.push({ label: chip('💥 意外', '#FFEBEE', '#C62828'), badges, text: null });
  }
  const mapBadges = [];
  if (cols.aMinimapHexes) {
    // 坐标 "5,5" 与条目共用逗号 → 按分号/等号尽力拆
    for (const seg of String(cols.aMinimapHexes).split(/[;；]/)) {
      const s = seg.trim();
      if (!s) continue;
      const eqIdx = s.indexOf('=');
      const pos = eqIdx > 0 ? s.slice(0, eqIdx).trim() : s;
      const label = eqIdx > 0 ? s.slice(eqIdx + 1).trim() : null;
      mapBadges.push(badge(label ? `📍(${pos}) ${label}` : `📍(${pos})`, '#FFF8E1', '#F57F17'));
    }
  }
  if (cols.ptEditor && cols.ptEditor !== '0,0')
    mapBadges.push(badge('✏️(' + cols.ptEditor + ')', '#F5F5F5', '#999'));
  if (mapBadges.length)
    rows.push({ label: chip('🗺 地图标注', '#ECEFF1', '#546E7A'), badges: mapBadges, text: null });

  return rows.length ? { rows } : null;
}

// 入口区（D08 §四）：条件未解析（灰色）+ 无触发器（单文件限制）
function xmlBuildEntry(cols) {
  const entry = { conditions: [], ownPreConditions: [], triggers: [] };
  const condSegs = String(cols.aConditions || '').split(',')
    .map(s => s.trim()).filter(s => s && s !== '1' && s !== '0');
  for (const seg of condSegs)
    entry.conditions.push({ icon: null, text: 'Condition #' + seg, bg: '#F5F5F5', fg: '#999', targetType: null, targetId: null, tooltip: null });
  const preSegs = String(cols.aPreConditions || '').split(',')
    .map(s => s.trim()).filter(Boolean);
  for (const seg of preSegs) {
    const isNeg = seg.startsWith('-');
    entry.ownPreConditions.push({
      icon: null, text: (isNeg ? 'NOT ' : '') + (isNeg ? seg.slice(1) : seg),
      bg: isNeg ? '#FFEBEE' : '#E8F5E9', fg: isNeg ? '#C62828' : '#2E7D32',
      targetType: null, targetId: null, tooltip: null,
    });
  }
  const hasContent = entry.conditions.length || entry.ownPreConditions.length;
  return hasContent ? entry : null;
}

/// 解析单个 encounters XML → 与 C# 快照同构的对象（渲染器零改动）。不支持的表返回 null。
function extractEncounterFromXml(xmlText) {
  const doc = new DOMParser().parseFromString(xmlText, 'application/xml');
  const table = doc.querySelector('table');
  const type = tableNameToType(table?.getAttribute('name'));
  if (!type) return null;

  const cols = {};
  table.querySelectorAll('column').forEach(c => {
    cols[c.getAttribute('name')] = c.textContent ?? '';
  });

  const id = String(cols.id ?? '');
  const desc = String(cols.strDesc ?? '');
  const branches = xmlParseResponses(cols.aResponses, Number(id));
  const isTerminal = branches.length > 0 && branches.every(b => b.endKind !== 'none');

  return {
    type,
    id,
    displayName: String(cols.strName || '').trim() || `Enc #${id}`,
    image: null,
    rawXml: xmlText,
    semantics: {
      typeChip: xmlTypeChip(cols.nType),
      isEntry: true,                 // 单文件无全表 → 前驱未知，按入口处理
      isTerminal,
      removeCreatures: cols.bRemoveCreatures === 'True' || cols.bRemoveCreatures === 'true' || cols.bRemoveCreatures === '1',
      removeUsed: cols.bRemoveUsed === 'True' || cols.bRemoveUsed === 'true' || cols.bRemoveUsed === '1',
      price: Number(cols.fPrice) || 0,
      lootChance: Number(cols.fLootChance) || 0,
      accidentChance: Number(cols.fAccidentChance) || 0,
      creatureChance: Number(cols.fCreatureChance) || 0,
      description: desc.length > 2000 ? desc.slice(0, 2000) + '…' : (desc || null),
      formatHint: '单文件 XML 模式：前驱/触发器不可用（无全表反查）；物品/条件未解析显示灰色 #id。',
      flow: { predecessors: [], branches, preCondFilters: [] },
      effects: xmlBuildEffects(cols),
      entry: xmlBuildEntry(cols),
    },
  };
}
