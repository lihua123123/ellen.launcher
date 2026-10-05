# 门槛 #4c：计算路径内抗性裸字面量 = 0
#
# 抗性值只能来自 Monster.json 的 BaseValue.*SubHurt、ResistanceTiers 常量、
# 或调用方入参。任何把裸数字直接喂进 k(r) 或在计算路径里赋值给抗性变量的写法都视为违规。
#
# 用法: powershell -File tools/ResistanceLiteralGate/check.ps1   (违规时退出码 1)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceRoot = Join-Path $root 'src'

# 允许出现裸数字的位置：常量表自身与读取器（把外部数据搬进来）。
$allowList = @(
    'ResistanceTiers.cs',
    'MonsterResistance.cs'
)

$rules = @(
    @{ Name = 'Coefficient 入参为裸字面量'; Pattern = 'Coefficient\s*\(\s*-?\d+(?:\.\d+)?[dDfF]?\s*\)' }
    @{ Name = '抗性变量被赋裸字面量'; Pattern = '\b(?:resistance|resist|抗性)\w*\s*=\s*-?\d+(?:\.\d+)?[dDfF]?(?:\s*;|\s*$)' }
)

$violations = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $sourceRoot -Recurse -Filter '*.cs' -File |
    Where-Object {
        # 排除测试工程：测试里的字面量是期望值，硬编码才是独立性来源。
        $_.FullName -notmatch '\\obj\\|\\bin\\|\.Test\\' -and
        $allowList -notcontains $_.Name
    } |
    ForEach-Object {
        $file = $_
        # @() 强制数组：单行文件时 Get-Content 返回 string，索引会得到 char。
        $lines = @(Get-Content -LiteralPath $file.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line.TrimStart().StartsWith('//')) { continue }
            foreach ($rule in $rules) {
                if ($line -match $rule.Pattern) {
                    $relative = $file.FullName.Substring($root.Length + 1)
                    $violations.Add(("{0}:{1}: [{2}] {3}" -f $relative, ($i + 1), $rule.Name, $line.Trim()))
                }
            }
        }
    }

if ($violations.Count -eq 0) {
    Write-Output "ResistanceLiteralGate: 0 violation"
    exit 0
}

Write-Output ("ResistanceLiteralGate: {0} violation(s)" -f $violations.Count)
$violations | ForEach-Object { Write-Output "  $_" }
exit 1
