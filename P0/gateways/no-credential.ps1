# 门禁：凭证与网络调用必须为 0（红线 5 / G7）
#
# 侧载试运行版不含账号登录、不含云端、不含自动更新（D23）。凭证不得出现在
# 任何会随包发布的源里：cookie_token / ltoken / stoken 属账号会话；
# DS 签名、Geetest 打码、设备指纹属风控面，一律不进范围。
#
# 用法: powershell -File P0/gateways/no-credential.ps1   (违规时退出码 1)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# 会随包发布或作为镜像真源的文件集合。
$scanRoots = @(
    (Join-Path $repo 'harmony'),
    (Join-Path $repo 'src\Ellen.Launcher\Ellen.Launcher.Domain'),
    (Join-Path $repo 'tools\DomainMirror\src')
)

$patterns = @(
    @{ Name = '账号会话凭证';   Pattern = 'cookie_token|\bltoken\b|\bstoken\b|SetUserCookie' }
    @{ Name = '风控面';         Pattern = 'GeeTest|Geetest|DeviceFp|CardCreateVerification|AmIBanned' }
    @{ Name = '运行时网络调用'; Pattern = '@kit\.NetworkKit|@ohos\.net\.|@ohos\.request|@kit\.RemoteCommunicationKit' }
    @{ Name = '网络权限';       Pattern = 'ohos\.permission\.INTERNET|ohos\.permission\.GET_NETWORK_INFO' }
    @{ Name = '私钥材料';       Pattern = 'BEGIN (RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY' }
    @{ Name = '已删除的拉取服务'; Pattern = 'EllenWikiDataService|IEllenWikiDataService' }
)

$extensions = @('.ets', '.ts', '.json', '.json5', '.cs', '.ps1', '.txt')
$violations = [System.Collections.Generic.List[string]]::new()

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
                $line = $lines[$i]
                if ($line.TrimStart().StartsWith('//')) { continue }
                foreach ($rule in $patterns) {
                    if ($line -match $rule.Pattern) {
                        $relative = $file.FullName.Substring($repo.Length + 1)
                        $violations.Add(("{0}:{1}: [{2}] {3}" -f $relative, ($i + 1), $rule.Name, $line.Trim()))
                    }
                }
            }
        }
}

if ($violations.Count -eq 0) {
    Write-Output "no-credential: 0 violation"
    exit 0
}

Write-Output ("no-credential: {0} violation(s)" -f $violations.Count)
$violations | ForEach-Object { Write-Output "  $_" }
exit 1
