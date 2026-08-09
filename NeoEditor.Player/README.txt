NeoScavenger Player v1.0.2（内测版）
=====================================

基于 Ruffle 的 NeoScavenger 独立播放器（Windows x64）。

【要求】
- Windows 10/11 x64，系统自带 WebView2 Runtime
- 自备游戏文件：NEOScavenger.swf 与游戏 data/ 目录放在同一文件夹
  （游戏根目录 = SWF 所在目录）

【使用】
1. 启动后把 NEOScavenger.swf 拖进窗口，或 文件 → 打开 SWF (Ctrl+O)
2. 文件 → 重新加载 (F5) 重开游戏；视图 → 全屏 (F11)
3. 视图 → 日志 (F10)：查看运行日志（行可点开展开完整内容）
4. 存档管理：查看/删除存档（游戏内「继续游戏」读的就是它）；每行「修改」
   可打开节点编辑器直接改存档数值（改完用「保存并加载」重启生效）
5. 调试：F12 开发者工具（Network / localStorage / Console）

【数据位置】
- 存档：页面 localStorage（重开播放器保留）；自动备份到 {游戏根目录}/save_backup/
- 运行日志：exe 旁 logs/player-run-*.log（每 run 一个，保留最新 2 个）
- 设置：%LocalAppData%/NeoScavengerPlayer/settings.json

【已知限制】
- Steam 模组版可能卡 43%（Ruffle 兼容性限制，原版正常）
- 单文件 exe 可能被杀软误报——加白名单即可
- 游戏会把内部日志写剪贴板，播放器已接管——内容重定向到日志（level=clipboard），
  不再污染系统剪贴板

【反馈 bug】
请提供：① 窗口标题栏版本号；② 文件 → 导出存档+日志 (zip) 生成的 zip 文件。

【SmartScreen 提示（发布者未知 / 已保护你的电脑）】
未签名的 exe 从网络下载后 Windows 会拦截，这是正常提示。内测阶段处理：
1. 右键下载的 zip 或解压后的 exe → 属性 → 常规 → 勾选「解除锁定」→ 确定
2. 或运行时点「更多信息」→「仍要运行」
（代码签名证书已在规划中，后续版本会显示发布者并逐步建立信誉）
