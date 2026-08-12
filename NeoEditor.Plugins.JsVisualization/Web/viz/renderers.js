'use strict';
/* D09 P1 / D10: 类型渲染器 —— ItemType（D04）/ Creature（D05）/ Recipe / 薄类型模板。
 * 输入 = /viz/data 快照 JSON（C# 语义已预本地化），输出 = 组件树。
 * 布局统一模板（D10 §二）：TopBar → Hero → 问题区（Section 两列）→ RefPanel → 原始 XML。
 * 包在 IIFE 内（顶层解构 const { el } 会与 app.js 重复声明冲突）。 */

(() => {
const { el, fmtP, chip, badge, badgeRow, section, twoCol, hero, valueGrid, statBar,
        lootTree, refPanel, details, bindExpand, lightGrid } = window.VizComponents;

/// 交互桥（经 window.VizActions，app.js 挂载）
function postAction(kind, entityType, entityId) {
  if (window.VizActions) window.VizActions.postAction(kind, entityType, entityId);
}

// ═══════════════ 共用：攻击模式行 + 展开详情（D04/D05 三层）═══════════════

function attackModeRow(mode) {
  const detail = attackModeDetail(mode);
  const arrow = el('span', { class: 'am-arrow', text: mode.resolved ? '▶' : '' });

  const row = el('div', { class: 'am-row' + (mode.resolved ? '' : ' unresolved'), dataset: {} }, [
    el('span', { class: 'am-name' + (mode.resolved ? '' : ' unresolved'), text: mode.name }),
    statBar(mode.damageBar),
    el('span', { class: 'am-meta', text: mode.meta || '' }),
    arrow,
  ]);
  if (mode.resolved) {
    row.classList.add('navigable');
    row.setAttribute('data-nav', '1');
    row.addEventListener('click', (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        postAction('navigate', 'AttackMode', mode.targetId ?? mode.name);
      }
    });
  }
  // P2 §3.2: 展开状态记忆（bindExpand 统一协议，.open 控制 am-detail 显示）
  const wrap = el('div', { class: 'am-wrap', dataset: { expandKey: `am:${mode.name}` } }, [row, detail]);
  if (mode.resolved) {
    bindExpand(wrap, `am:${mode.name}`);
    wrap.addEventListener('click', (e) => {
      if (e.ctrlKey || e.metaKey) return;
      arrow.textContent = wrap.classList.contains('open') ? '▼' : '▶';
    });
  }
  return wrap;
}

function attackModeDetail(mode) {
  const box = el('div', { class: 'am-detail' });
  const top = el('div', { class: 'row am-top' });
  if (mode.image) top.append(el('img', { class: 'am-icon', src: mode.image, alt: '' }));
  if (mode.typeLabel) top.append(el('span', { class: 'chip', style: 'background:#ECEFF1;color:#555', text: mode.typeLabel }));
  if (mode.moraleText) top.append(el('span', { class: 'am-morale', style: `color:${mode.moraleColor}`, text: mode.moraleText }));
  if (mode.effectiveText) top.append(el('span', { class: 'am-effective', text: mode.effectiveText }));
  box.append(top);
  if (mode.formulaNote) box.append(el('div', { class: 'am-formula', text: mode.formulaNote }));
  if (mode.statCells && mode.statCells.length) box.append(valueGrid(mode.statCells, 3));
  if (mode.chargeBadges && mode.chargeBadges.length) {
    box.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '弹药' }), el('div', { class: 'row' }, mode.chargeBadges.map(badge))]));
  }
  if (mode.attackerConditions && mode.attackerConditions.length) {
    box.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '攻击者条件' }), el('div', { class: 'row' }, mode.attackerConditions.map(badge))]));
  }
  if (mode.wieldPhrase) box.append(el('div', { class: 'am-phrase', text: `“${mode.wieldPhrase}”` }));
  if (mode.attackPhrases && mode.attackPhrases.length) {
    box.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '攻击短语' }),
      el('div', { class: 'row' }, mode.attackPhrases.map(p => el('span', { class: 'chip', style: 'background:#E3F2FD;color:#1565C0', text: p })))]));
  }
  if (mode.notes) box.append(el('div', { class: 'am-notes', text: mode.notes }));
  box.append(el('div', { class: 'am-hint', text: 'Ctrl+点击 跳转 / Ctrl+右键 预览' }));
  return box;
}

function combatSection(combat, title, accent, icon) {
  if (!combat) return null;   // 无攻击模式的物品/生物：combat 为 null
  const body = el('div', {});
  if (combat.totalBar) body.append(statBar(combat.totalBar));
  if (combat.totalEffective) body.append(el('div', { class: 'value-row' }, [el('span', { class: 'value-label', text: '有效伤害' }), el('span', { class: 'value-num', style: 'color:#9575CD', text: combat.totalEffective })]));
  if (combat.fistsOnlyNote) body.append(el('div', { class: 'am-notes', text: combat.fistsOnlyNote }));
  for (const m of combat.modes) body.append(attackModeRow(m));
  return body.children.length ? section(title, { icon, accent }, body) : null;
}

// ═══════════════ ItemType（D04）═══════════════

function renderItemType(s) {
  const sem = s.semantics;

  const heroBadges = [
    el('span', { class: 'chip', style: 'background:#E3F2FD;color:#1565C0;font-weight:700', text: sem.gs }),
  ];
  const heroEl = hero(s, { badges: heroBadges, subtitle: sem.description, stats: sem.heroStats });
  const parts = [heroEl];

  // 情境 1：⚔ 战斗 | 🧍 装备
  let equip = null;
  if (sem.equipment) {
    const body = el('div', {});
    if (sem.equipment.slots && sem.equipment.slots.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '装备槽位' }), el('div', { class: 'row' }, sem.equipment.slots.map(badge))]));
    if (sem.equipment.useSlots && sem.equipment.useSlots.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '使用槽位' }), el('div', { class: 'row' }, sem.equipment.useSlots.map(badge))]));
    if (sem.equipment.socketLocked)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'row' }, [el('span', { class: 'chip', style: 'background:#FFEBEE;color:#C62828', text: '插槽锁定' })] )]));
    if (sem.equipment.sound)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '音效' }), el('div', { class: 'row' }, [el('span', { class: 'chip', style: 'background:#ECEFF1;color:#546E7A', text: sem.equipment.sound })])]));
    equip = section('装备', { icon: '🧍', accent: '#1565C0' }, body);
  }
  parts.push(twoCol(combatSection(sem.combat, '战斗', '#C62828', '⚔'), equip));

  // 情境 2：✨ 效果 | ⏳ 生命周期
  let effects = null;
  if (sem.conditionGroups && sem.conditionGroups.length) {
    const body = el('div', {});
    for (const g of sem.conditionGroups) {
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: g.label }), el('div', { class: 'row' }, g.conditions.map(badge))]));
    }
    if (sem.properties && sem.properties.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '属性' }), el('div', { class: 'row' }, sem.properties.map(badge))]));
    effects = section('效果', { icon: '✨', accent: '#E65100' }, body);
  }

  let lifecycle = null;
  if (sem.lifecycle) {
    const lc = sem.lifecycle;
    const body = el('div', {});
    if (lc.durability) body.append(statBar(lc.durability));
    if (lc.lossRates && lc.lossRates.length) body.append(valueGrid(lc.lossRates, 3));
    if (lc.lifespan) body.append(el('div', { class: 'value-row' }, [el('span', { class: 'value-label', text: '寿命推演' }), el('span', { class: 'value-num', style: 'color:#546E7A', text: lc.lifespan })]));
    for (const tree of lc.breakParts) body.append(lootTree(tree));
    if (lc.chargeProfiles && lc.chargeProfiles.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '弹药' }), el('div', { class: 'row' }, lc.chargeProfiles.map(badge))]));
    lifecycle = section('生命周期', { icon: '⏳', accent: '#6A1B9A' }, body);
  }
  parts.push(twoCol(effects, lifecycle));

  // 情境 3：📦 容器 | 🔗 来源产出
  let container = null;
  if (sem.container) {
    const ct = sem.container;
    const body = el('div', {});
    if (ct.capacity) body.append(el('div', { class: 'value-row' }, [el('span', { class: 'value-label', text: '容量' }), el('span', { class: 'value-num', style: 'color:#546E7A', text: ct.capacity })]));
    if (ct.contentIds && ct.contentIds.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '可容纳' }), el('div', { class: 'row' }, ct.contentIds.map(badge))]));
    if (ct.format) body.append(el('div', { class: 'value-row' }, [el('span', { class: 'value-label', text: '格式' }), el('span', { class: 'value-num', text: ct.format })]));
    container = section('容器', { icon: '📦', accent: '#00695C' }, body);
  }

  let associations = null;
  if (sem.associations) {
    const as = sem.associations;
    const body = el('div', {});
    if (as.switches && as.switches.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '切换状态' }), el('div', { class: 'row' }, as.switches.map(badge))]));
    for (const tree of as.lootTrees) body.append(lootTree(tree));
    associations = section('来源与产出', { icon: '🔗', accent: '#283593' }, body);
  }
  parts.push(twoCol(container, associations));

  parts.push(refPanel(sem.refs));
  parts.push(details(s.rawXml));
  return parts.filter(Boolean);
}

// ═══════════════ Creature（D05）═══════════════

function renderCreature(s) {
  const sem = s.semantics;

  const heroEl = hero(s, {
    badges: sem.heroBadges.map(b => badge(b)),
    subtitle: sem.namePublic,
    stats: null,
  });
  const parts = [heroEl];

  if (sem.notes) parts.push(el('div', { class: 'notes-box', text: sem.notes }));

  // 情境 1：⚔ 战斗 | 🧬 属性与出场状态
  let attributes = null;
  if (sem.attributeCells && sem.attributeCells.length) {
    const body = el('div', {});
    body.append(valueGrid(sem.attributeCells));
    if (sem.spawnStatus && sem.spawnStatus.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '出场状态' }), el('div', { class: 'row' }, sem.spawnStatus.map(badge))]));
    if (sem.activities && sem.activities.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '日常行为' }), el('div', { class: 'row' }, sem.activities.map(a => el('span', { class: 'chip', style: 'background:#E8EAF6;color:#283593', text: a })))]));
    attributes = section('属性与出场状态', { icon: '🧬', accent: '#6A1B9A' }, body);
  }

  const combatBody = el('div', {});
  if (sem.combat) {
    if (sem.combat.totalBar) combatBody.append(statBar(sem.combat.totalBar));
    if (sem.combat.totalEffective) combatBody.append(el('div', { class: 'value-row' }, [el('span', { class: 'value-label', text: '有效伤害' }), el('span', { class: 'value-num', style: 'color:#9575CD', text: sem.combat.totalEffective })]));
    if (sem.combat.fistsOnlyNote) combatBody.append(el('div', { class: 'am-notes', text: sem.combat.fistsOnlyNote }));
    for (const m of sem.combat.modes) combatBody.append(attackModeRow(m));
  }
  if (sem.factionRelation) combatBody.append(statBar(sem.factionRelation));
  const combat = combatBody.children.length ? section('战斗', { icon: '⚔', accent: '#C62828' }, combatBody) : null;
  parts.push(twoCol(combat, attributes));

  // 情境 2：🎁 战利品 | 📍 遭遇
  let loot = null;
  if (sem.lootPools && sem.lootPools.length) {
    const body = el('div', {});
    for (const pool of sem.lootPools) {
      body.append(el('div', { class: 'am-sub' }, [
        el('div', { class: 'am-sub-label', text: pool.label }),
        pool.tree ? lootTree(pool.tree) : el('span', { class: 'chip', style: 'background:#F5F5F5;color:#999', text: pool.unresolvedId }),
      ]));
    }
    loot = section('战利品', { icon: '🎁', accent: '#2E7D32' }, body);
  }

  let encounters = null;
  if ((sem.encounterChain && sem.encounterChain.length) || (sem.appearsIn && sem.appearsIn.length) || (sem.spawnPoints && sem.spawnPoints.length)) {
    const body = el('div', {});
    if (sem.encounterChain && sem.encounterChain.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '出场事件链' }), el('div', { class: 'row' }, sem.encounterChain.map(badge))]));
    if (sem.appearsIn && sem.appearsIn.length)
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '会出现在' }), el('div', { class: 'row' }, sem.appearsIn.map(badge))]));
    if (sem.spawnPoints && sem.spawnPoints.length) {
      const rows = el('div', {});
      for (const sp of sem.spawnPoints) {
        const row = el('div', { class: 'spawn-row' }, [
          el('span', { class: 'spawn-name', text: sp.name }),
          el('span', { class: 'spawn-pos', text: sp.position }),
          el('span', { class: 'spawn-count', text: sp.countText }),
          el('span', { class: 'spawn-weight', text: sp.weightText }),
        ]);
        if (sp.targetType && sp.targetId) {
          row.classList.add('nav');
          row.setAttribute('data-nav', '1');
          row.append(el('span', { class: 'nav-arrow', text: '↗' }));
          row.addEventListener('click', (e) => {
            if (e.ctrlKey || e.metaKey) { e.preventDefault(); e.stopPropagation(); postAction('navigate', sp.targetType, sp.targetId); }
          });
          row.addEventListener('contextmenu', (e) => {
            if (e.ctrlKey || e.metaKey) { e.preventDefault(); e.stopPropagation(); postAction('peek', sp.targetType, sp.targetId); }
          });
        }
        rows.append(row);
      }
      body.append(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: '刷新点' }), rows]));
    }
    encounters = section('遭遇', { icon: '📍', accent: '#00695C' }, body);
  }
  parts.push(twoCol(loot, encounters));

  parts.push(refPanel(sem.refs));
  parts.push(details(s.rawXml));
  return parts.filter(Boolean);
}

// ═══════════════ Recipe ═══════════════

function renderRecipe(s) {
  const sem = s.semantics;

  const heroBadges = [];
  if (sem.type) heroBadges.push(el('span', { class: 'chip', style: 'background:#E8F5E9;color:#2E7D32;font-weight:700', text: sem.type }));
  if (sem.flags && sem.flags.length) heroBadges.push(el('span', { class: 'chip', style: 'background:#FFF3E0;color:#E65100', text: sem.flags.join(' · ') }));
  const parts = [hero(s, { badges: heroBadges, subtitle: sem.secretName, stats: sem.heroStats })];

  // 原料三组（Tools 橙 / Consumed 红 / Destroyed 粉）
  if (sem.ingredientGroups && sem.ingredientGroups.length) {
    const body = el('div', {});
    for (const g of sem.ingredientGroups) {
      body.append(el('div', { class: 'ing-group' }, [
        el('div', { class: 'am-sub-label', style: `color:${g.fg}`, text: g.label }),
        el('div', { class: 'ing-list' },
          g.items.map(item => el('div', { class: 'card ing-card' }, [
            el('div', { class: 'row' }, [
              el('span', { class: 'chip', style: `background:${g.bg};color:${g.fg}`, text: 'Ingredient' }),
              item.resolved
                ? el('span', { class: 'ing-name nav', text: item.name })
                : el('span', { class: 'ing-name unresolved', text: item.name }),
              item.qty ? el('span', { class: 'chip', style: 'background:#08000000;color:#666', text: `×${item.qty}` }) : null,
            ]),
            (item.required && item.required.length) || (item.forbidden && item.forbidden.length)
              ? el('div', { class: 'two-col ing-props' }, [
                  item.required && item.required.length
                    ? el('div', { class: 'ing-prop' }, [el('div', { class: 'am-sub-label', style: 'color:#2E7D32', text: '必需' }), el('div', { class: 'row' }, item.required.map(badge))])
                    : null,
                  item.forbidden && item.forbidden.length
                    ? el('div', { class: 'ing-prop' }, [el('div', { class: 'am-sub-label', style: 'color:#C62828', text: '禁止' }), el('div', { class: 'row' }, item.forbidden.map(badge))])
                    : null,
                ])
              : null,
          ]))),
      ]));
    }
    parts.push(section('原料', { icon: '🧪', accent: '#E65100' }, body));
  }

  // 产物：TT 树 + Temp Product
  const productParts = [];
  if (sem.product) productParts.push(lootTree(sem.product));
  if (sem.tempProduct && sem.tempProduct.length)
    productParts.push(el('div', { class: 'am-sub' }, [el('div', { class: 'am-sub-label', text: 'Temp Product Preview' }), el('div', { class: 'row' }, sem.tempProduct.map(badge))]));
  if (productParts.length) parts.push(section('产物', { icon: '📦', accent: '#2E7D32' }, el('div', {}, productParts)));

  if (sem.alsoTry && sem.alsoTry.length)
    parts.push(section('Also Try（替代配方）', { icon: '🔁', accent: '#6A1B9A' }, el('div', { class: 'row' }, sem.alsoTry.map(badge))));
  if (sem.hidden && sem.hidden.length)
    parts.push(section('隐藏配方', { icon: '🔒', accent: '#E65100' }, el('div', { class: 'row' }, sem.hidden.map(badge))));

  parts.push(refPanel(sem.refs));
  parts.push(details(s.rawXml));
  return parts.filter(Boolean);
}

// ═══════════════ 薄类型模板（D10 §3.8：C/D 级，零 per-type 渲染器）═══════════════

function renderTemplate(s) {
  const sem = s.semantics;
  const parts = [hero(s, {
    badges: (sem.heroBadges || []).map(b => badge(b)),
    subtitle: sem.subtitle,
    stats: sem.heroStats,
  })];

  for (const block of sem.blocks || []) {
    const body = el('div', {});
    if (block.rows && block.rows.length) body.append(valueGrid(block.rows));
    if (block.bars && block.bars.length) for (const b of block.bars) body.append(statBar(b));
    if (block.lightCells && block.lightCells.length) body.append(lightGrid(block.lightCells));
    if (block.mode) body.append(attackModeRow(block.mode));   // AttackMode 实体页：单模式行+详情
    if (block.badges && block.badges.length) body.append(el('div', { class: 'row' }, block.badges.map(badge)));
    for (const g of block.badgeGroups || []) {
      if (!g.badges || !g.badges.length) continue;
      body.append(el('div', { class: 'am-sub' }, [
        el('div', { class: 'am-sub-label', text: g.label }),
        el('div', { class: 'row' }, g.badges.map(badge)),
      ]));
    }
    for (const tree of block.trees || []) body.append(lootTree(tree));
    if (block.text) body.append(el('pre', { class: 'mono-text', text: block.text }));
    if (body.children.length) parts.push(section(block.title, { accent: block.accent }, body));
  }

  parts.push(refPanel(sem.refs));
  parts.push(details(s.rawXml));
  return parts.filter(Boolean);
}

// 渲染器注册表（app.js 按 snapshot.type 分发）：A/C 级专有渲染器 + B/D 级 17 个
// 全部走薄模板（D10 §3.8 零 per-type 渲染器；C# 侧语义聚合为 Blocks）。
window.VizRenderers = {
  ItemType: renderItemType,
  Creature: renderCreature,
  Recipe: renderRecipe,
  ContainerType: renderTemplate,
  BarterHex: renderTemplate,
  Map: renderTemplate,
  // B 级 7 个（语义迁移，区块 Section 化）
  AttackMode: renderTemplate,
  Condition: renderTemplate,
  TreasureTable: renderTemplate,
  HexType: renderTemplate,
  Faction: renderTemplate,
  BattleMove: renderTemplate,
  CampType: renderTemplate,
  // D 级 10 个（模板组合，保持薄）
  GameVar: renderTemplate,
  ItemProp: renderTemplate,
  Headline: renderTemplate,
  ForbiddenHex: renderTemplate,
  ChargeProfile: renderTemplate,
  Ingredient: renderTemplate,
  DmcPlace: renderTemplate,
  CreatureSource: renderTemplate,
  EncounterTrigger: renderTemplate,
  DataFile: renderTemplate,
};
})();
