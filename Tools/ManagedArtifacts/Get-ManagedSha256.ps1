function Get-ManagedSha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $resolvedPath = [IO.Path]::GetFullPath($LiteralPath)
    $stream = [IO.File]::OpenRead($resolvedPath)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha256.ComputeHash($stream)
        [pscustomobject]@{
            Algorithm = 'SHA256'
            Hash = [BitConverter]::ToString($digest).Replace('-', '')
            Path = $resolvedPath
        }
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}
