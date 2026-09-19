$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$version = '0.9.5'

Write-Host '[1/6] Validating XAML/XML...'
Get-ChildItem (Join-Path $root 'src\MYWO.Desktop') -Filter '*.xaml' -Recurse | ForEach-Object {
    try { [xml](Get-Content $_.FullName -Raw) | Out-Null }
    catch { throw "Invalid XAML/XML: $($_.FullName): $($_.Exception.Message)" }
}

Write-Host '[2/6] Validating reference geometry and design tokens...'
$main = Get-Content (Join-Path $root 'src\MYWO.Desktop\MainWindow.xaml') -Raw
$geometry = @(
    'Width="1380" Height="950"',
    'x:Name="SidebarColumn" Width="214"',
    'RowDefinition Height="42"',
    'x:Name="MainToolbarRow" Height="68"',
    'x:Name="ProductDrawer" Grid.RowSpan="3" Panel.ZIndex="30" Width="422"',
    'x:Name="ServiceDrawer" Grid.RowSpan="3" Panel.ZIndex="30" Width="420"',
    'Source="Assets/mywo-logo.png"'
)
foreach ($token in $geometry) {
    if (-not $main.Contains($token)) { throw "Reference geometry/design token missing: $token" }
}

foreach ($token in @(
    'x:Name="SidebarLogoRow"',
    'x:Name="PageHeaderRow"',
    'x:Name="ProductFilterGrid"',
    'x:Name="ProductMoreFiltersBorder"',
    'x:Name="ServiceFilterGrid"',
    'x:Name="ServiceSortBorder"',
    'x:Name="DashboardMainGrid"',
    'x:Name="DashboardSummaryGrid"',
    'x:Name="CategoriesTreeColumn"',
    'x:Name="CategoriesTreeCard"',
    'x:Name="CategoriesUltraActions"',
    'x:Name="ApiBottomGrid"',
    'x:Name="HistoryDetailsGrid"',
    'x:Name="PriceOverviewGrid"',
    'x:Name="SettingsPrimaryRow"',
    'x:Name="SettingsSecondaryRow"',
    'x:Name="SettingsCompaniesCard"',
    'x:Name="SettingsSystemCard"',
    'x:Name="SettingsOptionsGrid"',
    'x:Name="SearchShortcutBadge"',
    'x:Name="NotificationPopupCard"'
)) {
    if (-not $main.Contains($token)) { throw "Responsive layout token missing: $token" }
}

$mainCode = Get-Content (Join-Path $root 'src\MYWO.Desktop\MainWindow.xaml.cs') -Raw
foreach ($token in @(
    'ultraNarrow',
    'DpiChanged',
    'WindowStateService.Restore',
    'FitWindowToCurrentWorkArea',
    'ApplyProductFilterLayout',
    'ApplyServiceFilterLayout',
    'ApplyDashboardResponsiveLayout',
    'ApplyApiResponsiveLayout',
    'ApplyHistoryResponsiveLayout',
    'ApplyPriceResponsiveLayout',
    'ApplySettingsResponsiveLayout',
    'CategoriesUltraActions'
)) {
    if (-not $mainCode.Contains($token)) { throw "Adaptive runtime token missing: $token" }
}

$workflow = Get-Content (Join-Path $root '.github\workflows\windows-release.yml') -Raw
foreach ($token in @(
    '@{ W = 880; H = 600 }',
    '@{ W = 1024; H = 768 }',
    '@{ W = 1280; H = 720 }',
    '@{ W = 1366; H = 768 }',
    '@{ W = 1440; H = 900 }',
    '@{ W = 1600; H = 900 }',
    '@{ W = 1920; H = 1080 }',
    '@{ W = 2560; H = 1440 }',
    '@{ W = 3840; H = 2160 }'
)) {
    if (-not $workflow.Contains($token)) { throw "Runtime resize QA token missing: $token" }
}
$windowStatePath = Join-Path $root 'src\MYWO.Desktop\Services\WindowStateService.cs'
if (-not (Test-Path $windowStatePath)) { throw 'Missing persisted window-state service.' }

$manifestPath = Join-Path $root 'src\MYWO.Desktop\app.manifest'
if (-not (Test-Path $manifestPath)) { throw 'Missing Per-Monitor DPI manifest.' }
$manifest = Get-Content $manifestPath -Raw
foreach ($token in @('PerMonitorV2','longPathAware')) {
    if (-not $manifest.Contains($token)) { throw "DPI/manifest token missing: $token" }
}

$app = Get-Content (Join-Path $root 'src\MYWO.Desktop\App.xaml') -Raw
foreach ($token in @('x:Key="ToggleSwitch"','x:Key="AccentGradient"','FontSize" Value="32"')) {
    if (-not $app.Contains($token)) { throw "MYWO design-system token missing: $token" }
}

Write-Host '[3/6] Validating projects and version consistency...'
foreach ($project in @('MYWO.Desktop','MYWO.Updater','MYWO.Setup')) {
    $path = Join-Path $root "src\$project\$project.csproj"
    if (-not (Test-Path $path)) { throw "Missing project: $path" }
    $xml = [xml](Get-Content $path -Raw)
    if ($xml.Project.PropertyGroup.Version -notcontains $version) { throw "$project version is not $version" }
}

Write-Host '[4/6] Validating branding assets...'
foreach ($asset in @('src\MYWO.Desktop\Assets\mywo.ico','src\MYWO.Desktop\Assets\mywo-logo.png')) {
    $path = Join-Path $root $asset
    if (-not (Test-Path $path)) { throw "Missing branding asset: $asset" }
    if ((Get-Item $path).Length -lt 512) { throw "Branding asset is unexpectedly small: $asset" }
}

Write-Host '[5/6] Validating setup/update contract...'
$setupCode = Get-Content (Join-Path $root 'src\MYWO.Setup\Program.cs') -Raw
$updaterCode = Get-Content (Join-Path $root 'src\MYWO.Updater\Program.cs') -Raw
foreach ($token in @('MYWO-Update.exe','--uninstall','UninstallString','ReplaceInstallTree')) {
    if (-not ($setupCode.Contains($token) -or $updaterCode.Contains($token))) { throw "Setup/update token missing: $token" }
}
if ($setupCode.Contains('unins000.exe') -or $setupCode.Contains('uninstall.exe')) { throw 'Standalone uninstaller reference detected in setup source.' }

Write-Host '[6/6] Scanning production source for unfinished markers...'
$bad = Get-ChildItem (Join-Path $root 'src') -Recurse -File | Where-Object { $_.Extension -in '.cs','.xaml' } |
    Select-String -Pattern 'TODO\b|FIXME\b|PLACEHOLDER\b|lorem ipsum|dev only' -CaseSensitive:$false
if ($bad) {
    $bad | ForEach-Object { Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'Unfinished production markers detected.'
}

Write-Host 'Static source, release-contract and reference-parity validation passed.'
