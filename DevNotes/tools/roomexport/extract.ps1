Add-Type -AssemblyName System.IO.Compression.FileSystem
$src = "D:\Dreamport\EXTERNAL FILES FOR REFERNCE\Rec\Rooms"
$dst = "C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\1c0947a6-bfa3-41e9-ae24-3394d8a31490\scratchpad\ex"
$want = @('Subroom.json','persisted_room_data.binpb','PrefabIds.csv','descriptor_set.binpb','RoomDetails.json','super_room_data.binpb','CV2NodeTypes.csv','Warnings.log')
$bs = [char]92
$total = 0
foreach ($z in Get-ChildItem $src -Filter *.zip) {
  $parts = $z.BaseName.Split([char]95); $name = ($parts[2..($parts.Length-3)] -join [char]95); if ($name.Length -gt 32) { $name = $name.Substring(0,32) }
  $zip = [System.IO.Compression.ZipFile]::OpenRead($z.FullName)
  try {
    foreach ($e in $zip.Entries) {
      $p = $e.FullName.Replace($bs, '/')
      $leaf = ($p.Split('/'))[-1]
      if (($want -contains $leaf) -and -not $p.StartsWith('Inventions/')) {
        $out = Join-Path (Join-Path $dst $name) ($p.Replace('/', $bs))
        $dir = Split-Path $out
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $out, $true)
        $total++
      }
    }
  } finally { $zip.Dispose() }
}
"extracted $total files"

