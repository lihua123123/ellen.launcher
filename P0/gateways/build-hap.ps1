# 真实鸿蒙构建（门槛 #6）。
#
# 为什么需要这个脚本：hvigor 用 fs.realpathSync 校验项目路径，白名单只允许
# 字母/数字/-/_/./()/空格/@。本仓库位于 D:\艾莲的数据库\... 含中文，直接构建会
# 报 00306003 Specification Limit Violation。junction 无效（realpath 仍解析回中文）。
# 故在纯 ASCII 路径镜像构建，再把产物拷回仓库。
#
# 另两个必须：DEVECO_SDK_HOME 指向 SDK；jbr\bin 必须在 PATH（系统未装 java，
# 否则 PackageHap 阶段 spawn java ENOENT）。
#
# 用法: powershell -File P0/gateways/build-hap.ps1

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source = Join-Path $repo 'harmony'
$stage = 'C:\hmos-build\harmony'
$deveco = 'D:\DevEco Studio'
$hvigorw = Join-Path $deveco 'tools\hvigor\bin\hvigorw.bat'

foreach ($required in @($source, $hvigorw, (Join-Path $deveco 'sdk'), (Join-Path $deveco 'jbr\bin\java.exe'))) {
    if (-not (Test-Path $required)) { throw "缺少前置: $required" }
}

if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $stage) | Out-Null
robocopy $source $stage /E /XD node_modules oh_modules .hvigor build .cxx /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy 失败: $LASTEXITCODE" }

Set-Location $stage
$env:DEVECO_SDK_HOME = Join-Path $deveco 'sdk'
$env:PATH = (Join-Path $deveco 'jbr\bin') + ';' + $env:PATH

& $hvigorw assembleHap --no-daemon
$code = $LASTEXITCODE

if ($code -ne 0) {
    Write-Output "BUILD FAILED (exit=$code)"
    exit $code
}

$outputs = Join-Path $stage 'entry\build\default\outputs\default'
$hap = Get-ChildItem -Path $outputs -Filter '*.hap' -File | Select-Object -First 1
if (-not $hap) { throw "构建成功但未找到 .hap" }

# 产物拷回仓库，便于留档与哈希核对。
$sink = Join-Path $source 'entry\build\default\outputs\default'
New-Item -ItemType Directory -Force -Path $sink | Out-Null
Copy-Item $hap.FullName $sink -Force

$hash = (Get-FileHash $hap.FullName -Algorithm SHA256).Hash
Write-Output ""
Write-Output ("HAP      : {0}" -f $hap.Name)
Write-Output ("SIZE     : {0:N0} B" -f $hap.Length)
Write-Output ("SHA256   : {0}" -f $hash)
