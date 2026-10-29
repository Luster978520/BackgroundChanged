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
$demo = $null
try {
    $service = New-Object BackgroundChanged.Services.SettingsService($testDirectory)
    $vm = New-Object BackgroundChanged.ViewModels.MainViewModel($service)
    $window = New-Object BackgroundChanged.MainWindow($vm)
    $window.ShowInTaskbar = $false
    $window.Left = -10000; $window.Top = -10000
    $window.Show()
    Pump 100
    $entry = $window.FindName('FlipPanelButton')
    $entry.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($window.OwnedWindows.Count -eq 1) 'Main entry opens a separate owned FlipPanel demo window'
    $demo = $window.OwnedWindows[0]
    $entry.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Assert-That ($window.OwnedWindows.Count -eq 1) 'Repeated entry reuses the demo window'
    $demo.Left = -10000; $demo.Top = -10000
    $panel = $demo.FindName('DemoPanel')
    $front = $panel.Template.FindName('PART_Front', $panel)
    $back = $panel.Template.FindName('PART_Back', $panel)
    Assert-That (!$panel.IsFlipped -and $panel.RotationAngle -eq 0 -and $front.IsEnabled -and !$back.IsEnabled) 'Initial front is visible and only its content accepts input'
    $viewport = $panel.Template.FindName('PART_Viewport', $panel)
    Assert-That ($viewport.Camera -is [Windows.Media.Media3D.PerspectiveCamera] -and $viewport.Children.Count -eq 3) 'Control uses two real 3D faces and a perspective camera'
    Render $demo 'flip-front-preview'
    $demo.FindName('SpeedSlider').Value = 1200
    $frontButton = $demo.FindName('FrontFlipButton')
    Assert-That ($frontButton.CommandTarget -eq $panel) 'Button inside the 3D front binds to the panel command target'
    $frontButton.Command.Execute($null, $frontButton.CommandTarget)
    Pump 450
    if ([Windows.SystemParameters]::ClientAreaAnimation) {
        Assert-That ($panel.IsAnimating -and $panel.RotationAngle -gt 0 -and $panel.RotationAngle -lt 180) 'Flip animates through intermediate 3D angles'
        Assert-That (!$front.IsEnabled -and !$back.IsEnabled) 'Both surfaces stop accepting input during motion'
        Render $demo 'flip-midpoint-preview'
        $angle = $panel.RotationAngle
        $panel.Flip()
        Assert-That ([Math]::Abs($panel.RotationAngle - $angle) -lt 1) 'Interrupted flip reverses from its current angle without jumping'
        Pump 1200
        Assert-That (!$panel.IsFlipped -and !$panel.IsAnimating -and $panel.RotationAngle -eq 0) 'Reversed animation settles on the front'
        $panel.Flip()
    }
    Pump 1300
    Assert-That ($panel.IsFlipped -and $panel.RotationAngle -eq 180 -and !$panel.IsAnimating -and $back.IsEnabled -and !$front.IsEnabled) 'Back settles at 180 degrees and accepts input'
    $backRotation = $panel.Template.FindName('PART_BackRotation', $panel)
    Assert-That ($backRotation.Angle -eq 360) 'Back text faces forward without mirroring'
    $demo.FindName('BackNote').Text = 'Persistent back content'
    Render $demo 'flip-back-preview'
    $panel.AnimationDuration = 0
    $panel.Flip()
    Assert-That (!$panel.IsFlipped -and $panel.RotationAngle -eq 0 -and !$panel.IsAnimating) 'Zero duration switches instantly'
    $panel.Flip()
    Assert-That ($demo.FindName('BackNote').Text -eq 'Persistent back content') 'Back content retains its state across flips'
    $legacy = New-Object WpfCustomControlLibrary1.FilpPanel
    $legacy.IsFilp = $true
    Assert-That ($legacy.IsFlipped -and $legacy.RotationAngle -eq 180) 'Original FilpPanel and IsFilp remain compatible'
    $demo.Close(); $demo = $null
    $entry.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Pump 100
    Assert-That ($window.OwnedWindows.Count -eq 1) 'Closed demo can be reopened'
    $demo = $window.OwnedWindows[0]
    $demo.FindName('DemoPanel').Flip()
    $demo.Close(); $demo = $null
    Pump 100
    $window.Width = 820; $window.Height = 640
    Pump 100
    $settings = $window.FindName('SettingsButton')
    $entryBounds = $entry.TransformToAncestor($window.Content).TransformBounds((New-Object Windows.Rect(0,0,$entry.ActualWidth,$entry.ActualHeight)))
    $settingsBounds = $settings.TransformToAncestor($window.Content).TransformBounds((New-Object Windows.Rect(0,0,$settings.ActualWidth,$settings.ActualHeight)))
    Assert-That ($entryBounds.Right -lt $settingsBounds.Left) 'Header buttons remain separate at the minimum window width'
    Render $window 'flip-entry-preview'
    Assert-That ([string]::IsNullOrWhiteSpace($bindingLog.ToString())) ('No WPF binding errors: ' + $bindingLog.ToString())
    $checks | Set-Content (Join-Path $root 'artifacts/flip-verification-results.txt') -Encoding UTF8
    $checks | ForEach-Object { Write-Output $_ }
}
finally {
    if ($null -ne $demo) { $demo.Close() }
    if ($null -ne $window) { $window.Close() }
    [System.Diagnostics.PresentationTraceSources]::DataBindingSource.Listeners.Remove($listener)
    $app.Shutdown()
}

