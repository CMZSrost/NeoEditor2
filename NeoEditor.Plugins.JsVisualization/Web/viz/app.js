'use strict';
/* D09 JS 可视化 — 渲染引擎（零宿主依赖）
 * 版本标记：右下角小字显示构建时间戳，便于确认编辑器加载的是否最新页面。 */
const VIZ_VERSION = '20260809-1830';
/* 契约：/viz/data 返回 EntitySnapshotDto JSON（C# 侧语义提取、字符串已本地化），
 * 本页只做布局与交互 —— D09 原则②。页面可在 WebView2 与独立浏览器中一致运行：
 * 数据全部经相对路径 fetch；chrome.webview 仅作可选增强。
 *
 * 结构（D10 统一模板）：TopBar → Hero → 问题区（类型渲染器）→ RefPanel → 原始 XML。
 * 类型渲染器注册表在 renderers.js（window.VizRenderers）；组件库在 components.js。
 *
 * Encounter 交互（对照 RefNode.WireNavigation / D08 v1.3）：
 *  - 左键点击前驱/后继卡  = 组件内焦点切换（重算其前后文）
 *  - 「⏎ 回到当前」       = 焦点复位到最初查看的场景
 *  - Ctrl+左键 徽章/卡    = 页面跳转到目标实体（/viz/action navigate）
 *  - Ctrl+右键 徽章/卡    = Peek 预览（/viz/action peek）
 * 包在 IIFE 内：顶层 const { el } 解构与 renderers.js 重复声明冲突；postAction
 * 经 window.VizActions 桥给组件库/渲染器。 */

(() => {
const { el, fmtP, chip, badge, badgeRow, section, twoCol, hero, valueGrid, statBar,
        lootTree, topBar, refPanel, details, imageBlock, bindTip, restoreExpands } = window.VizComponents;

const app = () => document.getElementById('app');

// ─────────────────────────── 状态 ───────────────────────────

const state = {
  type: 'Encounter',
  rootId: null,      // 最初查看的场景（「回到当前」锚点）
  currentId: null,   // 当前焦点场景
  preConds: new Set(),
  animateFlow: false,  // 焦点切换动画（初次加载不动画，交互切换时开）
  pendingTargetId: null, // 本次切换点击的目标卡 id（平移/淡入动画用）
  sample: null,       // sample 模式：焦点切换读本地 samples/<type><id>.json
  navStack: [],       // P2 §3.1: 组件内导航历史（← 返回逐级回退）
  snapshotCache: new Map(),  // P2 §3.1: id → snapshot（返回不重新 fetch）
};

const sleep = ms => new Promise(r => setTimeout(r, ms));
const easeInOut = t => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;

/// JS 驱动动画（不依赖 CSS transition——离屏模式 WebView2 合成器不播放 transition，
/// setTimeout 插值任何环境强制播放）。
function animateJs(duration, onFrame, easing = easeInOut) {
  return new Promise(resolve => {
    const start = performance.now();
    const step = () => {
      const p = Math.min(1, (performance.now() - start) / duration);
      onFrame(easing(p));
      if (p < 1) setTimeout(step, 16);
      else resolve();
    };
    step();
  });
}

// ─────────────────────────── API ───────────────────────────

/// 快照获取：缓存优先（P2 §3.1 —— 组件内导航返回不重新 fetch）。
async function fetchSnapshot(id) {
  const key = String(id);
  if (state.snapshotCache.has(key)) return state.snapshotCache.get(key);
  // sample 模式（独立静态服务器/无后端）：切换目标从本地 samples/ 取；
  // 正式环境（WebView2 回环 /viz/data）走同源端点。
  let s;
  if (state.sample) {
    const res = await fetch(`samples/${state.type.toLowerCase()}${id}.json`);
    if (!res.ok) throw new Error(`samples/${state.type.toLowerCase()}${id}.json → ${res.status}`);
    s = await res.json();
  } else {
    const params = new URLSearchParams({ type: state.type, id });
    if (state.preConds.size > 0) params.set('pre', [...state.preConds].join(','));
    const res = await fetch('/viz/data?' + params);
    if (!res.ok) throw new Error(`/viz/data → ${res.status}`);
    s = await res.json();
  }
  state.snapshotCache.set(key, s);
  return s;
}

async function fetchSnapshotFromXml(type, xml) {
  const params = new URLSearchParams({ type, xml });
  const res = await fetch('/viz/data?' + params);
  if (!res.ok) throw new Error(`/viz/data(xml) → ${res.status}`);
  return res.json();
}

/// 交互桥：POST /viz/action 为主（零宿主依赖，决策 8）；fetch 失败时回退
/// chrome.webview.postMessage（P2 增强通道，与 POST 同一协议、同一 Handler）。
async function postAction(kind, entityType, entityId) {
  const body = JSON.stringify({ kind, entityType, entityId });
  try {
    const res = await fetch('/viz/action', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    });
    if (res.ok) return;
  } catch (_) { /* 独立浏览器环境无 /viz/action —— 静默 */ }
  try {
    window.chrome?.webview?.postMessage(body);
  } catch (_) { /* 无宿主桥（纯浏览器）—— 静默 */ }
}

// 交互桥挂载（组件库/渲染器的 postAction 经此路由）
window.VizActions = { postAction };

// ─────────────────────────── 状态记忆（P2 §3.2：sessionStorage）───────────────────────────

/// 展开状态 + 滚动位置：sessionStorage['jsv:ui:{type}:{id}']，跨实体重渲染保留。
const uiState = {
  expanded: new Set(),
  scrollY: 0,
  loaded: false,
};

// 键 = 文档实体（rootId）：流转焦点切换属文档内导航，展开/滚动状态属于整个文档
const uiKey = () => (state.type && state.rootId) ? `jsv:ui:${state.type}:${state.rootId}` : null;

function loadUiState() {
  uiState.expanded.clear();
  uiState.scrollY = 0;
  uiState.loaded = true;
  const k = uiKey();
  if (!k) return;
  try {
    const raw = sessionStorage.getItem(k);
    if (!raw) return;
    const data = JSON.parse(raw);
    uiState.scrollY = Number(data.scrollY) || 0;
    for (const key of data.expanded || []) uiState.expanded.add(key);
  } catch (_) { /* 损坏的状态忽略 */ }
}

function saveUiState() {
  const k = uiKey();
  if (!k || !uiState.loaded) return;
  try {
    sessionStorage.setItem(k, JSON.stringify({
      scrollY: uiState.scrollY,
      expanded: [...uiState.expanded],
    }));
  } catch (_) { /* sessionStorage 不可用（隐私模式）忽略 */ }
}

let scrollTimer = null;
function initScrollMemory() {
  window.addEventListener('scroll', () => {
    uiState.scrollY = window.scrollY;
    clearTimeout(scrollTimer);
    scrollTimer = setTimeout(saveUiState, 500);   // debounce 500ms（D10 §3.2）
  }, { passive: true });
}

// 组件库经此读写展开状态（bindExpand / restoreExpands / details）
window.VizUiState = {
  expanded: uiState.expanded,
  toggle: (key, open) => {
    if (open) uiState.expanded.add(key); else uiState.expanded.delete(key);
    saveUiState();
  },
};

// ─────────────────────────── 交互：导航 / peek ───────────────────────────

function bindNav(element, entityType, entityId) {
  element.setAttribute('data-nav', '1');
  element.addEventListener('click', (e) => {
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      e.stopPropagation();
      postAction('navigate', entityType, entityId);
    }
  });
  element.addEventListener('contextmenu', (e) => {
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      e.stopPropagation();
      postAction('peek', entityType, entityId);
    }
  });
}

// 组件内焦点切换（D08 v1.3：左键点击前驱/后继卡 → 视角平移到目标并重算其前后文）。
// 点击瞬间立即淡化（fetch 并行）——消除"点了没反应"的间隙。
function bindFocusSwitch(element, targetId) {
  element.addEventListener('click', (e) => {
    if (e.ctrlKey || e.metaKey) return;
    const tid = String(targetId);
    // 立即视觉反馈：非目标卡先直接压到低透明度（fetch 并行，动画在 load 里续播）
    document.querySelectorAll('.flow-track .node-card').forEach(c => {
      if (c.dataset.id !== tid) c.style.opacity = '0.12';
    });
    pushNav(state.currentId ?? state.rootId);   // P2 §3.1: 记录来路（← 返回逐级回退）
    state.currentId = targetId;
    state.pendingTargetId = tid;
    state.animateFlow = true;
    load();
  });
}

// P2 §3.1: 组件内导航历史 —— 来源 id 入栈（去重连续相同 id）
function pushNav(sourceId) {
  if (sourceId == null) return;
  const key = String(sourceId);
  if (state.navStack[state.navStack.length - 1] === key) return;
  state.navStack.push(key);
}

/// ← 返回：逐级回退到上一场景（快照缓存命中，不重新 fetch；无动画直接重建流转区）。
function goBack() {
  const prev = state.navStack.pop();
  if (prev == null) return;
  state.currentId = prev;
  state.pendingTargetId = String(prev);
  state.animateFlow = false;
  load();
}

/// P2 §3.1: 流转区局部重渲染后同步 TopBar 返回按钮（renderSnapshot 全量重建时自带）。
function updateTopBar() {
  const bar = document.querySelector('.topbar');
  if (!bar) return;
  const hasBack = !!bar.querySelector('.topbar-back');
  if (state.navStack.length > 0 && !hasBack) {
    const back = el('button', { class: 'back-btn topbar-back', text: '← 返回' });
    back.addEventListener('click', goBack);
    bar.prepend(back);
  } else if (state.navStack.length === 0 && hasBack) {
    bar.querySelector('.topbar-back').remove();
  }
}

// ─────────────────────────── 节点卡（D06 §四 最终版 / R64） ───────────────────────────

function nodeCard(node, opts = {}) {
  const { isCurrent = false, weight, effectiveProb, filtered = false,
          annotation, unresolved = false, navigable = false, focusTarget = null, displayId = null } = opts;

  const card = el('div', {
    class: 'node-card' + (isCurrent ? ' current' : '') + (filtered ? ' filtered' : '') +
           (navigable ? ' navigable' : ''),
    dataset: node.id != null ? { id: String(node.id) } : {},
  });

  // 行1：类型 chip 在标题左边（R64）＋ 当前标记
  const titleRow = el('div', { class: 'node-title-row' });
  if (!unresolved) titleRow.append(chip(node.typeChip));
  if (isCurrent) titleRow.append(el('span', { class: 'node-current-tag', text: '📍 当前场景' }));
  titleRow.append(el('span', {
    class: 'node-title' + (unresolved ? ' unresolved' : ''),
    text: node.displayName ?? (node.id != null ? `Enc #${node.id}` : ''),
  }));
  card.append(titleRow);

  // 行2：图（主体 ~70% 宽）
  card.append(imageBlock('node-img', el('span', { class: 'placeholder', text: '📖' }), node.image));

  // 行3：ID chip（左）＋ 概率胶囊（右，分支卡才有）
  const bottom = el('div', { class: 'node-bottom' });
  bottom.append(el('span', { class: 'chip', style: 'background:#e3f2fd;color:#1565c0',
    text: `ID: ${displayId ?? node.id ?? ''}` }));
  if (!isCurrent && !opts.noProbPill && effectiveProb != null) {
    const color = effectiveProb >= 0.5 ? '#2e7d32' : effectiveProb >= 0.1 ? '#e65100' : '#999';
    bottom.append(el('span', { class: 'prob-pill', style: `background:${color}` },
      `${Number(weight ?? 0).toFixed(1)}(${fmtP(effectiveProb)})`));
  }
  card.append(bottom);

  // 底部行中间标注（R64：前驱的来路 / 后继的去路）
  if (annotation) card.append(el('div', { class: 'node-annotation', text: annotation }));

  // 交互
  if (navigable && node.id != null) {
    bindNav(card, node.type ?? 'Encounter', node.id);
    bindFocusSwitch(card, focusTarget ?? node.id);
  }
  if (opts.branchTip) bindTip(card, opts.branchTip, node.displayName);
  return card;
}

// 终点胶囊（D07 §3.2：停留 / 无后续 —— 永不渲染成卡片）
function endCapsule(branch) {
  const label = branch.endKind === 'stay' ? '⏹ 停留原地' : '☰ 无后续';
  return el('div', { class: 'end-capsule' },
    [el('span', { text: label }), el('span', { text: fmtP(branch.effectiveProb) })]);
}

// 分支 tooltip 摘要（D06 §四：前置满足 ✓/✗、物品触发、成功率、概率）
function branchTipText(branch) {
  const lines = [];
  const items = branch.itemBadges || [];
  if (items.length) {
    const groups = [];
    for (const b of items) groups.push((b.icon ? b.icon + ' ' : '') + b.text);
    lines.push(groups.join(' ｜ '));
  }
  if (branch.successProb != null && branch.successProb < 1)
    lines.push('⚡ 成功率 ' + fmtP(branch.successProb));
  const preConds = branch.preConds || [];
  if (preConds.length)
    lines.push(preConds.map(p => (p.satisfied ? '✓ ' : '✗ ') + p.label).join('  '));
  lines.push(`概率: ${Number(branch.weight).toFixed(1)}(${fmtP(branch.effectiveProb)})`);
  return lines.join('\n');
}

// ─────────────────────────── Hero（D08 §一，经统一 Hero 组件） ───────────────────────────

function renderHero(s) {
  const sem = s.semantics;
  const badges = [chip(sem.typeChip)];
  if (sem.isEntry && !sem.isTerminal) badges.push(el('span', { class: 'chip', style: 'background:#e8f5e9;color:#2e7d32', text: '⛳ 入口' }));
  if (sem.isTerminal && !sem.isEntry) badges.push(el('span', { class: 'chip', style: 'background:#eceff1;color:#546e7a', text: '⏹ 终点' }));
  if (sem.removeCreatures) badges.push(el('span', { class: 'chip', style: 'background:#ffebee;color:#c62828', text: 'RemoveCreatures' }));
  if (sem.removeUsed) badges.push(el('span', { class: 'chip', style: 'background:#ffebee;color:#c62828', text: 'RemoveUsed' }));

  const stats = [];
  if (sem.price !== 0) stats.push({ value: `Price: $${sem.price.toFixed(2)}` });
  if (sem.lootChance > 0) stats.push({ value: `Loot: ${fmtP(sem.lootChance)}`, color: '#2e7d32' });
  if (sem.accidentChance > 0) stats.push({ value: `Accident: ${fmtP(sem.accidentChance)}`, color: '#c62828' });
  if (sem.creatureChance > 0) stats.push({ value: `Creature: ${fmtP(sem.creatureChance)}`, color: '#283593' });

  return hero(s, { badges, stats });
}

// ─────────────────────────── ④ 内容与效果（D08 §五，两栏） ───────────────────────────

function renderEffects(effects) {
  if (!effects || !effects.rows || effects.rows.length === 0) return null;
  const rows = effects.rows.map(r => {
    const value = el('div', { class: 'value' });
    if (r.badges && r.badges.length) value.append(...r.badges.map(badge));
    if (r.text) value.append(el('span', { class: 'text', text: r.text }));
    const rowEl = el('div', { class: 'effect-row' }, [chip(r.label), value]);
    // P1: 战利品嵌套树（效果区 GiveLoot/LootPool 行的可展开树）
    if (r.trees && r.trees.length) {
      const treeBox = el('div', { class: 'effect-tree' });
      for (const t of r.trees) treeBox.append(lootTree(t));
      rowEl.append(treeBox);
    }
    return rowEl;
  });
  return el('div', { class: 'card' }, rows);
}

function renderContentEffects(s) {
  const left = s.semantics.description
    ? el('div', { class: 'card' }, el('div', { class: 'story-text', text: s.semantics.description }))
    : null;
  const right = renderEffects(s.semantics.effects);
  if (!left && !right) return null;

  const body = (left && right)
    ? el('div', { class: 'two-col' }, [left, right])
    : (left ?? right);
  return section('内容与效果', { icon: '📜', accent: '#e65100' }, body);
}

// ─────────────────────────── ② 场景流转（D08 §二，单轨道三行 ★） ───────────────────────────

/// 构建横向轨道（一个 .flow-scroll 包三行，整体横向滚动——D08 R64 结构）。
/// 焦点切换动画只替换轨道（Hero / 内容效果 / 入口区保持当前场景不动）。
function buildFlowTrack(s) {
  const flow = s.semantics.flow;

  // 行1：前驱层（谁通向这里）— 无入边 → ⛳ 入口
  const predRow = el('div', { class: 'flow-row', dataset: { row: 'pred' } });
  if (!flow.predecessors.length) {
    predRow.append(el('span', { class: 'chip', style: 'background:#e8f5e9;color:#2e7d32', text: '⛳ 入口' }));
  } else {
    for (const p of flow.predecessors) {
      predRow.append(nodeCard(p, { annotation: p.annotation ?? String(p.weight ?? ''), navigable: true }));
    }
  }

  // 行2：当前场景（居中高亮，不接导航）
  const currentRow = el('div', { class: 'flow-row', dataset: { row: 'current' } });
  currentRow.append(nodeCard({
    type: s.type, id: s.id, displayName: s.displayName, image: s.image, typeChip: s.semantics.typeChip,
  }, { isCurrent: true }));

  // 行3：后继层 — D07 §3.2 终点胶囊 / D06 分支卡（带概率胶囊 + 标注）
  const branchRow = el('div', { class: 'flow-row', dataset: { row: 'succ' } });
  if (!flow.branches.length) {
    branchRow.append(el('div', { style: 'text-align:center;color:#999;font-size:10px', text: '⏹ 终点 · 无后续分支' }));
  } else {
    for (const b of flow.branches) {
      if (b.endKind !== 'none') { branchRow.append(endCapsule(b)); continue; }
      branchRow.append(nodeCard({
        type: 'Encounter', id: b.entityId ?? String(b.targetId), displayName: b.displayName,
        image: b.image, typeChip: b.typeChip,
      }, {
        weight: b.weight, effectiveProb: b.effectiveProb,
        filtered: b.preConds.length > 0 && state.preConds.size > 0 && b.preConds.some(p => !p.satisfied),
        unresolved: !b.resolved, navigable: true,
        annotation: b.annotation,
        branchTip: branchTipText(b),
        displayId: String(b.targetId),   // ID chip 显示数字 id（导航键仍是 EntityId）
      }));
    }
  }

  return el('div', { class: 'flow-track' }, [predRow, currentRow, branchRow]);
}

/// 构建流转区 section（节标题 + 前置过滤 + 单横向滚动容器包轨道）。
function buildFlowSection(s) {
  const flow = s.semantics.flow;
  const backBtn = el('button', { class: 'back-btn', text: '⏎ 回到当前场景' });
  backBtn.addEventListener('click', () => {
    // 目标（根场景）不在当前视图 → 全部淡化，重建后新卡依次出现
    document.querySelectorAll('.flow-track .node-card').forEach(c => { c.style.opacity = '0.12'; });
    pushNav(state.currentId ?? state.rootId);   // P2 §3.1: 回到根也入栈（返回可逐级退）
    state.currentId = state.rootId;
    state.pendingTargetId = String(state.rootId);
    state.animateFlow = true;
    load();
  });

  const content = el('div', {});

  // 前置条件过滤 checkbox（随焦点场景重建）
  if (flow.preCondFilters && flow.preCondFilters.length) {
    const filterRow = el('div', { class: 'precond-filter' });
    filterRow.append(el('span', { style: 'font-size:9px;color:#999;font-weight:600', text: '前置条件:' }));
    for (const f of flow.preCondFilters) {
      const cb = el('input', { type: 'checkbox', checked: state.preConds.has(f.rawId) });
      cb.addEventListener('change', () => {
        if (cb.checked) state.preConds.add(f.rawId); else state.preConds.delete(f.rawId);
        state.animateFlow = false;   // 概率重算非导航：无动画直接重建
        load();
      });
      const label = el('label', { class: f.isNeg ? 'neg' : '' }, [cb, el('span', { text: (f.isNeg ? '¬' : '') + f.display })]);
      filterRow.append(label);
    }
    content.append(filterRow);
  }

  content.append(el('div', { class: 'flow-hint', text: s.semantics.formatHint ?? '' }));
  content.append(el('div', { class: 'flow-scroll' }, buildFlowTrack(s)));

  return section('场景流转', { icon: '🔀', accent: '#00695c', right: backBtn }, content);
}

/// 无动画路径：只替换轨道（主体不动）。
function renderFlowInto(flowSection, s) {
  const oldTrack = flowSection.querySelector('.flow-track');
  const newTrack = buildFlowTrack(s);
  oldTrack.replaceWith(newTrack);
  return newTrack;
}

/// 焦点切换动画（"视角平移"，全部 JS 驱动插值——离屏 WebView2 不播 CSS transition）：
/// 0. （点击时已完成）同级淡化到 0.12，fetch 并行
/// 1. 平移：轨道 transform 让目标卡横向移到当前卡位置（视角跟随目标）
/// 2. 重建：目标快照的三行替换轨道，复位 transform + 平滑滚动让新当前卡居中
/// 3. 出现：新当前卡先显现 → 前驱行 → 后继行依次淡入（目标的前驱后继慢慢出现）
async function animateFlowSwitch(flowSection, s) {
  const scroll = flowSection.querySelector('.flow-scroll');
  const track = flowSection.querySelector('.flow-track');
  const targetId = state.pendingTargetId;
  const targetEl = track?.querySelector(`.node-card[data-id="${targetId}"]`);
  const currentEl = track?.querySelector('.node-card.current');

  // 点击瞬间已把非目标卡压到 0.12（bindFocusSwitch）——短暂停顿让用户感知淡化完成
  await sleep(120);

  // 视角平移（目标卡 → 当前卡位置；无目标卡（回到当前/异常）跳过）
  let diff = 0;
  if (targetEl && currentEl && scroll) {
    const tr = targetEl.getBoundingClientRect();
    const cr = currentEl.getBoundingClientRect();
    diff = tr.left - cr.left;
    if (Math.abs(diff) > 2) {
      await animateJs(550, p => {
        track.style.transform = `translateX(${(-diff * p).toFixed(2)}px)`;
      });
    }
  }

  // 重建轨道（目标快照）
  const freshTrack = buildFlowTrack(s);
  track.replaceWith(freshTrack);
  track.style.transform = '';

  // 新当前卡平滑居中（视口跟随）
  const newCurrent = freshTrack.querySelector('.node-card.current');
  newCurrent?.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });

  // 依次淡入（JS 插值 0 → 1）：当前卡 → 前驱 +200ms → 后继 +350ms，每卡 +120ms
  const reveal = (cards, delay) => cards.forEach((c, i) => {
    c.style.opacity = '0';
    setTimeout(() => {
      animateJs(450, p => { c.style.opacity = String(p); }, t => t);
    }, delay + i * 120);
  });
  reveal([newCurrent].filter(Boolean), 0);
  reveal(freshTrack.querySelectorAll('.flow-row[data-row="pred"] .node-card'), 200);
  reveal(freshTrack.querySelectorAll('.flow-row[data-row="succ"] .node-card'), 350);
  newCurrent?.classList.add('card-pulse');
  document.body.dataset.flowAnimated = '1'; // 验收探针：动画分支真实执行过
  state.animateFlow = false;
}

// ─────────────────────────── ③ 如何进入（D08 §四） ───────────────────────────

function renderEntry(s) {
  const entry = s.semantics.entry;
  if (!entry) return null;
  const content = el('div', {});

  const conds = badgeRow(entry.conditions, '触发条件');
  if (conds) content.append(conds);

  const pre = badgeRow(entry.ownPreConditions, '前置条件');
  if (pre) content.append(el('div', { style: 'margin-top:8px' }, pre));

  if (entry.triggers && entry.triggers.length) {
    const rows = entry.triggers.map(t =>
      el('div', { class: 'row', style: 'margin-bottom:4px' },
        [el('span', { class: 'badge', style: 'background:#f3e5f5;color:#6a1b9a', text: t.name }),
         t.summary ? el('span', { style: 'font-size:10px;color:#666', text: t.summary }) : null]));
    content.append(el('div', { style: 'margin-top:8px' }, [el('div', { style: 'font-size:9px;color:#999;font-weight:600;margin-bottom:4px', text: '触发器' }), ...rows]));
  }

  return section('如何进入', { icon: '🚪', accent: '#1565c0' }, content);
}

// ─────────────────────────── 主渲染（D10 模板分发） ───────────────────────────

/// Encounter 渲染器（D08 页面序：Hero → 内容与效果 → 场景流转 → 如何进入 → 引用 → XML）；
/// 场景流转由 renderSnapshot 按锚点插入（焦点切换动画的局部重渲染目标）。
function renderEncounter(s) {
  return [
    renderHero(s),
    renderContentEffects(s),
    renderEntry(s),
    refPanel(s.semantics.refs),
    details(s.rawXml),
  ];
}

window.VizRenderers.Encounter = renderEncounter;

function renderSnapshot(s) {
  // P2 §3.2: 全量渲染前载入该文档的 UI 状态（展开/滚动），渲染后恢复
  loadUiState();
  const renderer = window.VizRenderers[s.type];
  if (!renderer) {
    app().replaceChildren(
      topBar(s),
      el('div', { class: 'not-implemented' },
        `「${s.type}」类型尚无 JS 渲染器（P1）—— 已加载 ${s.displayName}（${s.id}），见下方 Raw XML。`),
      details(s.rawXml));
    restoreUi();
    return;
  }

  const body = renderer(s);
  if (s.type === 'Encounter') {
    // Encounter 特有：流转区局部重渲染锚点（焦点切换动画只替换流转区）
    const flowSection = buildFlowSection(s);
    flowSection.classList.add('flow-section');
    body.splice(2, 0, flowSection);   // Hero → 内容与效果 → [场景流转] → 如何进入
  }
  app().replaceChildren(
    topBar(s, { canBack: state.navStack.length > 0, onBack: goBack }),
    ...body);
  restoreUi();
}

/// 渲染后恢复展开状态 + 滚动位置（P2 §3.2）
function restoreUi() {
  restoreExpands();
  if (uiState.scrollY > 0) {
    requestAnimationFrame(() => window.scrollTo(0, uiState.scrollY));
  }
  saveUiState();
}

// 版本标记（右下角小字）：确认编辑器加载的页面版本
const versionBadge = el('div', {
  class: 'viz-version',
  title: 'JS 可视化页面版本（构建时间戳）',
  text: 'v' + VIZ_VERSION,
});
versionBadge.addEventListener('click', () => showStatus('页面版本: ' + VIZ_VERSION));
document.addEventListener('DOMContentLoaded', () => document.body.append(versionBadge));

// ─────────────────────────── 通用渲染器 API（XML/JSON 传入 → 页面） ───────────────────────────

/// 应用一个快照（所有输入通道共用），resetRoot=true 时重置焦点锚点并关 sample 数据源。
function applySnapshot(s, resetRoot) {
  state.type = s.type;
  if (resetRoot) {
    state.rootId = s.id;
    state.currentId = s.id;
    state.sample = null;   // 传入模式焦点切换走 /viz/data（应用内回环端点）
    state.navStack = [];   // P2 §3.1: 新实体 = 新历史 + 新快照缓存
    state.snapshotCache.clear();
  }
  renderSnapshot(s);
}

/// JSON 快照文本 → 渲染（与 /viz/data 端点产出同构）。
function renderJson(jsonText) {
  const s = JSON.parse(jsonText);
  if (!s || typeof s !== 'object' || !s.type) throw new Error('无效的 JSON 快照（缺 type）');
  applySnapshot(s, true);
  return s;
}

/// XML 文本 → 页面内提取语义 → 渲染（单文件模式）。
function renderXml(xmlText) {
  const s = extractEncounterFromXml(xmlText);
  if (!s) throw new Error('无法解析该 XML（仅支持 encounters 表）');
  applySnapshot(s, true);
  return s;
}

/// 统一入口：JSON（{ 开头）或 XML（< 开头）。
function renderData(text) {
  const t = String(text ?? '').trim();
  if (t.startsWith('{')) return renderJson(t);
  if (t.startsWith('<')) return renderXml(t);
  throw new Error('无法识别输入：JSON（{...}）或 XML（<table...>）');
}

// 全局 API（宿主 InvokeScript / 浏览器 console / 自动化验证均可调用）
window.NeoViz = { render: renderData, renderJson, renderXml, applySnapshot };

/// 拖拽输入：XML/JSON 文件拖进页面即渲染（通用渲染器能力，无按钮无 UI）。
function initDragDrop() {
  window.addEventListener('dragover', e => e.preventDefault());
  window.addEventListener('drop', async e => {
    e.preventDefault();
    const file = [...(e.dataTransfer?.files ?? [])]
      .find(f => /\.(xml|json|txt)$/i.test(f.name));
    if (!file) return;
    try {
      renderData(await file.text());
    } catch (err) {
      showErrorBanner('加载失败: ' + err.message);
    }
  });
}

// ─────────────────────────── 启动 ───────────────────────────

function showStatus(text, isError = false) {
  const st = el('div', { class: 'status' + (isError ? ' error' : ''), text });
  app().replaceChildren(st);
}

// 错误横幅：切换失败时保留当前视图，仅顶部提示（不摧毁已渲染内容）
function showErrorBanner(msg) {
  const banner = el('div', { class: 'error-banner', text: msg });
  app().prepend(banner);
  setTimeout(() => banner.remove(), 8000);
}

let loading = false;   // 防重入：连续点击时忽略并发 load（否则动画互相覆盖）

async function load() {
  if (loading) return;
  loading = true;
  try {
    const s = await fetchSnapshot(state.currentId ?? state.rootId);
    state.currentId = s.id;

    const flowSection = document.querySelector('.flow-section');
    const animate = state.animateFlow;
    if (flowSection && s.semantics && s.type === 'Encounter') {
      if (animate) {
        await animateFlowSwitch(flowSection, s);   // 视角平移动画（淡化→平移→重建→依次出现）
      } else {
        renderFlowInto(flowSection, s);            // 无动画（初次/前置过滤重算/← 返回）
      }
      updateTopBar();   // P2 §3.1: 流转切换后同步返回按钮
    } else {
      renderSnapshot(s);   // 无流转区（异常/非 Encounter）→ 全量渲染
      state.animateFlow = false;
    }
  } catch (err) {
    state.animateFlow = false;
    // 恢复点击瞬间的淡化（fetch 失败时卡片不卡在半透明）
    document.querySelectorAll('.flow-track .node-card').forEach(c => { c.style.opacity = ''; });
    // 已有渲染内容（非首次加载）→ 保留视图 + 顶部横幅；否则整页错误
    const hasRendered = !!document.querySelector('.hero, .not-implemented, .node-card');
    if (hasRendered) showErrorBanner('切换失败: ' + err.message);
    else showStatus('加载失败: ' + err.message, true);
  } finally {
    loading = false;
  }
}

async function main() {
  initDragDrop();
  initScrollMemory();   // P2 §3.2: 滚动位置记忆（debounce 500ms）
  const params = new URLSearchParams(location.search);
  state.type = params.get('type') || 'Encounter';
  const id = params.get('id');
  const xml = params.get('xml');
  const json = params.get('json');
  const file = params.get('file');   // 调试：游戏目录 XML 文件（/viz/xmlfile 端点，C# 全量语义）
  const sample = params.get('sample');

  try {
    let s = null;
    if (sample) {
      // sample 模式：保留 state.sample（切换走本地 samples/），不走 applySnapshot 重置
      state.sample = sample;
      const res = await fetch('samples/' + sample + '.json');
      if (!res.ok) { showStatus(`sample 缺失: ${sample}.json → ${res.status}`); return; }
      s = await res.json();
      state.type = s.type;
      state.rootId = s.id;
      state.currentId = s.id;
      state.navStack = [];          // P2 §3.1
      state.snapshotCache.clear();
      renderSnapshot(s);
    } else if (json) {
      renderJson(json);                      // 快照 JSON 文本直接传入（渲染器 API）
      return;
    } else if (xml) {
      s = await fetchSnapshotFromXml(state.type, xml);   // 调试 URL：XML 文本经 C# 全量语义
      applySnapshot(s, true);
      return;
    } else if (file) {
      const res = await fetch('/viz/xmlfile?path=' + encodeURIComponent(file));
      if (!res.ok) throw new Error(`/viz/xmlfile → ${res.status}`);
      applySnapshot(await res.json(), true);
      return;
    } else if (id) {
      applySnapshot(await fetchSnapshot(id), true);
      return;
    } else {
      showStatus('缺少参数: 需要 ?type=&id= / ?xml= / ?json= / ?file= / ?sample=');
      return;
    }

    // 调试入口：?autoplay=N 自动模拟点击第 N 张可导航卡（缺省第 1 张）——
    // headless 截图/自动化无法做真实点击时的验收通道（触发同一动画/错误路径）。
    if (params.get('autoplay')) {
      const index = (parseInt(params.get('autoplay'), 10) || 1) - 1;
      setTimeout(() => {
        const cards = document.querySelectorAll('.flow-scroll .flow-row .node-card.navigable');
        const target = cards[index];
        if (target) target.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      }, 600);
    }

    // 调试入口：?autoback=N 自动点击「← 返回」N 次（P2 导航历史验收通道，同 autoplay 模式）
    if (params.get('autoback')) {
      const times = parseInt(params.get('autoback'), 10) || 1;
      setTimeout(() => {
        for (let i = 0; i < times; i++) {
          const btn = document.querySelector('.topbar-back');
          if (btn) btn.dispatchEvent(new MouseEvent('click', { bubbles: true }));
        }
      }, 1400);
    }

    // 调试入口：?autotoggle=key1,key2 自动点击指定 data-expand-key 元素
    // （P2 §3.2 状态记忆验收通道：同一浏览器会话内 sessionStorage 跨刷新持久化）
    if (params.get('autotoggle')) {
      const keys = params.get('autotoggle').split(',');
      setTimeout(() => {
        for (const key of keys) {
          const target = document.querySelector(`[data-expand-key="${key}"]`);
          // <details> 的原生开关在 summary 上（点击 details 本体不触发）
          const clickTarget = target?.querySelector('summary') ?? target;
          if (clickTarget) clickTarget.dispatchEvent(new MouseEvent('click', { bubbles: true }));
        }
      }, 900);
    }
  } catch (err) {
    showStatus('加载失败: ' + err.message, true);
  }
}

main();
})();
