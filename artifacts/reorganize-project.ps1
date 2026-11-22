$ErrorActionPreference = 'Stop'
$repoRoot = 'C:\Users\ADMIN\source\repos\Pomodoro'
$projectRoot = Join-Path $repoRoot 'Pomodoro'
$moves = @{
 'MainWindow.xaml'='Shell/MainWindow.xaml'; 'MainWindow.xaml.cs'='Shell/MainWindow.xaml.cs'
 'Controls/TimerDisplay.xaml'='Features/Timer/Controls/TimerDisplay.xaml'; 'Controls/TimerDisplay.xaml.cs'='Features/Timer/Controls/TimerDisplay.xaml.cs'
 'CountdownSession.cs'='Features/Timer/CountdownSession.cs'; 'TimerPage.cs'='Features/Timer/TimerPage.cs'; 'Interface/ITimerControl.cs'='Features/Timer/ITimerControl.cs'
 'Page/workPage.xaml'='Features/Timer/Pages/WorkPage.xaml'; 'Page/workPage.xaml.cs'='Features/Timer/Pages/WorkPage.xaml.cs'
 'Page/restPage.xaml'='Features/Timer/Pages/RestPage.xaml'; 'Page/restPage.xaml.cs'='Features/Timer/Pages/RestPage.xaml.cs'
 'Page/longRestPage.xaml'='Features/Timer/Pages/LongRestPage.xaml'; 'Page/longRestPage.xaml.cs'='Features/Timer/Pages/LongRestPage.xaml.cs'
 'Page/timeUpPage.xaml'='Features/Timer/Pages/TimeUpPage.xaml'; 'Page/timeUpPage.xaml.cs'='Features/Timer/Pages/TimeUpPage.xaml.cs'
 'Page/Setting.xaml'='Features/Settings/SettingsWindow.xaml'; 'Page/Setting.xaml.cs'='Features/Settings/SettingsWindow.xaml.cs'
 'AppSetting.cs'='Features/Settings/AppSetting.cs'; 'BackgroundController.cs'='Features/Background/BackgroundController.cs'; 'BackgroundOptions.cs'='Features/Background/BackgroundOptions.cs'
 'ThemeManager.cs'='Themes/ThemeManager.cs'; 'Theme.xaml'='Themes/Theme.xaml'; 'Alarm.cs'='Services/Alarm.cs'; 'AssemblyInfo.cs'='Properties/AssemblyInfo.cs'
}
# Preserve all current edits, including changes made before this restructuring.
$backupRoot = Join-Path $repoRoot 'artifacts/restructure-source-backup'
foreach ($source in $moves.Keys) {
 $sourcePath = Join-Path $projectRoot $source
 $backupPath = Join-Path $backupRoot $source
 New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($backupPath)) | Out-Null
 Copy-Item -LiteralPath $sourcePath -Destination $backupPath
 $targetPath = Join-Path $projectRoot $moves[$source]
 if (Test-Path -LiteralPath $targetPath) { throw "Target already exists: $targetPath" }
 New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
 Move-Item -LiteralPath $sourcePath -Destination $targetPath
}
# Move media as directories so no original media is discarded.
New-Item -ItemType Directory -Force (Join-Path $projectRoot 'Assets') | Out-Null
foreach ($pair in @(@('Asset/Image','Assets/Images'), @('Asset/Alarm','Assets/Sounds'))) {
 $mediaSource = [IO.Path]::GetFullPath((Join-Path $projectRoot $pair[0]))
 $mediaTarget = [IO.Path]::GetFullPath((Join-Path $projectRoot $pair[1]))
 if (-not $mediaSource.StartsWith($projectRoot + '\') -or -not $mediaTarget.StartsWith($projectRoot + '\')) { throw 'Media path outside project' }
 Move-Item -LiteralPath $mediaSource -Destination $mediaTarget
}
$names = @{ 'workPage'='WorkPage'; 'restPage'='RestPage'; 'longRestPage'='LongRestPage'; 'timeUpPage'='TimeUpPage'; 'Setting'='SettingsWindow' }
$files = @(Get-ChildItem (Join-Path $projectRoot 'Shell'),(Join-Path $projectRoot 'Features'),(Join-Path $projectRoot 'Themes'),(Join-Path $projectRoot 'Services') -File -Recurse)
foreach ($file in $files) {
 $content = [IO.File]::ReadAllText($file.FullName)
 foreach ($name in $names.Keys) { $content = [regex]::Replace($content, '\b' + $name + '\b', $names[$name]) }
 if ($file.Extension -eq '.cs') {
  $relative = [IO.Path]::GetRelativePath($projectRoot, $file.DirectoryName).Replace('\','.')
  $content = [regex]::Replace($content, 'namespace Pomodoro(?:\.Controls|\.Page)?(?=\s*[;{])', 'namespace Pomodoro.' + $relative)
  # ITimerControl previously used a dedicated Interface namespace.
  $content = $content.Replace('namespace Pomodoro.Interface;', 'namespace Pomodoro.Features.Timer;')
  $content = $content.Replace('using Pomodoro.Interface;', 'using Pomodoro.Features.Timer;').Replace('using Pomodoro.Controls;', 'using Pomodoro.Features.Timer.Controls;').Replace('using Pomodoro.Page;', 'using Pomodoro.Features.Timer.Pages;')
 } else {
  $content = $content.Replace('Pomodoro.MainWindow','Pomodoro.Shell.MainWindow').Replace('Pomodoro.Controls.TimerDisplay','Pomodoro.Features.Timer.Controls.TimerDisplay').Replace('Pomodoro.Page.SettingsWindow','Pomodoro.Features.Settings.SettingsWindow').Replace('Pomodoro.Page.TimeUpPage','Pomodoro.Features.Timer.Pages.TimeUpPage')
  foreach ($name in @('WorkPage','RestPage','LongRestPage')) { $content = $content.Replace('Pomodoro.' + $name,'Pomodoro.Features.Timer.Pages.' + $name) }
  $content = $content.Replace('xmlns:theme="clr-namespace:Pomodoro"','xmlns:theme="clr-namespace:Pomodoro.Themes"').Replace('clr-namespace:Pomodoro.Controls','clr-namespace:Pomodoro.Features.Timer.Controls').Replace('xmlns:local="clr-namespace:Pomodoro"','xmlns:local="clr-namespace:Pomodoro.Features.Timer"')
 }
 [IO.File]::WriteAllText($file.FullName, $content)
}
$imports = @{
 'App.xaml.cs'=@('Pomodoro.Features.Settings','Pomodoro.Themes')
 'Shell/MainWindow.xaml.cs'=@('Pomodoro.Features.Background','Pomodoro.Features.Settings','Pomodoro.Themes','Pomodoro.Services')
 'Features/Timer/Pages/WorkPage.xaml.cs'=@('Pomodoro.Features.Timer','Pomodoro.Features.Settings')
 'Features/Timer/Pages/RestPage.xaml.cs'=@('Pomodoro.Features.Timer','Pomodoro.Features.Settings')
 'Features/Timer/Pages/LongRestPage.xaml.cs'=@('Pomodoro.Features.Timer','Pomodoro.Features.Settings')
 'Features/Timer/Pages/TimeUpPage.xaml.cs'=@('Pomodoro.Services')
 'Features/Settings/SettingsWindow.xaml.cs'=@('Pomodoro.Features.Background','Pomodoro.Themes')
 'Features/Settings/AppSetting.cs'=@('Pomodoro.Features.Background','Pomodoro.Themes','Pomodoro.Services')
 'Features/Background/BackgroundController.cs'=@('Pomodoro.Themes')
}
foreach ($file in $imports.Keys) {
 $path = Join-Path $projectRoot $file
 $content = [IO.File]::ReadAllText($path)
 $header = ($imports[$file] | ForEach-Object { "using $_;" }) -join "`r`n"
 [IO.File]::WriteAllText($path, $header + "`r`n" + $content)
}
$path = Join-Path $projectRoot 'Features/Settings/AppSetting.cs'
$content = [IO.File]::ReadAllText($path)
$match = [regex]::Match($content, 'internal sealed record SettingsData\([^;]+;')
[IO.File]::WriteAllText((Join-Path $projectRoot 'Features/Settings/SettingsData.cs'), "using Pomodoro.Features.Background;`r`nusing Pomodoro.Themes;`r`n`r`nnamespace Pomodoro.Features.Settings;`r`n`r`n" + $match.Value + "`r`n")
[IO.File]::WriteAllText($path, $content.Replace($match.Value + "`r`n`r`n",''))
$path = Join-Path $projectRoot 'Themes/ThemeManager.cs'
$content = [IO.File]::ReadAllText($path)
$start = $content.IndexOf('[JsonConverter(')
$end = $content.IndexOf('internal static class ThemeManager')
$declarations = $content.Substring($start,$end-$start)
$enumEnd = $declarations.IndexOf('// Each independently')
[IO.File]::WriteAllText((Join-Path $projectRoot 'Themes/AppTheme.cs'), "using System.Text.Json.Serialization;`r`n`r`nnamespace Pomodoro.Themes;`r`n`r`n" + $declarations.Substring(0,$enumEnd))
[IO.File]::WriteAllText((Join-Path $projectRoot 'Themes/ThemeResources.cs'), "using System.Windows;`r`n`r`nnamespace Pomodoro.Themes;`r`n`r`n" + $declarations.Substring($enumEnd).Replace('/Pomodoro;component/Theme.xaml','/Pomodoro;component/Themes/Theme.xaml'))
[IO.File]::WriteAllText($path, $content.Remove($start,$end-$start).Replace("using System.Text.Json.Serialization;`r`n",''))
$path = Join-Path $projectRoot 'App.xaml'
[IO.File]::WriteAllText($path, [IO.File]::ReadAllText($path).Replace('StartupUri="MainWindow.xaml"','StartupUri="Shell/MainWindow.xaml"'))
$path = Join-Path $projectRoot 'Services/Alarm.cs'
[IO.File]::WriteAllText($path, [IO.File]::ReadAllText($path).Replace('"Asset", "Alarm"','"Assets", "Sounds"'))
$path = Join-Path $projectRoot 'Pomodoro.csproj'
[IO.File]::WriteAllText($path, [IO.File]::ReadAllText($path).Replace('Asset\Image\','Assets\Images\').Replace('Asset\Alarm\','Assets\Sounds\'))
# Tests follow the public type and media path moves; their assertions remain intact.
foreach ($test in @('tests/Pomodoro.RegressionTests/Program.cs','tests/Pomodoro.BackgroundTests/Program.cs')) {
 $path = Join-Path $repoRoot $test
 $content = [IO.File]::ReadAllText($path)
 foreach ($name in $names.Keys) { $content = [regex]::Replace($content, '\b' + $name + '\b', $names[$name]) }
 $content = $content.Replace('using Pomodoro.Controls;', 'using Pomodoro.Features.Timer.Controls;').Replace('using Pomodoro.Page;', 'using Pomodoro.Features.Timer.Pages;').Replace('Pomodoro/Asset/Image/', 'Pomodoro/Assets/Images/')
 $content = "using Pomodoro.Shell;`r`nusing Pomodoro.Features.Timer;`r`nusing Pomodoro.Features.Background;`r`nusing Pomodoro.Features.Settings;`r`nusing Pomodoro.Services;`r`nusing Pomodoro.Themes;`r`n" + $content
 [IO.File]::WriteAllText($path, $content)
}
Write-Output 'Moved source files, extracted settings/theme types, and updated XAML, media and test references.'
