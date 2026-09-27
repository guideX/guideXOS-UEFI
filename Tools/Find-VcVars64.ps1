function Find-VcVars64 {
    param([string]$MsbuildPath)

    $candidateVcVars = @()
    if ($MsbuildPath -and $MsbuildPath -ne 'msbuild') {
        $msbuildExe = Get-Item $MsbuildPath
        $vsInstallDir = Split-Path (Split-Path (Split-Path (Split-Path $msbuildExe.FullName -Parent) -Parent) -Parent) -Parent
        $candidateVcVars += (Join-Path $vsInstallDir 'VC\Auxiliary\Build\vcvars64.bat')
    }
    $candidateVcVars += @(
        'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\18\Professional\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat'
    )
    foreach ($path in $candidateVcVars) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }
    return $null
}
