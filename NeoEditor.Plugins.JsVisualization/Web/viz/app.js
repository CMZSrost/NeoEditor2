'use strict';
/* D09 JS 可视化 — 渲染引擎（零宿主依赖）
 * 版本标记：右下角小字显示构建时间戳，便于确认编辑器加载的是否最新页面。 */
const VIZ_VERSION = '20260808-2120';
 *
 * 契约：/viz/data 返回 EntitySnapshotDto JSON（C# 侧语义提取、字符串已本地化），
 * 本页只做布局与交互 —— D09 原则②。页面可在 WebView2 与独立浏览器中一致运行：
 * 数据全部经相对路径 fetch；chrome.webview 仅作可选增强。
 *
 * 交互（对照 RefNode.WireNavigation / D08 v1.3）：
 *  - 左键点击前驱/后继卡  = 组件内焦点切换（重算其前后文）
 *  - 「⏎ 回到当前」       = 焦点复位到最初查看的场景
 *  - Ctrl+左键 徽章/卡    = 页面跳转到目标实体（/viz/action navigate）
 *  - Ctrl+右键 徽章/卡    = Peek 预览（/viz/action peek）
 */

// ─────────────────────────── DOM 辅助 ───────────────────────────

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null) continue;
    if (k === 'class') node.className = v;
    else if (k === 'text') node.textContent = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else if (k === 'dataset') Object.assign(node.dataset, v);
    else node.setAttribute(k, v);
  }
  for (const c of [].concat(children)) {
    if (c == null) continue;
    node.append(c.nodeType ? c : document.createTextNode(String(c)));
  }
  return node;
}

const app = () => document.getElementById('app');

function fmtP(p) {
  const clamped = Math.max(0, Math.min(1, p));
  return (clamped * 100).toFixed(clamped * 100 % 1 === 0 ? 0 : 1).replace(/\.0$/, '') + '%';
}

// ─────────────────────────── 状态 ───────────────────────────

const state = {
  type: 'Encounter',
  rootId: null,      // 最初查看的场景（「回到当前」锚点）
  currentId: null,   // 当前焦点场景
  preConds: new Set(),
  animateFlow: false,  // 焦点切换动画（初次加载不动画，交互切换时开）
  pendingTargetId: null, // 本次切换点击的目标卡 id（平移/淡入动画用）
  sample: null,       // sample 模式：焦点切换读本地 samples/<type><id>.json
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

async function fetchSnapshot(id) {
  // sample 模式（独立静态服务器/无后端）：切换目标从本地 samples/ 取；
  // 正式环境（WebView2 回环 /viz/data）走同源端点。
  if (state.sample) {
    const res = await fetch(`samples/${state.type.toLowerCase()}${id}.json`);
    if (res.ok) return res.json();
    throw new Error(`samples/${state.type.toLowerCase()}${id}.json → ${res.status}`);
  }
  const params = new URLSearchParams({ type: state.type, id });
  if (state.preConds.size > 0) params.set('pre', [...state.preConds].join(','));
  const res = await fetch('/viz/data?' + params);
  if (!res.ok) throw new Error(`/viz/data → ${res.status}`);
  return res.json();
}

async function fetchSnapshotFromXml(type, xml) {
  const params = new URLSearchParams({ type, xml });
  const res = await fetch('/viz/data?' + params);
  if (!res.ok) throw new Error(`/viz/data(xml) → ${res.status}`);
  return res.json();
}

async function postAction(kind, entityType, entityId) {
  try {
    await fetch('/viz/action', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ kind, entityType, entityId }),
    });
  } catch (_) { /* 独立浏览器环境无 /viz/action —— 静默 */ }
}

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
    state.currentId = targetId;
    state.pendingTargetId = tid;
    state.animateFlow = true;
    load();
  });
}

// ─────────────────────────── 组件库（D09 §四 映射表） ───────────────────────────

function chip(c) {
  return el('span', { class: 'chip', style: `background:${c.bg};color:${c.fg}` }, c.label);
}

function badge(b) {
  const node = el('span', { class: 'badge', style: `background:${b.bg};color:${b.fg}` },
    (b.icon ? b.icon + ' ' : '') + b.text);
  if (b.targetType && b.targetId) bindNav(node, b.targetType, b.targetId);
  if (b.tooltip) bindTip(node, b.tooltip, b.text);
  return node;
}

function badgeRow(badges, prefix) {
  if (!badges || badges.length === 0) return null;
  const children = [];
  if (prefix) children.push(el('span', { class: 'chip', text: prefix }));
  children.push(...badges.map(badge));
  return el('div', { class: 'row' }, children);
}

// hover tooltip（复杂信息住在这里，不在卡面 —— D06 §四）
const tipEl = el('div', { class: 'tip', style: 'display:none' });
document.addEventListener('DOMContentLoaded', () => document.body.append(tipEl));
let tipTimer = null;

function bindTip(target, text, title) {
  target.addEventListener('mouseenter', () => {
    tipTimer = setTimeout(() => {
      tipEl.innerHTML = '';
      if (title) tipEl.append(el('div', { class: 'tip-title', text: title }));
      tipEl.append(el('div', { class: 'tip-line', text }));
      const r = target.getBoundingClientRect();
      tipEl.style.left = Math.min(r.left, window.innerWidth - 300) + 'px';
      tipEl.style.top = (r.bottom + 6) + 'px';
      tipEl.style.display = 'block';
    }, 250);
  });
  target.addEventListener('mouseleave', () => { clearTimeout(tipTimer); tipEl.style.display = 'none'; });
}

function section(title, accent, backBtn, content) {
  const head = el('div', { class: 'section-head' },
    [el('span', { class: 'accent-bar', style: `background:${accent}` }), el('span', { text: title })]);
  if (backBtn) head.append(backBtn);
  const sec = el('div', { class: 'section' }, [head, content]);
  return sec;
}

function imageBlock(containerClass, placeholder, url) {
  const box = el('div', { class: containerClass });
  if (url) {
    const img = el('img', { src: url, alt: '' });
    img.addEventListener('error', () => { img.remove(); box.append(placeholder); });
    box.append(img);
  } else {
    box.append(placeholder);
  }
  return box;
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

// ─────────────────────────── Hero（D08 §一） ───────────────────────────

function renderHero(s) {
  const sem = s.semantics;
  const info = el('div', { class: 'hero-info' });

  const idRow = el('div', { class: 'row' });
  idRow.append(el('span', { class: 'chip', style: 'background:#e3f2fd;color:#1565c0;font-weight:700',
    text: `ID: ${s.id}` }));
  if (s.modId != null) idRow.append(el('span', { class: 'chip', style: 'background:#f3e5f5;color:#6a1b9a',
    text: `mod ${s.modId}` }));
  info.append(idRow);

  const infoRow = el('div', { class: 'row' });
  infoRow.append(chip(sem.typeChip));
  if (sem.isEntry && !sem.isTerminal) infoRow.append(el('span', { class: 'chip', style: 'background:#e8f5e9;color:#2e7d32', text: '⛳ 入口' }));
  if (sem.isTerminal && !sem.isEntry) infoRow.append(el('span', { class: 'chip', style: 'background:#eceff1;color:#546e7a', text: '⏹ 终点' }));
  if (sem.removeCreatures) infoRow.append(el('span', { class: 'chip', style: 'background:#ffebee;color:#c62828', text: 'RemoveCreatures' }));
  if (sem.removeUsed) infoRow.append(el('span', { class: 'chip', style: 'background:#ffebee;color:#c62828', text: 'RemoveUsed' }));
  info.append(infoRow);

  info.append(el('div', { class: 'hero-title', text: s.displayName }));

  const chanceParts = [];
  if (sem.price !== 0) chanceParts.push(el('span', { text: `Price: $${sem.price.toFixed(2)}` }));
  if (sem.lootChance > 0) chanceParts.push(el('span', { style: 'color:#2e7d32', text: `Loot: ${fmtP(sem.lootChance)}` }));
  if (sem.accidentChance > 0) chanceParts.push(el('span', { style: 'color:#c62828', text: `Accident: ${fmtP(sem.accidentChance)}` }));
  if (sem.creatureChance > 0) chanceParts.push(el('span', { style: 'color:#283593', text: `Creature: ${fmtP(sem.creatureChance)}` }));
  if (chanceParts.length) info.append(el('div', { class: 'hero-chance' }, chanceParts));

  return el('div', { class: 'card hero' }, [
    imageBlock('hero-img', el('span', { class: 'placeholder', text: '📖' }), s.image),
    info,
  ]);
}

// ─────────────────────────── ④ 内容与效果（D08 §五，两栏） ───────────────────────────

function renderEffects(effects) {
  if (!effects || !effects.rows || effects.rows.length === 0) return null;
  const rows = effects.rows.map(r => {
    const value = el('div', { class: 'value' });
    if (r.badges && r.badges.length) value.append(...r.badges.map(badge));
    if (r.text) value.append(el('span', { class: 'text', text: r.text }));
    return el('div', { class: 'effect-row' }, [chip(r.label), value]);
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
  return section('内容与效果', '#e65100', null, body);
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

  return section('场景流转', '#00695c', backBtn, content);
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
/// 1. 淡化续播：被压低的卡片从 0.12 → 进一步到 0.12（保持）——点击瞬间已到位
/// 2. 平移：轨道 transform 让目标卡横向移到当前卡位置（视角跟随目标）
/// 3. 重建：目标快照的三行替换轨道，复位 transform + 平滑滚动让新当前卡居中
/// 4. 出现：新当前卡先显现 → 前驱行 → 后继行依次淡入（目标的前驱后继慢慢出现）
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

  return section('如何进入', '#1565c0', null, content);
}

// ─────────────────────────── Raw XML 兜底 ───────────────────────────

function renderRaw(s) {
  if (!s.rawXml) return null;
  return el('details', { class: 'raw-xml' },
    [el('summary', { text: 'Raw XML' }), el('pre', { text: s.rawXml })]);
}

// ─────────────────────────── 主渲染 ───────────────────────────

function renderSnapshot(s) {
  if (!s.semantics) {
    app().replaceChildren(
      el('div', { class: 'not-implemented' },
        `「${s.type}」类型尚无 JS 渲染器（P1）—— 已加载 ${s.displayName}（${s.id}），见下方 Raw XML。`),
      renderRaw(s));
    return;
  }

  const flowSection = buildFlowSection(s);
  flowSection.classList.add('flow-section');   // 焦点切换局部重建的锚点
  app().replaceChildren(
    renderHero(s),
    renderContentEffects(s),
    flowSection,
    renderEntry(s),
    renderRaw(s));
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
    if (flowSection && s.semantics) {
      if (animate) {
        await animateFlowSwitch(flowSection, s);   // 视角平移动画（淡化→平移→重建→依次出现）
      } else {
        renderFlowInto(flowSection, s);            // 无动画（初次/前置过滤重算）
      }
    } else {
      renderSnapshot(s);   // 无流转区（异常/首次路径兜底）→ 全量渲染
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
  } catch (err) {
    showStatus('加载失败: ' + err.message, true);
  }
}

main();
