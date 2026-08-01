# R27 — ImageAssetManager 双视图拆分

> 生效：2026-08-01 | 来源：用户决策 Phase 9C
> 依从：D01 Plugin 架构方向 · R24 统一数据修改路径
> 类型：基石(DO)

---

## 规则

ImageAssetManager 必须拆分为两个独立视图：**Image Browser**（文件系统浏览器）与 **Image Orchestration**（getimages.php 编排视图）。两者不混合在一棵树中。

---

## 拆分设计

| 维度 | Image Browser | Image Orchestration |
|------|:---:|:---:|
| **数据源** | 文件系统（`img/` 目录实际文件） | `getimages.php` 声明式编排 |
| **显示内容** | 按 Mod 分组的实际图片文件 + x2 配对 | 声明加载顺序 + normal→x2 配对 + 文件存在性 |
| **排序** | 文件名（或可选按修改时间） | **严格按 getimages.php 声明顺序**（不可改，引擎硬约束） |
| **操作** | 预览、双击打开编辑 | 编辑编排（增删 pair、调整顺序）、导出写回 PHP |
| **联动** | 选中图片可拖入 Orchestration | 标记 "文件缺失" 的 pair 高亮显示 |

---

## 为什么

1. **数据来源根本不同**：文件系统是"实际有什么"，getimages.php 是"引擎加载什么"。两者可能不一致（文件存在但未编排，或编排引用了不存在的文件）
2. **getimages.php 顺序语义**：NeoScavenger 引擎要求 `nRows=N&nCols=2` 格式，每对 `(normal, x2)` 顺序固定——normal 在前、x2 紧跟其后，形成 `n*2` 表格。文件系统扫描无法保证此顺序
3. **当前混合导致混淆**：`BuildTree()` 对 base game 只扫描 `img/` 目录、忽略 `<gameRoot>/getimages.php`；对 mod 在有 getimages.php 时用解析结果、否则回退到文件扫描——两种模式行为不一致

---

## 实现约束

### Image Browser

- **Base Game 节点**：扫描 `<gameRoot>/img/` 目录，用 `@2x` / `_2x` 文件名约定配对
- **Mod 节点**：扫描 `<modPath>/img/` 目录（如存在），同上配对
- **不解析 getimages.php**
- 预览、搜索过滤、双击打开编辑保留现有逻辑

### Image Orchestration

- **Base Game 节点**：读取 `<gameRoot>/getimages.php`，解析声明顺序
- **Mod 节点**：读取 `<modPath>/getimages.php`，解析声明顺序
- 显示 pair 列表（声明顺序不可编辑，但允许增删 pair）
- 校验每对图片文件是否实际存在（绿色✓ / 红色✗）
- 编辑后通过 `PhpParser.GenerateImagePhp` 写回（保留 n*2 顺序约束）

### 路径解析

图片路径解析必须依次尝试：
1. `<modFolder>/<imageName>` — 相对 mod 目录
2. `<modFolder>/img/<imageName>` — mod 的 img 子目录
3. `<gameRoot>/img/<imageName>` — base game 图片（mod 可引用 game 资源）

---

## 决策边界

### 适用

- ImageAssetManager 的 UI 结构和数据加载逻辑

### 不适用

- ImageEditorDocument（图片编辑）——不做修改
- ModImagesDocument（单 mod 图片编辑）——保留，与 Orchestration 可能功能重叠，后续评估合并
- `PhpParser` / `IModImageListService` ——保留，Orchestration 复用

---

## 验收

- ImageAssetManager Tool Dock 不再混合显示文件扫描和 getimages.php 解析结果
- Image Orchestration 中声明的 pair 顺序与 getimages.php 中 `strImageURL` 顺序严格一致
- 文件存在性校验正确标记
