# Static audit: detect Style="{ThemeResource X}" references whose element type cannot accept
# the Style's TargetType.
#
# Why: WinUI throws XamlParseException("Cannot apply a Style with TargetType ...") at page
# construction time. The compiler does not catch it, and MsHutao's navigation pipeline
# swallows the exception, leaving the page blank. Checking statically is the only cheap guard.
#
# Notes / limits:
#  * A Style with TargetType=T is valid on any control derived from T, so custom controls
#    (e.g. DescriptionTextBlock : TextBlock) are legitimate. Only comparisons between the
#    known WinUI framework types below are reported.
#  * Attribute names ending in "Style" (ItemContainerStyle, FlyoutPresenterStyle, ...) are
#    intentionally ignored - they are not element Style attributes.
#
# Usage: powershell -File tools\XamlStyleAudit.ps1 [xamlRoot]

param(
    [string]$Root = "$PSScriptRoot\..\src\Ellen.Launcher\Ellen.Launcher"
)

# Framework element types that have no inheritance relation to each other.
$frameworkTypes = @(
    'Grid', 'Border', 'StackPanel', 'RelativePanel', 'Canvas', 'ScrollViewer', 'Pivot',
    'PivotItem', 'Image', 'TextBlock', 'Button', 'ToggleSwitch', 'ListView', 'ListViewItem',
    'Slider', 'ToggleButton', 'CheckBox', 'ContentPresenter', 'RadioButton', 'Expander',
    'NavigationViewItem', 'ProgressBar', 'ComboBox', 'ComboBoxItem', 'TreeView',
    'TreeViewItem', 'MenuFlyoutItem', 'Page', 'RichTextBlock'
)

$files = Get-ChildItem -Path $Root -Recurse -Filter *.xaml -File |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

# key -> TargetType
$styleTargetType = @{ }
foreach ($file in $files)
{
    $text = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($text, '<Style\s+[^>]*?x:Key="(?<k>[^"]+)"[^>]*?>', 'Singleline'))
    {
        $tt = [regex]::Match($m.Value, 'TargetType="(?<t>[^"]+)"')
        if ($tt.Success)
        {
            $styleTargetType[$m.Groups['k'].Value] = ($tt.Groups['t'].Value -replace '^.*:', '')
        }
    }
}

Write-Host ("Parsed {0} style definitions in {1} xaml files" -f $styleTargetType.Count, $files.Count)

$findings = @()
$ignoredCustomControls = @{ }

foreach ($file in $files)
{
    $text = Get-Content $file.FullName -Raw

    # every element start tag together with its attribute text
    foreach ($tag in [regex]::Matches($text, '<(?<t>[A-Za-z_][\w:.]*)(?<attrs>[^<>]*)>', 'Singleline'))
    {
        $attrs = $tag.Groups['attrs'].Value
        foreach ($style in [regex]::Matches($attrs, '(?<![\w.])Style\s*=\s*"\{(?:Theme|Static)Resource\s+(?<k>[^"\}]+)\}"'))
        {
            $key = $style.Groups['k'].Value.Trim()
            if (-not $styleTargetType.ContainsKey($key))
            {
                continue
            }

            $control = $tag.Groups['t'].Value -replace '^.*:', ''
            $target = $styleTargetType[$key]

            if ($control -eq $target)
            {
                continue
            }

            if ($frameworkTypes -notcontains $control -or $frameworkTypes -notcontains $target)
            {
                # custom control or unknown type: can only be verified at runtime
                $ignoredCustomControls["$control <- $target"] = $true
                continue
            }

            $findings += [pscustomobject]@{
                File     = $file.Name
                Control  = $control
                Style    = $key
                Expected = $target
            }
        }
    }
}

Write-Host ''
if ($findings.Count -eq 0)
{
    Write-Host 'PASS no invalid Style/TargetType usage between framework types'
}
else
{
    Write-Host ("FAIL {0} invalid Style usages:" -f $findings.Count)
    $findings | Sort-Object File, Style | Format-Table -AutoSize | Out-String -Width 130 | Write-Host
}

if ($ignoredCustomControls.Count -gt 0)
{
    $pairs = ($ignoredCustomControls.Keys | Sort-Object) -join '; '
    Write-Host ("INFO skipped custom/unknown control types (needs runtime check): {0}" -f $pairs)
}

if ($findings.Count -gt 0)
{
    exit 1
}
