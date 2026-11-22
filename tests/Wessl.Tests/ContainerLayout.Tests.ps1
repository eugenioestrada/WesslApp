# Run against an open Wessl panel with multiple containers, supplying one long name and its image.
# This UI Automation check is separate from dotnet test and requires an interactive Windows desktop.
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$ContainerName,
    [Parameter(Mandatory = $true)][string]$Image,
    [string[]]$HiddenPorts = @(),
    [string[]]$Endpoints = @(),
    [string]$StatsLoadingName,
    [string]$StatsResultPattern,
    [string]$StatsActionName,
    [int]$StatsWaitSeconds = 15
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$process = Get-Process -Id $ProcessId
if ($process.MainWindowHandle -eq 0) {
    throw 'The Wessl panel must be open before running this test.'
}

$root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
$elements = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)

# Use a delayed stats client to observe the first card before the request completes.
if ($StatsLoadingName -or $StatsResultPattern -or $StatsActionName) {
    if (-not $StatsLoadingName -or -not $StatsResultPattern -or -not $StatsActionName) {
        throw 'StatsLoadingName, StatsResultPattern and StatsActionName must be supplied together.'
    }
    $loading = $elements | Where-Object { $_.Current.Name -eq $StatsLoadingName } | Select-Object -First 1
    $action = $elements | Where-Object {
        $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
        $_.Current.Name -eq $StatsActionName
    } | Select-Object -First 1
    if ($null -eq $loading -or $null -eq $action -or $action.Current.BoundingRectangle.IsEmpty) {
        throw 'The first card must be visible and loading statistics.'
    }
    $before = $action.Current.BoundingRectangle
    $deadline = (Get-Date).AddSeconds($StatsWaitSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $elements = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        $result = $elements | Where-Object { $_.Current.Name -like $StatsResultPattern } | Select-Object -First 1
    } while ($null -eq $result -and (Get-Date) -lt $deadline)
    if ($null -eq $result) {
        throw 'The statistics result did not appear before the timeout.'
    }
    $action = $elements | Where-Object {
        $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
        $_.Current.Name -eq $StatsActionName
    } | Select-Object -First 1
    if ($null -eq $action -or $action.Current.BoundingRectangle.IsEmpty -or
        $action.Current.BoundingRectangle.Left -ne $before.Left -or
        $action.Current.BoundingRectangle.Top -ne $before.Top) {
        throw 'The first card action moved while statistics loaded.'
    }
}

$nameElement = $null
$imageElement = $null
foreach ($element in $elements) {
    if ($null -eq $nameElement -and $element.Current.Name -eq $ContainerName) {
        $nameElement = $element
    }
    elseif ($null -ne $nameElement -and $element.Current.Name -eq $Image) {
        $imageElement = $element
        break
    }
}
if ($null -eq $nameElement -or $null -eq $imageElement) {
    throw 'The requested container name or image was not found in the accessible UI.'
}

$scrollItem = $nameElement.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)
$scrollItem.ScrollIntoView()
Start-Sleep -Milliseconds 500

$viewport = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($nameElement)
$scroll = $viewport.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
if (-not $scroll.Current.VerticallyScrollable -or $scroll.Current.HorizontallyScrollable) {
    throw 'The container list must scroll vertically, without horizontal scrolling.'
}

$bounds = $viewport.Current.BoundingRectangle
$nameBounds = $nameElement.Current.BoundingRectangle
$imageBounds = $imageElement.Current.BoundingRectangle
if ($nameBounds.IsEmpty -or $imageBounds.IsEmpty -or
    $nameBounds.Left -lt $bounds.Left -or $imageBounds.Right -gt $bounds.Right -or
    $nameBounds.Right -gt $imageBounds.Left -or
    [Math]::Abs($nameBounds.Top - $imageBounds.Top) -gt 2) {
    throw "The container header exceeds its viewport or its name overlaps its image. Name: $nameBounds; image: $imageBounds; viewport: $bounds"
}

foreach ($port in $HiddenPorts) {
    if ($elements | Where-Object { $_.Current.Name -eq $port }) {
        throw "The raw port mapping should not be displayed: $port"
    }
}

foreach ($endpoint in $Endpoints) {
    $endpointElement = $elements | Where-Object {
        $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text -and $_.Current.Name -eq $endpoint
    } | Select-Object -First 1
    $buttons = @($elements | Where-Object {
        $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
        $_.Current.Name.Contains($endpoint)
    })
    if ($null -eq $endpointElement -or $buttons.Count -ne 2) {
        throw "The endpoint or its open and copy actions are missing: $endpoint"
    }

    $endpointElement.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView()
    Start-Sleep -Milliseconds 250
    $endpointBounds = $endpointElement.Current.BoundingRectangle
    $openBounds = $buttons[0].Current.BoundingRectangle
    $copyBounds = $buttons[1].Current.BoundingRectangle
    if ($endpointBounds.IsEmpty -or $openBounds.IsEmpty -or $copyBounds.IsEmpty -or
        -not $buttons[0].Current.IsEnabled -or -not $buttons[1].Current.IsEnabled -or
        $endpointBounds.Left -lt $bounds.Left -or $endpointBounds.Right -gt $openBounds.Left -or
        $openBounds.Right -gt $copyBounds.Left -or $copyBounds.Right -gt $bounds.Right) {
        throw "The endpoint actions overlap, are clipped, or are unavailable: $endpoint"
    }
}

Write-Output 'Container layout fits the viewport and scrolls vertically.'
