# 昼夜之间 · Daylight

一个原生 WPF 桌面应用，保留项目原有的 **.NET Framework 4.7.2**，无需第三方 NuGet 包。

## 启动

- 在 Visual Studio 中打开 `BackgroundChanged.sln`，按 **F5** 运行。
- 或直接运行 `BackgroundChanged/bin/Release/BackgroundChanged.exe`。
- 运行环境：Windows + .NET Framework 4.7.2 或更高的 4.x 版本；从源码编译需要 WPF/.NET Framework 开发工具。

## 使用

- 功能面板每次启动保留玻璃标题卡片，默认收起功能按钮列表。点击卡片底部“↓”展开列表，点击“↑”只收起列表，卡片随之缩短，标题、说明和数量保留。收起再展开会保留原来的滚动位置。
- 顶部“收起空间 / 功能空间”入口继续支持隐藏或恢复整个面板；隐藏整个面板后释放左侧布局空间，时间和昼夜开关恢复居中。
- 左侧“随心开启”为玻璃质感功能面板，使用 `Grid → ScrollViewer → ItemsControl → Button` 结构，支持鼠标滚轮、触摸滚动和键盘聚焦。半透明渐变、圆角高光和阴影随昼夜主题变化；这是原生 WPF 的玻璃质感表现，未使用系统背景模糊或折射着色器。
- 面板内置 12 个示例入口，点击后打开对应占位窗体；同一入口重复点击会激活已有窗体，关闭后可重新打开。实际业务功能尚待接入。
- 修改 `Controls/GlassLauncher.xaml.cs` 的 `Features` 可增删按钮及调整名称、图标和说明；在 `MainWindow.xaml.cs` 的 `CreateFeatureWindow` 中按 `feature.Id` 返回自己的 `Window`，即可接入实际窗体。主界面统一处理 Owner、重复打开与关闭清理。
- 点击中央太阳/月亮开关切换昼夜，也可以 Tab 聚焦后按空格。
- 约 620 毫秒内完成圆球滚动、太阳/月亮交替、云朵/星星淡入淡出和整页背景交叉过渡。圆球向右顺时针滚动、向左逆时针滚回；位移与转角由同一个进度驱动，按移动距离/半径计算转角（约 149°）。太阳高光和月亮斑点随圆球旋转，月亮停稳后保持原本的图案方向。连续操作从当前位置和角度转向最后选择。尊重 Windows 的减少动画设置。
- 右上角“背景设置”展开侧边面板，点击面板外区域、关闭按钮或 Esc 关闭。
- 日间、夜间卡片选择的是**正在编辑的背景**，不会直接改变主界面主题。关闭面板并点击主开关可查看另一套背景。
- 两套背景均支持内置风景渐变、纯色、图片。纯色值在输入框失去焦点后生效。
- 图片默认等比填满并裁剪，勾选“完整显示”后等比完整显示，多余空间使用默认底色。
- “柔和遮罩”可以降低背景干扰；“恢复当前背景的默认设置”只重置正在编辑的那套背景。
- 当前主题、背景类型、图片、颜色、显示方式和遮罩自动保存，重启后恢复。
- 默认开启鼠标流光：在窗口内移动鼠标形成光带，默认约 0.65 秒后消失，不影响按钮点击。
- 在“背景设置”中向下滚动到“鼠标流光”，可调整配色、留影时长（0.2～1.5 秒）、强度、宽度（WPF 逻辑像素）及减少动画偏好。关闭面板后查看效果。
- 配色提供“极光”“霓虹”“单色呼吸”；单色呼吸为绿色，另外两种按约 8 秒一轮循环变色。日间与夜间自动调整外围光晕强度。
- 打开设置时暂停生成新轨迹，已有轨迹继续消散；离开窗口或长时间停顿后开始新轨迹。失去焦点、最小化、关闭效果时清理轨迹。
- 默认遵循 Windows 客户区动画偏好；系统禁用动画时可在设置中取消该选项以单独启用拖尾。

## 本地数据

设置文件：`%LOCALAPPDATA%/BackgroundChanged/settings.xml`。

导入图片复制到 `%LOCALAPPDATA%/BackgroundChanged/Backgrounds/`，之后移动原图不影响显示。替换或重置后，成功保存设置时清理不再引用的应用导入图片。图片缺失、格式损坏或设置文件损坏时回退到内置背景/默认设置。

## 结构

- `MainWindow.xaml`：主界面、时间、设置抽屉。
- `Controls/GlassLauncher.xaml`：玻璃功能面板、可滚动按钮列表及交互样式。
- `Controls/GlassLauncher.xaml.cs`：示例入口定义与 `FeatureRequested` 事件。
- `Controls/DayNightSwitch.xaml`：矢量胶囊开关、自定义 ToggleButton 模板。
- `Controls/DayNightSwitch.xaml.cs`：可中断的开关动画、IsNight 依赖属性。
- `Themes/Day.xaml`、`Themes/Night.xaml`：动态主题资源。
- `ViewModels/MainViewModel.cs`：界面状态、编辑命令、时钟与保存调度。
- `Models/AppSettings.cs`：日夜配置模型。
- `Services/BackgroundFactory.cs`：矢量风景绘制、图片解码与回退。
- `Services/SettingsService.cs`：XML 配置、图片导入与应用资源清理。
- `Models/MouseTrailSettings.cs`：拖尾持久配置、默认值和范围校验；兼容旧版配置文件。
- `ViewModels/MouseTrailSettingsViewModel.cs`：子 ViewModel，提供配置绑定、恢复默认命令，修改后通知主 ViewModel 自动保存。
- `Controls/MouseTrailOverlay.cs`：FrameworkElement 绘制层，依赖属性接收配置，观察 InputSource 的鼠标事件，用 CompositionTarget.Rendering 驱动消散；不引用 ViewModel。
- `Controls/TrailBuffer.cs`：只保存瞬态位置、单调时间戳、颜色相位和轨迹断点；过滤抖动、按时间清理，上限 128 点。

拖尾数据流：设置控件 → 子 ViewModel → 依赖属性 → 绘制层；鼠标移动 → 绘制层 → 临时轨迹缓存 → OnRender。鼠标位置和逐帧更新不经过 PropertyChanged。每段曲线由多层半透明笔触绘制，笔对象缓存上限 4096，轨迹全部消失后解除逐帧订阅，卸载时解除输入、窗口、系统偏好和渲染事件。

## 构建与验证

在 Visual Studio Developer PowerShell 中执行：

```powershell
msbuild BackgroundChanged.sln /p:Configuration=Release /v:minimal
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/Verify.ps1 -Configuration Release
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/VerifyTrail.ps1 -Configuration Release
```

验证脚本在隔离目录中测试真实 WPF 窗口、ToggleButton 双向绑定、动画终态、快速切换、资源字典、设置面板、配置恢复、图片解码和异常回退，并输出截图与 `artifacts/verification-results.txt`，不修改正常运行时的个人配置。

`artifacts/day-preview.png`、`artifacts/night-preview.png`、`artifacts/settings-preview.png` 为程序直接渲染的界面截图。

拖尾验证结果见 `artifacts/trail-verification-results.txt`，包括配置绑定与持久化、旧配置兼容、采样上限、断线处理、消散停止、暂停、最小化、卸载事件释放和 WPF 绑定错误检查。`artifacts/trail-day-preview.png`、`artifacts/trail-night-preview.png` 使用模拟轨迹输入到实际绘制控件生成，`artifacts/trail-settings-preview.png` 展示新增设置。

## FlipPanel 3D 翻转控件

主界面“自定义按钮”右侧的“3D 翻转面板”打开独立演示窗体，重复点击会激活已有窗体，关闭后可重新打开。演示提供正反面按钮、背面输入框、200～1600 ms 时长调节，以及“显示正面 / 显示背面”。卡片聚焦时空格或 Enter 可翻转；输入框内的正常输入不触发卡片快捷键。

控件位于自定义控件库中，无需新增 NuGet 依赖：

- `WpfCustomControlLibrary1/FilpPanel.cs`：完整 `FlipPanel` 控件和兼容原名称的 `FilpPanel`。
- `WpfCustomControlLibrary1/Themes/FilpPanelDic.xaml`：默认模板，由现有 `Generic.xaml` 加载。
- `BackgroundChanged/View/FlipPanelWindow.xaml`：可直接运行的演示界面。

复用示例（窗口上声明 `xmlns:custom="clr-namespace:WpfCustomControlLibrary1;assembly=WpfCustomControlLibrary1"`）：

```xml
<custom:FlipPanel x:Name="Card" Width="500" Height="320"
                  IsFlipped="{Binding ShowBack, Mode=TwoWay}"
                  AnimationDuration="650" Radius="24" Padding="20">
    <custom:FlipPanel.FrontContent>
        <StackPanel>
            <TextBlock Text="正面内容"/>
            <Button Content="翻到背面"
                    Command="{x:Static custom:FlipPanel.FlipCommand}"
                    CommandTarget="{Binding ElementName=Card}"/>
        </StackPanel>
    </custom:FlipPanel.FrontContent>
    <custom:FlipPanel.BackContent>
        <TextBox Text="可编辑的背面内容"/>
    </custom:FlipPanel.BackContent>
</custom:FlipPanel>
```

也可调用 `Card.Flip()` 或设置 `IsFlipped`。`IsAnimating` 和 `RotationAngle` 为只读状态，适合展示运行状态。`AnimationDuration` 单位为毫秒，设为 0 立即切换；系统关闭客户端动画时也直接显示目标面。原有 `FilpPanel`、`IsFilp` 和 `Radius` 保持兼容。

动画通过 `PerspectiveCamera + Viewport2DVisual3D + AxisAngleRotation3D` 实现，正反面使用不同的朝向，背面文字不会镜像。连续翻转从当前角度重新定向；动画期间暂时禁用两面的交互，结束后只启用当前面，内容和输入状态持续保留。控件卸载时取消动画并归位到目标面。

验证命令：

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/VerifyFlip.ps1 -Configuration Release
```

结果见 `artifacts/flip-verification-results.txt`；正面、背面和翻转中截图分别为 `artifacts/flip-front-preview.png`、`artifacts/flip-back-preview.png`、`artifacts/flip-midpoint-preview.png`。
