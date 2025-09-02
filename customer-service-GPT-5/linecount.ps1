$ErrorActionPreference = 'Stop'
$root = Get-Location
$excludePattern = '\\bin\\|\\obj\\|\\\.git\\|\\\.vs\\|node_modules'
$files = Get-ChildItem -Recurse -File -Include *.cs,*.bicep,*.json,*.yml,*.yaml,*.md | Where-Object { $_.FullName -notmatch $excludePattern }
$stats = foreach($f in $files){
  try {
    $count = [System.IO.File]::ReadLines($f.FullName).Count
    [pscustomobject]@{ File = $f.FullName.Substring($root.Path.Length+1); Ext = $f.Extension.ToLower(); Lines = $count }
  } catch {}
}
$byExt = $stats | Group-Object Ext | ForEach-Object { $sum = ($_.Group | Select-Object -Expand Lines | Measure-Object -Sum).Sum; [pscustomobject]@{ Extension=$_.Name; Files=$_.Count; Lines= $sum } } | Sort-Object Lines -Descending
$total = ($stats | Select-Object -Expand Lines | Measure-Object -Sum).Sum
"TOTAL_LINES=$total"
"Breakdown:" 
$byExt | Format-Table -AutoSize
'--- Top 10 largest files ---'
$stats | Sort-Object Lines -Descending | Select-Object -First 10 | Format-Table -AutoSize
'--- End ---'
