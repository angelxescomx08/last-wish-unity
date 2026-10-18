param([Parameter(Mandatory=$true)][string]$Before, [Parameter(Mandatory=$true)][string]$After)
$ErrorActionPreference = 'Stop'
$baseline = Get-Content -LiteralPath $Before -Raw | ConvertFrom-Json
$current = Get-Content -LiteralPath $After -Raw | ConvertFrom-Json
foreach ($sample in @($baseline, $current)) {
    if (!$sample.gcCounterAvailable -or $sample.frames -lt 300 -or $sample.refreshCalls -lt 30 -or $sample.frameP95Ms -le 0 -or $sample.refreshMedianMs -le 0) { throw 'Incomplete or invalid benchmark' }
}
if ($baseline.graphics -ne $current.graphics -or $baseline.context -ne $current.context -or $baseline.unity -ne $current.unity) { throw 'Compare on the same engine, GPU and launch settings' }
if ($current.refreshMedianMs -ge $baseline.refreshMedianMs) { throw 'Refresh CPU did not improve' }
if ($current.refreshFrameGcBytes -ge $baseline.refreshFrameGcBytes) { throw 'Refresh-frame GC did not improve' }
if ($current.frameP95Ms -gt $baseline.frameP95Ms * 1.25) { throw 'Idle frame p95 regressed by more than 25%; investigate and resample' }
Write-Output ('Refresh CPU median: {0:N4} -> {1:N4} ms; GC/frame: {2:N1} -> {3:N1} bytes' -f $baseline.refreshMedianMs, $current.refreshMedianMs, $baseline.refreshFrameGcBytes, $current.refreshFrameGcBytes)
Write-Output 'Measured refresh improvements passed; capped idle FPS is reported separately.'
