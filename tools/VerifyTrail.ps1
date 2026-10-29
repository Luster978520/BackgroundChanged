param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$root = Split-Path -Parent $PSScriptRoot
[Reflection.Assembly]::LoadFrom((Join-Path $root "BackgroundChanged/bin/$Configuration/BackgroundChanged.exe")) | Out-Null
[Windows.Application]::ResourceAssembly = [BackgroundChanged.App].Assembly
$app = New-Object BackgroundChanged.App
$app.InitializeComponent()
# Prevent the queued Application startup from creating a second window with personal settings.
[Windows.Application].GetField('_startupUri',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($app,$null)
$app.ShutdownMode = 'OnExplicitShutdown'
$testDirectory = Join-Path $root ('artifacts/trail-verification-' + [Guid]::NewGuid().ToString('N'))
$checks = New-Object 'System.Collections.Generic.List[string]'
$private = [Reflection.BindingFlags]'Instance,NonPublic'
function Check($condition, [string]$description) {
    if (!$condition) { throw "FAILED: $description" }
    $checks.Add("PASS: $description")
}
function Field($target, [string]$name) { $target.GetType().GetField($name, $private).GetValue($target) }
function Call($target, [string]$name, [object[]]$values = @()) {
    $target.GetType().GetMethod($name, [Reflection.BindingFlags]'Instance,Public,NonPublic').Invoke($target,$values)
}
function Add-Point($target, [double]$x, [double]$y, [double]$time) {
    Call $target 'Add' @([Windows.Point]::new($x,$y),$time)
}
function Pump([int]$milliseconds = 100) {
    $frame = New-Object Windows.Threading.DispatcherFrame
    $timer = New-Object Windows.Threading.DispatcherTimer
    $timer.Interval = [TimeSpan]::FromMilliseconds($milliseconds); $timer.Tag = $frame
    $timer.Add_Tick({param($s,$e) $s.Stop(); $s.Tag.Continue = $false})
    $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Render($window, [string]$name) {
    $window.UpdateLayout(); $visual = $window.Content
    $image = New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$visual.ActualWidth,[int]$visual.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $image.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
    $stream = [IO.File]::Create((Join-Path $root "artifacts/$name.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
function Seed-Trail($trail) {
    $data = Field $trail 'buffer'; Call $data 'Clear'
    $now = (Field $trail 'clock').Elapsed.TotalSeconds
    for ($i = 0; $i -lt 86; $i++) {
        $x = 155 + $i * 10
        $y = 455 - [Math]::Sin($i / 19.0) * 78
        Add-Point $data $x $y ($now - .61 + $i * .007) | Out-Null
    }
    $trail.GetType().GetField('frameTime',$private).SetValue($trail,$now)
    $trail.InvalidateVisual()
    Call $trail 'StartRendering'
}
$bindingLog = New-Object IO.StringWriter
$listener = New-Object Diagnostics.TextWriterTraceListener($bindingLog)
[Diagnostics.PresentationTraceSources]::DataBindingSource.Listeners.Add($listener) | Out-Null
[Diagnostics.PresentationTraceSources]::DataBindingSource.Switch.Level = 'Warning'
$window = $null
try {
    $service = New-Object BackgroundChanged.Services.SettingsService($testDirectory)
    $vm = New-Object BackgroundChanged.ViewModels.MainViewModel($service)
    Check ($vm.MouseTrailSettings.IsEnabled -and [Math]::Abs($vm.MouseTrailSettings.Lifetime - .65) -lt .001) 'New and old configurations have safe trail defaults'
    $window = New-Object BackgroundChanged.MainWindow($vm)
    $window.WindowStartupLocation = 'Manual'; $window.Left = -10000; $window.Top = -10000; $window.ShowInTaskbar = $false
    $window.Show(); Pump
    $trail = $window.FindName('MouseTrail')
    $vm.MouseTrailSettings.RespectReducedMotion = $false
    Check (!$trail.IsHitTestVisible -and !$trail.Focusable) 'Drawing overlay cannot intercept mouse clicks or keyboard focus'
    Check ([object]::ReferenceEquals($trail.InputSource,$window.FindName('SceneInput'))) 'Overlay observes the complete scene, including empty background'
    Check ([object]::ReferenceEquals((Field $trail 'attachedSource'),$trail.InputSource)) 'Loaded overlay attaches to its configured input source'
    Check (!(Field $trail 'rendering')) 'Idle overlay does not subscribe to per-frame rendering'
    $vm.MouseTrailSettings.Lifetime = 1.1
    $vm.MouseTrailSettings.TrailWidth = 8
    $vm.MouseTrailSettings.Intensity = .7
    $vm.MouseTrailSettings.Palette = '霓虹'
    Check ([Math]::Abs($trail.Lifetime - 1.1) -lt .001 -and $trail.TrailWidth -eq 8 -and $trail.Palette -eq '霓虹') 'Child ViewModel settings update the drawing dependency properties'
    $vm.MouseTrailSettings.ResetCommand.Execute($null)
    $vm.MouseTrailSettings.RespectReducedMotion = $false
    Check ($trail.Palette -eq '极光' -and $trail.TrailWidth -eq 6) 'Reset command restores default trail configuration'

    $bufferType = [BackgroundChanged.App].Assembly.GetType('BackgroundChanged.Controls.TrailBuffer')
    $data = [Activator]::CreateInstance($bufferType,$true)
    Check (Add-Point $data 10 10 0) 'First sample begins a stroke'
    Check (!(Add-Point $data 11 10 .01)) 'Sub-two-pixel jitter is filtered'
    Add-Point $data 30 10 .02 | Out-Null
    Call $data 'Break'
    Add-Point $data 800 400 .03 | Out-Null
    Check $data.Samples[2].StartsStroke 'Leaving and reentering never draws a line across the gap'
    Add-Point $data 830 410 .3 | Out-Null
    Check $data.Samples[3].StartsStroke 'Resuming after a pause begins a separate stroke'
    for ($i = 1; $i -le 300; $i++) { Add-Point $data ($i*3) 60 (.3+$i*.001) | Out-Null }
    Check ($data.Samples.Count -eq 128) 'High-rate input is bounded to 128 samples'
    Call $data 'Prune' @([double]3,[double]0.65)
    Check ($data.Samples.Count -eq 0) 'Time-based expiry removes every old point'
    $green = Call $trail 'PaletteColor' @([double]0)
    $blue = Call $trail 'PaletteColor' @([double]0.5)
    Check ($green -ne $blue) 'Aurora palette changes color across its cycle'
    $vm.MouseTrailSettings.Palette = '单色呼吸'
    Check ((Call $trail 'PaletteColor' @([double]0)) -eq (Call $trail 'PaletteColor' @([double]0.5))) 'Green breathing mode preserves a single hue'
    $vm.MouseTrailSettings.Palette = '极光'

    # Inject deterministic points into the same renderer used by real mouse events.
    Seed-Trail $trail
    Check (!$vm.IsNight -and $app.Resources['ForegroundBrush'].Color.ToString() -eq '#FF203F52') 'Trail controls preserve the active day theme resources'
    Render $window 'trail-day-preview'
    Check ((Field $trail 'buffer').Samples.Count -gt 2 -and (Field $trail 'rendering')) 'A trajectory starts the animation loop'
    Pump 800
    Check ((Field $trail 'buffer').Samples.Count -eq 0 -and !(Field $trail 'rendering')) 'Stopping input fades all points and releases the rendering subscription'
    $vm.IsNight = $true; Pump 750
    Seed-Trail $trail
    Render $window 'trail-night-preview'
    $vm.IsSettingsOpen = $true
    Check $trail.IsPaused 'Opening settings pauses new samples through binding'
    $beforeCount = (Field $trail 'buffer').Samples.Count
    Call $trail 'RecordSample' @([Windows.Point]::new(400,200))
    Check ((Field $trail 'buffer').Samples.Count -eq $beforeCount) 'Paused overlay rejects new mouse samples'
    Pump 800
    Check (!(Field $trail 'rendering')) 'Existing trail still expires while settings are open'
    $scroll = $null
    function Find-Scroll($visual) {
        if ($visual -is [Windows.Controls.ScrollViewer]) { return $visual }
        for ($j=0; $j -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($visual); $j++) {
            $found = Find-Scroll ([Windows.Media.VisualTreeHelper]::GetChild($visual,$j))
            if ($found) { return $found }
        }
    }
    $scroll = Find-Scroll $window.FindName('SettingsDrawer')
    $scroll.ScrollToBottom(); Pump
    Render $window 'trail-settings-preview'
    $vm.IsSettingsOpen = $false
    Call $trail 'RecordSample' @([Windows.Point]::new(400,200))
    Call $trail 'RecordSample' @([Windows.Point]::new(420,205))
    Check (Field $trail 'rendering') 'Input resumes after settings close'
    $vm.MouseTrailSettings.IsEnabled = $false
    Check ((Field $trail 'buffer').Samples.Count -eq 0 -and !(Field $trail 'rendering')) 'Disabling immediately clears and stops the effect'
    $vm.MouseTrailSettings.IsEnabled = $true
    Call $trail 'RecordSample' @([Windows.Point]::new(400,200))
    Call $trail 'RecordSample' @([Windows.Point]::new(440,205))
    $window.WindowState = 'Minimized'; Pump
    Check (!(Field $trail 'rendering') -and (Field $trail 'buffer').Samples.Count -eq 0) 'Minimizing clears stale coordinates and rendering'
    $window.WindowState = 'Normal'; Pump
    $vm.MouseTrailSettings.Lifetime = 1.2; $vm.MouseTrailSettings.Intensity = .6
    $vm.MouseTrailSettings.TrailWidth = 7; $vm.MouseTrailSettings.Palette = '霓虹'
    $window.Close(); $window = $null
    Pump
    Check (!(Field $trail 'rendering') -and $null -eq (Field $trail 'attachedSource')) 'Unloading detaches both input and rendering events'
    $restored = New-Object BackgroundChanged.ViewModels.MainViewModel($service)
    Check ($restored.MouseTrailSettings.Palette -eq '霓虹' -and [Math]::Abs($restored.MouseTrailSettings.Lifetime - 1.2) -lt .001 -and $restored.MouseTrailSettings.TrailWidth -eq 7) 'Trail configuration persists across a new ViewModel'
    $restored.Dispose()
    [IO.File]::WriteAllText((Join-Path $testDirectory 'settings.xml'),'<AppSettings><IsNight>true</IsNight></AppSettings>')
    $message = $null; $old = $service.Load([ref]$message)
    Check ($old.IsNight -and $old.MouseTrail.IsEnabled -and $old.MouseTrail.Palette -eq '极光') 'Legacy XML without a MouseTrail element retains its theme and gains defaults'
    [IO.File]::WriteAllText((Join-Path $testDirectory 'settings.xml'),'<AppSettings><MouseTrail><Lifetime>NaN</Lifetime><Intensity>99</Intensity><TrailWidth>-10</TrailWidth><Palette>unknown</Palette></MouseTrail></AppSettings>')
    $bad = $service.Load([ref]$message)
    Check ($bad.MouseTrail.Lifetime -eq .65 -and $bad.MouseTrail.Intensity -eq 1 -and $bad.MouseTrail.TrailWidth -eq 3 -and $bad.MouseTrail.Palette -eq '极光') 'Out-of-range and nonfinite persisted values normalize safely'
    $listener.Flush()
    Check ([string]::IsNullOrWhiteSpace($bindingLog.ToString())) ('No WPF binding errors: ' + $bindingLog.ToString())
    $checks | Set-Content (Join-Path $root 'artifacts/trail-verification-results.txt') -Encoding UTF8
    $checks
}
finally {
    if ($window) { $window.Close() }
    [Diagnostics.PresentationTraceSources]::DataBindingSource.Listeners.Remove($listener)
    $app.Shutdown()
    $resolvedTest = [IO.Path]::GetFullPath($testDirectory)
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if ($resolvedTest.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedTest) -match '^trail-verification-[0-9a-f]{32}$' -and (Test-Path -LiteralPath $resolvedTest)) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}







