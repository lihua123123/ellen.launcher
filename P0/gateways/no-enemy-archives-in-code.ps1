# 门禁：代码与随包资源中不得出现 EnemyArchives UGC 数据（红线 7 / G7）
#
# EnemyArchives.json 是米游社 UGC 的 OCR/VLM 转录成果，未获授权，不得打包或回引。
# 抗性主源已改为随包 MIT 元数据 Monster.json 的 BaseValue.*SubHurt（见 env-facts.md
# 与 P1-A-metadata-budget.md），本门禁防止它被悄悄引回来。
#
# 用法: powershell -File P0/gateways/no-enemy-archives-in-code.ps1   (违规时退出码 1)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# 代码与数据源；不含 P0/*.md 等文档（文档需要能说明「不引用」这件事）。
$scanRoots = @(
    (Join-Path $repo 'harmony'),
    (Join-Path $repo 'src\Ellen.Launcher\Ellen.Launcher.Domain'),
    (Join-Path $repo 'src\Ellen.Launcher\Ellen.Launcher.Test\Resistance'),
    (Join-Path $repo 'tools\ScalingStatGen'),
    (Join-Path $repo 'tools\DomainMirror\src'),
    (Join-Path $repo 'tools\ResistanceLiteralGate'),
    (Join-Path $repo 'tools\gate')
)

$extensions = @('.ets', '.ts', '.json', '.json5', '.cs', '.py', '.ps1')
$violations = [System.Collections.Generic.List[string]]::new()

# 1) 随包目录里不得存在任何 EnemyArchives 文件或目录。
$packaged = Join-Path $repo 'harmony'
if (Test-Path $packaged) {
    Get-ChildItem -Path $packaged -Recurse -Force |
        Where-Object { $_.Name -match 'EnemyArchive' } |
        ForEach-Object { $violations.Add("随包资源中出现: " + $_.FullName.Substring($repo.Length + 1)) }
}

# 2) 代码中不得出现 EnemyArchives 标识符。
foreach ($root in $scanRoots) {
    if (-not (Test-Path $root)) { continue }

    Get-ChildItem -Path $root -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '\\node_modules\\|\\oh_modules\\|\\build\\|\\\.hvigor\\' -and
            $extensions -contains $_.Extension
        } |
        ForEach-Object {
            $file = $_
            $lines = @(Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue)
            for ($i = 0; $i -lt $lines.Count; $i++) {
                # 跳过注释：文档里说明「不依赖 EnemyArchives」是否定声明，不是引用。
                if ($lines[$i].TrimStart().StartsWith('//')) { continue }
                if ($lines[$i] -match 'EnemyArchive') {
                    $relative = $file.FullName.Substring($repo.Length + 1)
                    $violations.Add(("{0}:{1}: {2}" -f $relative, ($i + 1), $lines[$i].Trim()))
                }
            }
        }
}

if ($violations.Count -eq 0) {
    Write-Output "no-enemy-archives-in-code: 0 violation"
    exit 0
}

Write-Output ("no-enemy-archives-in-code: {0} violation(s)" -f $violations.Count)
$violations | ForEach-Object { Write-Output "  $_" }
exit 1
