function Normalize-PeTimestamps([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x40) { throw "PE image is too short: $Path" }

    $peOffset = [BitConverter]::ToUInt32($bytes, 0x3C)
    if ($peOffset + 24 -gt $bytes.Length -or
            $bytes[[int]$peOffset] -ne 0x50 -or
            $bytes[[int]$peOffset + 1] -ne 0x45 -or
            $bytes[[int]$peOffset + 2] -ne 0 -or
            $bytes[[int]$peOffset + 3] -ne 0) {
        throw "PE signature/header is invalid: $Path"
    }

    # The COFF header timestamp and timestamps embedded in IMAGE_DEBUG_DIRECTORY
    # are linker metadata. The loader authenticates the exact normalized image
    # bytes; these fields do not change executable semantics.
    ([BitConverter]::GetBytes([uint32]0)).CopyTo($bytes, [int]($peOffset + 8))

    $sectionCount = [BitConverter]::ToUInt16($bytes, [int]($peOffset + 6))
    $optionalSize = [BitConverter]::ToUInt16($bytes, [int]($peOffset + 20))
    $optionalOffset = [int]$peOffset + 24
    $sectionTable = $optionalOffset + $optionalSize
    if ($optionalOffset + $optionalSize -gt $bytes.Length -or
            $sectionTable + ($sectionCount * 40) -gt $bytes.Length) {
        throw "PE optional header or section table is truncated: $Path"
    }
    if ([BitConverter]::ToUInt16($bytes, $optionalOffset) -ne 0x20B -or
            $optionalSize -lt (112 + (7 * 8))) {
        throw "PE32+ debug data-directory entry is missing: $Path"
    }

    $debugDirectory = $optionalOffset + 112 + (6 * 8)
    $debugRva = [BitConverter]::ToUInt32($bytes, $debugDirectory)
    $debugSize = [BitConverter]::ToUInt32($bytes, $debugDirectory + 4)
    if ($debugRva -eq 0 -and $debugSize -eq 0) {
        [IO.File]::WriteAllBytes($Path, $bytes)
        return
    }
    if ($debugRva -eq 0 -or $debugSize -lt 28 -or ($debugSize % 28) -ne 0) {
        throw "PE debug directory is malformed: $Path"
    }

    $debugRaw = -1L
    for ($index = 0; $index -lt $sectionCount; $index++) {
        $sectionOffset = $sectionTable + ($index * 40)
        $virtualSize = [BitConverter]::ToUInt32($bytes, $sectionOffset + 8)
        $virtualAddress = [BitConverter]::ToUInt32($bytes, $sectionOffset + 12)
        $rawSize = [BitConverter]::ToUInt32($bytes, $sectionOffset + 16)
        $rawPointer = [BitConverter]::ToUInt32($bytes, $sectionOffset + 20)
        $mappedSize = [Math]::Max($virtualSize, $rawSize)
        if ($debugRva -ge $virtualAddress -and
                $debugRva -lt ($virtualAddress + $mappedSize)) {
            $debugRaw = [long]$rawPointer + ($debugRva - $virtualAddress)
            break
        }
    }
    if ($debugRaw -lt 0 -or $debugRaw + $debugSize -gt $bytes.Length) {
        throw "PE debug directory is not section-backed: $Path"
    }

    $entryCount = [int]($debugSize / 28)
    for ($index = 0; $index -lt $entryCount; $index++) {
        $timestampOffset = [int]($debugRaw + ($index * 28) + 4)
        ([BitConverter]::GetBytes([uint32]0)).CopyTo($bytes, $timestampOffset)
    }
    [IO.File]::WriteAllBytes($Path, $bytes)
}
