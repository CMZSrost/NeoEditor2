#Requires -Version 5.1
<#
    NeoEditor 本地发包脚本（交互式，Docs/42 §八）

    菜单选项：
      [1] 发布并打包 · 单文件（推荐，~143MB，Web/ruffle 外置）
      [2] 发布并打包 · 多文件
      [3] 仅发布 · 单文件（不打包）
      [4] 运行测试
      [5] 打开输出目录
      [0] 退出

    取消：菜单输入 0 / q，或随时按 Ctrl+C 中断发布过程。
    参数模式（跳过菜单）：./publish.ps1 -Single / -Multi / -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$Single,      # 直接执行单文件发布（跳过菜单）
    [switch]$Multi,       # 直接执行多文件发布（跳过菜单）
    [switch]$SkipTests    # 发布前不跑测试
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Dist = Join-Path $Root "dist"
$PlayerCsproj = Join-Path $Root "NeoEditor.Player\NeoEditor.Player.csproj"
$Solution = Join-Path $Root "NeoEditor.sln"

function Show-Banner {
    Write-Host ""
    Write-Host "  NeoEditor Player 发包工具" -ForegroundColor Cyan
    Write-Host "  ==========================" -ForegroundColor DarkGray
}

function Invoke-Tests {
    Write-Host "`n运行测试（dotnet test NeoEditor.sln）..." -ForegroundColor Cyan
    & dotnet test $Solution --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "测试失败（exit $LASTEXITCODE），中止。" -ForegroundColor Red
        return $false
    }
    Write-Host "测试全部通过。" -ForegroundColor Green
    return $true
}

function Get-DistSize {
    if (-not (Test-Path $Dist)) { return "0 MB" }
    $mb = (Get-ChildItem $Dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
    return ("{0:N1} MB" -f $mb)
}

function Invoke-Publish {
    param([bool]$SingleFile)
    Write-Host "`n发布中（Ctrl+C 可随时取消）..." -ForegroundColor Cyan
    $args = @("publish", $PlayerCsproj, "-c", "Release", "-o", $Dist, "-p:DebugType=None")
    if ($SingleFile) {
        # IncludeNativeLibrariesForSelfExtract: also bundle native dlls (Skia/HarfBuzz/
        # ANGLE) into the exe — output stays a single NeoScavengerPlayer.exe + Web/
        $args += @("-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true")
    }
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        Write-Host "发布失败（exit $LASTEXITCODE）。" -ForegroundColor Red
        return $false
    }
    Write-Host "发布完成：$Dist（$(Get-DistSize)）" -ForegroundColor Green
    return $true
}

function New-ReleaseZip {
    param([string]$Version)
    $zip = Join-Path $Root "NeoScavengerPlayer-$Version-win-x64.zip"
    Remove-Item $zip -ErrorAction SilentlyContinue
    Write-Host "打包 $zip ..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $Dist "*") -DestinationPath $zip
    $size = "{0:N1} MB" -f ((Get-Item $zip).Length / 1MB)
    Write-Host "打包完成：$zip（$size）" -ForegroundColor Green
}

function Get-CsprojVersion {
    # R43: 版本号唯一来源 = csproj <Version>（窗口标题/About/导出 zip 同源）。
    $content = Get-Content $PlayerCsproj -Raw
    if ($content -match '<Version>([^<]+)</Version>') { return $Matches[1].Trim() }
    return ""
}

function Read-Version {
    $default = Get-CsprojVersion
    if ([string]::IsNullOrWhiteSpace($default)) { $default = Get-Date -Format "yyyyMMdd" }
    $v = Read-Host "版本号（用于 zip 命名，回车默认 $default）"
    if ([string]::IsNullOrWhiteSpace($v)) { $v = $default }
    return $v
}

function Invoke-PublishFlow {
    param([bool]$SingleFile)
    if (-not $SkipTests) {
        if (-not (Invoke-Tests)) { return }
    }
    if (-not (Invoke-Publish $SingleFile)) { return }
    $v = Read-Version
    New-ReleaseZip -Version $v
}

function Show-Menu {
    Show-Banner
    Write-Host ""
    Write-Host "  [1] 发布并打包 · 单文件（推荐）" -ForegroundColor White
    Write-Host "  [2] 发布并打包 · 多文件" -ForegroundColor White
    Write-Host "  [3] 仅发布 · 单文件（不打包）" -ForegroundColor White
    Write-Host "  [4] 运行测试" -ForegroundColor White
    Write-Host "  [5] 打开输出目录（dist）" -ForegroundColor White
    Write-Host "  [0] 退出" -ForegroundColor White
    Write-Host ""
}

# ── 参数模式：一次性流程后退出 ──
if ($Single -or $Multi) {
    Show-Banner
    if (-not $SkipTests) {
        if (-not (Invoke-Tests)) { exit 1 }
    }
    if (-not (Invoke-Publish -SingleFile:$Single)) { exit 1 }
    $v = Read-Version
    New-ReleaseZip -Version $v
    Write-Host "`n完成。zip 位于仓库根目录。" -ForegroundColor Green
    exit 0
}

# ── 交互菜单模式 ──
try {
    while ($true) {
        Show-Menu
        $choice = Read-Host "请选择"
        switch ($choice) {
            "1" { Invoke-PublishFlow -SingleFile $true }
            "2" { Invoke-PublishFlow -SingleFile $false }
            "3" {
                if (-not $SkipTests) { if (-not (Invoke-Tests)) { break } }
                Invoke-Publish -SingleFile $true
            }
            "4" { Invoke-Tests | Out-Null }
            "5" {
                if (Test-Path $Dist) { Start-Process explorer.exe $Dist } else { Write-Host "输出目录不存在，先执行发布。" -ForegroundColor Yellow }
            }
            { $_ -in @("0", "q", "Q", "cancel", "exit") } { Write-Host "再见。" -ForegroundColor DarkGray; break }
            default { Write-Host "无效选项，请重新输入。" -ForegroundColor Yellow }
        }
        if ($choice -in @("0", "q", "Q", "cancel", "exit")) { break }
        Write-Host ""
        Read-Host "按回车继续…" | Out-Null
    }
}
finally {
    # Ctrl+C / 异常时也回到控制台
    Write-Host ""
}
