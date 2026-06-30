<#
.SYNOPSIS
Simulates a visitor moving toward a POI on an Android emulator so mobile geofence narration can be tested.

.DESCRIPTION
This script looks up a public POI from the backend API, builds a straight approach path from outside the geofence,
and pushes GPS fixes into the Android emulator with `adb emu geo fix`.

Defaults are aligned with the current mobile tracking thresholds:
- Android foreground service requests updates every 10 seconds / 20 meters
- Shared tracking service accepts samples after 6 seconds or 12 meters

.EXAMPLE
.\scripts\simulate-customer-movement.ps1 -PoiId 6861b6f6d1d8c742aa0a1234

.EXAMPLE
.\scripts\simulate-customer-movement.ps1 -PoiName "Banh mi Co Giang" -DryRun

.EXAMPLE
.\scripts\simulate-customer-movement.ps1 -PoiName "Banh xeo" -EmulatorSerial emulator-5554 -Lang vi
#>
[CmdletBinding(DefaultParameterSetName = 'ByPoiId')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'ByPoiId')]
    [string]$PoiId,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByPoiName')]
    [string]$PoiName,

    [string]$ApiBaseUrl = 'http://localhost:5163',

    [ValidateSet('vi', 'en', 'zh', 'ja', 'ko')]
    [string]$Lang = 'vi',

    [string]$EmulatorSerial,

    [ValidateRange(0, 359)]
    [double]$BearingDegrees = 225,

    [ValidateRange(20, 5000)]
    [int]$ApproachBufferMeters = 120,

    [ValidateRange(50, 5000)]
    [int]$MinimumStartDistanceMeters = 250,

    [ValidateRange(20, 500)]
    [int]$StepMeters = 25,

    [ValidateRange(1, 120)]
    [int]$PauseSeconds = 11,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$InvariantCulture = [System.Globalization.CultureInfo]::InvariantCulture
$EarthRadiusMeters = 6371000.0
$SelectedParameterSet = $PSCmdlet.ParameterSetName

function Write-Section {
    param(
        [string]$Text
    )

    Write-Host "`n=== $Text ===" -ForegroundColor Cyan
}

function Format-Invariant {
    param(
        [double]$Value,
        [string]$Format = '0.######'
    )

    return $Value.ToString($Format, $InvariantCulture)
}

function Invoke-BackendGet {
    param(
        [string]$PathAndQuery
    )

    $uri = '{0}{1}' -f $ApiBaseUrl.TrimEnd('/'), $PathAndQuery
    try {
        return Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec 30
    }
    catch {
        throw "Failed to call backend endpoint '$uri'. Start the API first or pass -ApiBaseUrl. $($_.Exception.Message)"
    }
}

function Get-ApiData {
    param(
        [object]$Response,
        [string]$Context
    )

    if ($null -eq $Response) {
        throw "Backend returned no response for $Context."
    }

    if (-not $Response.success) {
        $message = if ($Response.message) { [string]$Response.message } else { 'Backend reported a failed response.' }
        throw "$Context failed: $message"
    }

    return $Response.data
}

function Resolve-PoiDetail {
    if ($SelectedParameterSet -eq 'ByPoiId') {
        $encodedId = [Uri]::EscapeDataString($PoiId)
        return Get-ApiData -Response (Invoke-BackendGet -PathAndQuery "/api/v1/poi/${encodedId}?lang=${Lang}") -Context "POI detail lookup"
    }

    $encodedKeyword = [Uri]::EscapeDataString($PoiName)
    $candidates = @(Get-ApiData -Response (Invoke-BackendGet -PathAndQuery "/api/v1/poi/search?lang=${Lang}&keyword=${encodedKeyword}") -Context "POI search")
    if ($candidates.Count -eq 0) {
        throw "No public POI matched name '$PoiName'."
    }

    $exactMatches = @($candidates | Where-Object { $_.name -eq $PoiName })
    $selected = $null

    if ($exactMatches.Count -eq 1) {
        $selected = $exactMatches[0]
    }
    elseif ($candidates.Count -eq 1) {
        $selected = $candidates[0]
    }
    else {
        $preview = $candidates |
            Select-Object -First 5 |
            ForEach-Object { "- $($_.name) [$($_.id)]" }
        throw "Multiple POIs matched '$PoiName'. Re-run with -PoiId or a more specific -PoiName.`n$($preview -join [Environment]::NewLine)"
    }

    $encodedId = [Uri]::EscapeDataString([string]$selected.id)
    return Get-ApiData -Response (Invoke-BackendGet -PathAndQuery "/api/v1/poi/${encodedId}?lang=${Lang}") -Context "POI detail lookup"
}

function Resolve-PlaybackHint {
    param(
        [object]$Poi
    )

    $narrationText = [string]$Poi.ttsScript
    if ([string]::IsNullOrWhiteSpace($narrationText)) {
        $narrationText = [string]$Poi.description
    }

    if (-not [string]::IsNullOrWhiteSpace($narrationText)) {
        return 'Narration path will use TTS text.'
    }

    try {
        $encodedId = [Uri]::EscapeDataString([string]$Poi.id)
        $audio = Get-ApiData -Response (Invoke-BackendGet -PathAndQuery "/api/v1/poi/${encodedId}/audio?lang=${Lang}") -Context "POI audio lookup"
        if ($null -ne $audio -and -not [string]::IsNullOrWhiteSpace([string]$audio.audioUrl)) {
            return 'Narration path will use generated/uploaded audio.'
        }
    }
    catch {
        return 'Narration source could not be verified from the audio endpoint.'
    }

    return 'No narration text or audio URL was found. Geofence may trigger without audible playback.'
}

function Resolve-AdbCommand {
    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "adb was not found on PATH. Install Android platform-tools or add adb to PATH."
    }

    return $command.Source
}

function Get-EmulatorSerial {
    param(
        [string]$AdbPath
    )

    if ($DryRun) {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($EmulatorSerial)) {
        return $EmulatorSerial
    }

    & $AdbPath start-server | Out-Null
    $deviceLines = & $AdbPath devices
    if ($LASTEXITCODE -ne 0) {
        throw "adb devices failed."
    }

    $emulators = @(
        $deviceLines |
            Select-Object -Skip 1 |
            Where-Object { $_ -match '^(emulator-\d+)\s+device$' } |
            ForEach-Object { $matches[1] }
    )

    if ($emulators.Count -eq 0) {
        throw "No running Android emulator was found. Start one first or pass -EmulatorSerial."
    }

    if ($emulators.Count -gt 1) {
        $choices = $emulators | ForEach-Object { "- $_" }
        throw "More than one emulator is running. Re-run with -EmulatorSerial.`n$($choices -join [Environment]::NewLine)"
    }

    return $emulators[0]
}

function Convert-DegreesToRadians {
    param(
        [double]$Degrees
    )

    return $Degrees * [Math]::PI / 180.0
}

function Convert-RadiansToDegrees {
    param(
        [double]$Radians
    )

    return $Radians * 180.0 / [Math]::PI
}

function Get-DistanceMeters {
    param(
        [double]$Latitude1,
        [double]$Longitude1,
        [double]$Latitude2,
        [double]$Longitude2
    )

    $lat1 = Convert-DegreesToRadians -Degrees $Latitude1
    $lat2 = Convert-DegreesToRadians -Degrees $Latitude2
    $deltaLat = Convert-DegreesToRadians -Degrees ($Latitude2 - $Latitude1)
    $deltaLon = Convert-DegreesToRadians -Degrees ($Longitude2 - $Longitude1)

    $sinLat = [Math]::Sin($deltaLat / 2.0)
    $sinLon = [Math]::Sin($deltaLon / 2.0)
    $a = ($sinLat * $sinLat) + ([Math]::Cos($lat1) * [Math]::Cos($lat2) * $sinLon * $sinLon)
    $c = 2.0 * [Math]::Atan2([Math]::Sqrt($a), [Math]::Sqrt(1.0 - $a))
    return $EarthRadiusMeters * $c
}

function Get-OffsetCoordinate {
    param(
        [double]$Latitude,
        [double]$Longitude,
        [double]$DistanceMeters,
        [double]$BearingDegrees
    )

    $angularDistance = $DistanceMeters / $EarthRadiusMeters
    $bearingRadians = Convert-DegreesToRadians -Degrees $BearingDegrees
    $lat1 = Convert-DegreesToRadians -Degrees $Latitude
    $lon1 = Convert-DegreesToRadians -Degrees $Longitude

    $sinLat1 = [Math]::Sin($lat1)
    $cosLat1 = [Math]::Cos($lat1)
    $sinAd = [Math]::Sin($angularDistance)
    $cosAd = [Math]::Cos($angularDistance)

    $lat2 = [Math]::Asin(($sinLat1 * $cosAd) + ($cosLat1 * $sinAd * [Math]::Cos($bearingRadians)))
    $lon2 = $lon1 + [Math]::Atan2(
        [Math]::Sin($bearingRadians) * $sinAd * $cosLat1,
        $cosAd - ($sinLat1 * [Math]::Sin($lat2))
    )

    $normalizedLon = (($lon2 + (3.0 * [Math]::PI)) % (2.0 * [Math]::PI)) - [Math]::PI

    return [pscustomobject]@{
        Latitude = Convert-RadiansToDegrees -Radians $lat2
        Longitude = Convert-RadiansToDegrees -Radians $normalizedLon
    }
}

function New-TrackPoints {
    param(
        [double]$TargetLatitude,
        [double]$TargetLongitude,
        [double]$StartLatitude,
        [double]$StartLongitude,
        [double]$GeofenceRadiusMeters
    )

    $startDistance = Get-DistanceMeters -Latitude1 $StartLatitude -Longitude1 $StartLongitude -Latitude2 $TargetLatitude -Longitude2 $TargetLongitude
    $segmentCount = [Math]::Max(1, [int][Math]::Ceiling($startDistance / $StepMeters))
    $points = [System.Collections.Generic.List[object]]::new()

    for ($index = 0; $index -le $segmentCount; $index++) {
        $fraction = $index / $segmentCount
        $latitude = $StartLatitude + (($TargetLatitude - $StartLatitude) * $fraction)
        $longitude = $StartLongitude + (($TargetLongitude - $StartLongitude) * $fraction)
        $distanceToPoi = Get-DistanceMeters -Latitude1 $latitude -Longitude1 $longitude -Latitude2 $TargetLatitude -Longitude2 $TargetLongitude
        $points.Add([pscustomobject]@{
                Step = $index + 1
                Latitude = $latitude
                Longitude = $longitude
                DistanceToPoiMeters = $distanceToPoi
                InsideGeofence = ($distanceToPoi -le $GeofenceRadiusMeters)
            })
    }

    return $points
}

function Send-GeoFix {
    param(
        [string]$AdbPath,
        [string]$Serial,
        [double]$Latitude,
        [double]$Longitude
    )

    & $AdbPath -s $Serial emu geo fix (Format-Invariant -Value $Longitude) (Format-Invariant -Value $Latitude) | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "adb emu geo fix failed for emulator '$Serial'."
    }
}

$poi = Resolve-PoiDetail
if ($null -eq $poi) {
    throw "POI lookup returned no detail object."
}

$poiLatitude = [double]$poi.latitude
$poiLongitude = [double]$poi.longitude
$radiusMeters = if ([double]$poi.geofenceRadiusMeters -gt 0) { [double]$poi.geofenceRadiusMeters } else { 150.0 }
$autoNarrationEnabled = [bool]$poi.autoNarrationEnabled
$startDistanceMeters = [Math]::Max([double]$MinimumStartDistanceMeters, $radiusMeters + [double]$ApproachBufferMeters)
$startPoint = Get-OffsetCoordinate -Latitude $poiLatitude -Longitude $poiLongitude -DistanceMeters $startDistanceMeters -BearingDegrees $BearingDegrees
$trackPoints = New-TrackPoints -TargetLatitude $poiLatitude -TargetLongitude $poiLongitude -StartLatitude $startPoint.Latitude -StartLongitude $startPoint.Longitude -GeofenceRadiusMeters $radiusMeters
$insideCount = @($trackPoints | Where-Object { $_.InsideGeofence }).Count

$adbPath = $null
$serial = $null
if (-not $DryRun) {
    $adbPath = Resolve-AdbCommand
    $serial = Get-EmulatorSerial -AdbPath $adbPath
}
$playbackHint = Resolve-PlaybackHint -Poi $poi

Write-Section "POI"
Write-Host ("Name            : {0}" -f [string]$poi.name)
Write-Host ("Id              : {0}" -f [string]$poi.id)
Write-Host ("Location        : {0}, {1}" -f (Format-Invariant -Value $poiLatitude), (Format-Invariant -Value $poiLongitude))
Write-Host ("Geofence radius : {0} m" -f [Math]::Round($radiusMeters))
Write-Host ("Auto narration  : {0}" -f ($(if ($autoNarrationEnabled) { 'ON' } else { 'OFF' })))
Write-Host ("Playback        : {0}" -f $playbackHint)

if (-not $autoNarrationEnabled) {
    Write-Warning "This POI has autoNarrationEnabled = false. The movement replay can still run, but the mobile geofence flow will not queue narration for this POI."
}

if ($PauseSeconds -lt 10) {
    Write-Warning "PauseSeconds is below the Android location service request interval (10 seconds). Some fixes may be skipped."
}

Write-Section "Plan"
Write-Host ("Approach start  : {0} m away" -f [Math]::Round($startDistanceMeters))
Write-Host ("Bearing         : {0} deg" -f [Math]::Round($BearingDegrees, 1))
Write-Host ("Step size       : {0} m" -f $StepMeters)
Write-Host ("Pause           : {0} s" -f $PauseSeconds)
Write-Host ("Track points    : {0}" -f $trackPoints.Count)
Write-Host ("Inside points   : {0}" -f $insideCount)

$preview = $trackPoints |
    Select-Object Step,
    @{ Name = 'Latitude'; Expression = { Format-Invariant -Value ([double]$_.Latitude) } },
    @{ Name = 'Longitude'; Expression = { Format-Invariant -Value ([double]$_.Longitude) } },
    @{ Name = 'DistanceToPoiM'; Expression = { [Math]::Round([double]$_.DistanceToPoiMeters) } },
    @{ Name = 'Inside'; Expression = { if ($_.InsideGeofence) { 'Y' } else { '' } } }

$preview | Format-Table -AutoSize

if ($DryRun) {
    Write-Section "Dry Run"
    Write-Host "No GPS fixes were sent because -DryRun was used."
    return
}

Write-Section "Replay"
Write-Host ("Emulator serial : {0}" -f $serial)
Write-Host "Sending GPS fixes..."

for ($index = 0; $index -lt $trackPoints.Count; $index++) {
    $point = $trackPoints[$index]
    Send-GeoFix -AdbPath $adbPath -Serial $serial -Latitude ([double]$point.Latitude) -Longitude ([double]$point.Longitude)

    $insideLabel = if ($point.InsideGeofence) { 'inside geofence' } else { 'outside geofence' }
    Write-Host ("[{0}/{1}] {2}, {3} | {4} m | {5}" -f
        ($index + 1),
        $trackPoints.Count,
        (Format-Invariant -Value ([double]$point.Latitude)),
        (Format-Invariant -Value ([double]$point.Longitude)),
        [Math]::Round([double]$point.DistanceToPoiMeters),
        $insideLabel)

    if ($index -lt ($trackPoints.Count - 1)) {
        Start-Sleep -Seconds $PauseSeconds
    }
}

Write-Section "Done"
Write-Host "Replay finished. If the app has Auto Narration enabled and the emulator app is in the foreground, narration should have queued when the route entered the geofence."
