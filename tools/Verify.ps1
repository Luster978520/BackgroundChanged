param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$root = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $root "BackgroundChanged/bin/$Configuration/BackgroundChanged.exe"
[System.Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null
[System.Windows.Application]::ResourceAssembly = [BackgroundChanged.App].Assembly
$testDirectory = Join-Path $root ('artifacts/verification-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$app = New-Object BackgroundChanged.App
$app.InitializeComponent()
# Test owns its windows and must not run the production StartupUri with personal settings.
[Windows.Application].GetField('_startupUri',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($app,$null)
$app.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
$checks = New-Object 'System.Collections.Generic.List[string]'
function Assert-That($condition, [string]$description) {
    if (!$condition) { throw "FAILED: $description" }
    $checks.Add("PASS: $description")
}
function Pump([int]$milliseconds = 750) {
    $frame = New-Object System.Windows.Threading.DispatcherFrame
    $timer = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds)
    $timer.Tag = $frame
    $timer.Add_Tick({ param($s,$e) $s.Stop(); $s.Tag.Continue = $false })
    $timer.Start()
    [System.Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Render($window, [string]$name) {
    $window.UpdateLayout()
    $visual = $window.Content
    $width = [int]$visual.ActualWidth
    $height = [int]$visual.ActualHeight
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width,$height,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create((Join-Path $root "artifacts/$name.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
$bindingLog = New-Object System.IO.StringWriter
$listener = New-Object System.Diagnostics.TextWriterTraceListener($bindingLog)
[System.Diagnostics.PresentationTraceSources]::DataBindingSource.Listeners.Add($listener) | Out-Null
[System.Diagnostics.PresentationTraceSources]::DataBindingSource.Switch.Level = [System.Diagnostics.SourceLevels]::Warning
$window = $null
try {
    $service = New-Object BackgroundChanged.Services.SettingsService($testDirectory)
    $vm = New-Object BackgroundChanged.ViewModels.MainViewModel($service)
    $window = New-Object BackgroundChanged.MainWindow($vm)
    $window.ShowInTaskbar = $false
    $window.WindowStartupLocation = 'Manual'
    $window.Left = -10000
    $window.Top = -10000
    $window.Show()
    Pump
    Assert-That (!$vm.IsNight) 'Fresh launch uses day mode'
    Assert-That ($window.FindName('ThemeSwitch').IsNight -eq $false) 'Switch reflects initial view-model state'
    Render $window 'day-preview'

    $launcher = $window.FindName('Launcher')
    $launcherToggle = $window.FindName('LauncherToggle')
    $featureScroll = $launcher.FindName('FeatureScrollViewer')
    $listToggle = $launcher.FindName('ListToggle')
    Assert-That ($launcher.IsVisible -and !$launcher.IsExpanded -and $featureScroll.Visibility -eq 'Collapsed') 'Startup keeps the glass header visible and hides only the feature list'
    Render $window 'launcher-collapsed-preview'
    $collapsedHeight = $launcher.FindName('LauncherGrid').ActualHeight
    $listToggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($launcher.IsExpanded -and $featureScroll.IsVisible -and $launcher.FindName('LauncherGrid').ActualHeight -gt $collapsedHeight) 'Down arrow expands buttons and restores the full glass panel'
    $listToggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That (!$launcher.IsExpanded -and $featureScroll.Visibility -eq 'Collapsed' -and $launcher.IsVisible -and [Math]::Abs($launcher.FindName('LauncherGrid').ActualHeight - $collapsedHeight) -lt 1) 'Up arrow hides only buttons and shrinks the panel back to its header'
    $listToggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    $launcherToggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($launcher.Visibility -eq 'Collapsed' -and $window.FindName('LauncherColumn').ActualWidth -eq 0) 'Top shortcut still hides the entire panel and releases its space'
    $launcherToggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($launcher.IsVisible -and $launcher.IsExpanded) 'Top shortcut restores the panel and preserves its list state'
    Render $window 'launcher-expanded-preview'
    $featureItems = $launcher.FindName('FeatureItems')
    $featureScroll = $launcher.FindName('FeatureScrollViewer')
    Assert-That ($featureItems.Items.Count -eq 12) 'Launcher exposes twelve feature buttons'
    Assert-That ($featureScroll.ScrollableHeight -gt 0) 'Launcher content scrolls within a bounded panel'
    $featureScroll.ScrollToEnd()
    Pump 100
    Assert-That ($featureScroll.VerticalOffset -gt 0) 'Launcher scroll reaches later features'
    $featureScroll.ScrollToHome()
    Pump 100
    function Find-FeatureButton($visual) {
        if ($visual -is [Windows.Controls.Button]) { return $visual }
        for ($i = 0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($visual); $i++) {
            $found = Find-FeatureButton ([Windows.Media.VisualTreeHelper]::GetChild($visual, $i))
            if ($null -ne $found) { return $found }
        }
        return $null
    }
    $featureButton = Find-FeatureButton ($featureItems.ItemContainerGenerator.ContainerFromIndex(0))
    $featureButton.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($window.OwnedWindows.Count -eq 1) 'Feature button opens an owned placeholder window'
    $featureButton.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Assert-That ($window.OwnedWindows.Count -eq 1) 'Repeated feature click reuses the existing window'
    $window.OwnedWindows[0].Close()
    $featureButton.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Assert-That ($window.OwnedWindows.Count -eq 1) 'A closed feature window can be reopened'
    $window.OwnedWindows[0].Close()
    $window.Width = 820; $window.Height = 640
    Pump 100
    $switchBounds = $window.FindName('ThemeSwitch').TransformToAncestor($window.Content).TransformBounds((New-Object Windows.Rect(0,0,320,159)))
    $launcherBounds = $launcher.TransformToAncestor($window.Content).TransformBounds((New-Object Windows.Rect(0,0,$launcher.ActualWidth,$launcher.ActualHeight)))
    Assert-That ($launcherBounds.Right -lt $switchBounds.Left -and $switchBounds.Right -le $window.Content.ActualWidth) 'Minimum window size keeps launcher and theme switch separate and visible'
    Render $window 'launcher-compact-preview'
    $window.Width = 1180; $window.Height = 800
    Pump 100

    # Exercise the actual ToggleButton binding, not just the view-model setter.
    $control = $window.FindName('ThemeSwitch')
    $toggle = $control.FindName('Switch')
    $toggle.IsChecked = $true
    Pump 260
    $move = $toggle.Template.FindName('KnobTranslation', $toggle)
    $rotation = $toggle.Template.FindName('KnobRotation', $toggle)
    $expectedAngle = 192.0 / 74 * 180 / [Math]::PI
    if ([Windows.SystemParameters]::ClientAreaAnimation) {
        Assert-That ($move.X -gt 0 -and $move.X -lt 192 -and $rotation.Angle -gt 0) 'Day-to-night knob rotates during its travel'
        Assert-That ([Math]::Abs($rotation.Angle - $move.X / 74 * 180 / [Math]::PI) -lt .01) 'Translation and rotation follow the same rolling progress'
        Render $window 'rolling-midpoint-preview'
    }
    Pump
    Assert-That $vm.IsNight 'ToggleButton changes theme through the two-way binding'
    Assert-That ($window.FindName('NightLayer').Opacity -gt .99) 'Night background reaches full opacity'
    $move = $toggle.Template.FindName('KnobTranslation', $toggle)
    Assert-That ([Math]::Abs($move.X - 192) -lt .01) 'Moon slides to the right endpoint'
    Assert-That ([Math]::Abs($rotation.Angle - $expectedAngle) -lt .01) 'Night endpoint completes the rolling angle'
    $moonOrientation = $toggle.Template.FindName('MoonOrientation', $toggle)
    Assert-That ([Math]::Abs($rotation.Angle + $moonOrientation.Angle) -lt .01) 'Moon settles in its original artwork orientation'
    Assert-That ($app.Resources['ForegroundBrush'].Color.ToString() -eq '#FFEFF3FF') 'Night resource dictionary is applied'
    Render $window 'night-preview'

    $vm.IsNight = $false
    Pump 100
    $before = $window.FindName('NightLayer').Opacity
    $beforeRotation = $rotation.Angle
    if ([Windows.SystemParameters]::ClientAreaAnimation) {
        Assert-That ($beforeRotation -gt 0 -and $beforeRotation -lt $expectedAngle) 'Night-to-day knob rolls in the reverse direction'
    }
    $vm.IsNight = $true
    $after = $window.FindName('NightLayer').Opacity
    Assert-That ([Math]::Abs($rotation.Angle - $beforeRotation) -lt .1) 'Reversing the rolling animation preserves its current angle'
    Assert-That ([Math]::Abs($before - $after) -lt .03) 'Reversing a transition preserves its current opacity'
    Pump 100
    $vm.IsNight = $false
    Pump
    Assert-That ($window.FindName('NightLayer').Opacity -lt .01 -and [Math]::Abs($move.X) -lt .01) 'Interrupted transitions finish in the last requested state'
    Assert-That ([Math]::Abs($rotation.Angle) -lt .01) 'Interrupted rolling returns to the exact day endpoint'

    $vm.IsSettingsOpen = $true
    Pump 350
    Assert-That ($window.FindName('SettingsOverlay').Visibility -eq 'Visible') 'Settings drawer opens'
    Assert-That (!$window.FindName('MainContent').IsEnabled) 'Modal settings disables underlying controls'
    Render $window 'settings-preview'
    $vm.EditingMode = 0
    $vm.BackgroundKind = '纯色'
    $vm.SolidColor = '#91BED5'
    $vm.Overlay = .25
    Assert-That ($vm.DayBackground.Color.ToString() -eq '#FF91BED5') 'Day solid color is editable'
    $vm.EditingMode = 1
    Assert-That ($vm.BackgroundKind -eq '风景渐变') 'Night background settings remain independent'
    $vm.BackgroundKind = '纯色'
    $vm.SolidColor = '#223456'
    $vm.Overlay = .35
    $vm.IsNight = $true
    Pump
    Render $window 'night-settings-preview'
    $window.Width = 820; $window.Height = 640
    Pump 200
    Render $window 'compact-settings-preview'
    $vm.CloseSettingsCommand.Execute($null)
    Assert-That ($window.FindName('SettingsOverlay').Visibility -eq 'Collapsed') 'Settings drawer closes'
    $window.Close(); $window = $null

    $restored = New-Object BackgroundChanged.ViewModels.MainViewModel($service)
    Assert-That $restored.IsNight 'Last theme persists across a new view-model'
    $window = New-Object BackgroundChanged.MainWindow($restored)
    $window.ShowInTaskbar = $false; $window.WindowStartupLocation = 'Manual'
    $window.Left = -10000; $window.Top = -10000
    $window.Show(); Pump
    Assert-That ($window.FindName('ThemeSwitch').IsNight -and $window.FindName('NightLayer').Opacity -gt .99) 'Reopened window initializes the restored night visuals'
    $restoredSwitch = $window.FindName('ThemeSwitch').FindName('Switch')
    $restoredRotation = $restoredSwitch.Template.FindName('KnobRotation',$restoredSwitch)
    Assert-That ([Math]::Abs($restoredRotation.Angle - $expectedAngle) -lt .01) 'Restored night mode initializes the correct rolling orientation'
    $restored.EditingMode = 0
    Assert-That ($restored.SolidColor -eq '#91BED5' -and [Math]::Abs($restored.Overlay - .25) -lt .001) 'Day color and overlay persist'
    $restored.EditingMode = 1
    Assert-That ($restored.SolidColor -eq '#223456' -and [Math]::Abs($restored.Overlay - .35) -lt .001) 'Night color and overlay persist'
    $restored.ResetCommand.Execute($null)
    Assert-That ($restored.BackgroundKind -eq '风景渐变') 'Reset restores only the edited background'
    $restored.EditingMode = 0
    Assert-That ($restored.SolidColor -eq '#91BED5') 'Reset leaves the other theme untouched'
    $window.Close(); $window = $null

    $source = Join-Path $root 'artifacts/day-preview.png'
    $imported = $service.ImportImage($source,$false)
    $custom = New-Object BackgroundChanged.Models.BackgroundSettings
    $custom.Kind = '图片'; $custom.ImagePath = $imported
    $fallback = $false
    $brush = [BackgroundChanged.Services.BackgroundFactory]::Create($custom,$false,[ref]$fallback)
    Assert-That (!$fallback -and $brush -is [System.Windows.Media.ImageBrush]) 'Imported local image decodes as an ImageBrush'
    $stream = [System.IO.File]::Open($imported, 'Open', 'ReadWrite', 'None'); $stream.Dispose()
    Assert-That $true 'Decoded background does not lock its source file'
    $custom.ShowWholeImage = $true
    $brush = [BackgroundChanged.Services.BackgroundFactory]::Create($custom,$false,[ref]$fallback)
    Assert-That ($brush.Stretch -eq 'Uniform') 'Whole-image option uses Uniform stretch'
    $custom.ImagePath = Join-Path $testDirectory 'missing.png'
    $brush = [BackgroundChanged.Services.BackgroundFactory]::Create($custom,$true,[ref]$fallback)
    Assert-That ($fallback -and $brush -is [System.Windows.Media.DrawingBrush]) 'Missing image safely falls back to vector landscape'
    $custom.ImagePath = Join-Path $testDirectory 'broken.png'
    [System.IO.File]::WriteAllText($custom.ImagePath, 'not an image')
    $brush = [BackgroundChanged.Services.BackgroundFactory]::Create($custom,$true,[ref]$fallback)
    Assert-That $fallback 'Corrupt image safely falls back'
    [System.IO.File]::WriteAllText((Join-Path $testDirectory 'settings.xml'), '<broken>')
    $message = $null
    $loaded = $service.Load([ref]$message)
    Assert-That ($null -ne $message -and !$loaded.IsNight) 'Malformed settings safely fall back to defaults'
    $listener.Flush()
    Assert-That ([string]::IsNullOrWhiteSpace($bindingLog.ToString())) ('No WPF binding errors: ' + $bindingLog.ToString())
    $checks | Set-Content (Join-Path $root 'artifacts/verification-results.txt') -Encoding UTF8
    $checks
}
finally {
    if ($window) { $window.Close() }
    [System.Diagnostics.PresentationTraceSources]::DataBindingSource.Listeners.Remove($listener)
    $app.Shutdown()
    $resolvedTest = [System.IO.Path]::GetFullPath($testDirectory)
    $allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [System.IO.Path]::DirectorySeparatorChar
    if ($resolvedTest.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedTest) -match '^verification-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}









