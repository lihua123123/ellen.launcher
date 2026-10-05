# 单命令门禁：把三个独立检查串成一条，退出码即结论。
#
#   GATE: PASS (3/3)  -> 允许进入 P1-C
#   GATE: FAIL (N/3)  -> 退回 P0 修复 + P1 数据层，不得推迟到 P1
#
# 每个子检查都必须能「红」，否则它只是装饰。三项均已各自自检过：
#   1. dotnet test       —— 先写失败测试（CS0234）再写实现
#   2. DomainMirror      —— 篡改 0.15625 后 1 check failed / exit 1
#   3. LiteralGate       —— 注入 Coefficient(0.4) 后 1 violation / exit 1
#
# 用法: powershell -File tools/gate/check.ps1

$ErrorActionPreference = 'Continue'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$results = [System.Collections.Generic.List[string]]::new()
$passed = 0

function Invoke-Step {
    param([string]$Index, [string]$Name, [scriptblock]$Body)

    Write-Output "=== [$Index] $Name"
    $output = & $Body 2>&1
    $code = $LASTEXITCODE
    $output | Select-Object -Last 4 | ForEach-Object { Write-Output "    $_" }

    if ($code -eq 0) {
        $script:passed++
        $script:results.Add("$Index $Name -> PASS")
    }
    else {
        $script:results.Add("$Index $Name -> FAIL (exit=$code)")
    }
}

Invoke-Step -Index '1/4' -Name 'dotnet test (Resistance)' -Body {
    dotnet test 'src\Ellen.Launcher\Ellen.Launcher.Test\Ellen.Launcher.Test.csproj' `
        --filter 'FullyQualifiedName~Ellen.Launcher.Test.Resistance' -v minimal
}

Invoke-Step -Index '2/4' -Name 'DomainMirror check (C# 与 ArkTS 镜像同表)' -Body {
    Push-Location 'tools\DomainMirror'
    try {
        npm run --silent check
    }
    finally {
        Pop-Location
    }
}

Invoke-Step -Index '3/4' -Name 'ResistanceLiteralGate (抗性裸字面量)' -Body {
    powershell -NoProfile -File 'tools\ResistanceLiteralGate\check.ps1'
}

Invoke-Step -Index '4/4' -Name 'hvigorw assembleHap (真实鸿蒙构建)' -Body {
    powershell -NoProfile -File 'P0\gateways\build-hap.ps1'
}

Write-Output ""
if ($passed -eq 4) {
    Write-Output "GATE: PASS (4/4)  -> 允许进入 P1-C"
    exit 0
}

Write-Output ("GATE: FAIL ({0}/4)  -> 退回 P0 修复 + P1 数据层，不得推迟到 P1" -f $passed)
exit 1
