$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5080'
function Post($path, $body, $token) {
  $headers = @{ 'Content-Type' = 'application/json' }
  if ($token) { $headers.Authorization = "Bearer $token" }
  try {
    $r = Invoke-WebRequest -Uri "$base$path" -Method POST -Headers $headers -Body ($body | ConvertTo-Json -Depth 6) -UseBasicParsing
    return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) }
  } catch {
    $resp = $_.Exception.Response
    $code = 0
    $text = $_.Exception.Message
    if ($resp) {
      $code = [int]$resp.StatusCode
      $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
      $text = $reader.ReadToEnd()
    }
    return @{ status = $code; body = $text }
  }
}
function GetJson($path, $token) {
  $headers = @{}
  if ($token) { $headers.Authorization = "Bearer $token" }
  try {
    $r = Invoke-WebRequest -Uri "$base$path" -Headers $headers -UseBasicParsing
    return @{ status = [int]$r.StatusCode; body = ($r.Content | ConvertFrom-Json) }
  } catch {
    $resp = $_.Exception.Response
    $code = if ($resp) { [int]$resp.StatusCode } else { 0 }
    return @{ status = $code; raw = $_.Exception.Message }
  }
}
function First($value) {
  if ($null -eq $value) { return $null }
  return @($value)[0]
}

$dev = Post '/auth/register' @{ email='developer@binding.local'; password='password123'; displayName='Dev'; inviteCode=$null } $null
if ($dev.status -ne 200) { throw "register dev $($dev.status) $($dev.body)" }
$dt = $dev.body.token
$me = GetJson '/auth/me' $dt
Write-Output "DEV $($me.body.isDeveloper)"
Post '/skills' @{ name='Coding'; meaning='Making small things' } $dt | Out-Null
$sid = (First (GetJson '/skills' $dt).body).id
Post "/skills/$sid/challenges" @{ name='Ship a page'; detail='A page you use'; required=$true } $dt | Out-Null
Post '/habits' @{ identity='I am someone who sits down to write.'; cue='After coffee'; cueTime='07:30'; twoMinute='Open the file.'; fullVersion='Write one function.'; weekdays=127; keystone=$true } $dt | Out-Null
$st = (GetJson '/status' $dt).body
Write-Output "OPEN state=$($st.bindingState) day=$($st.dayOfBinding) fame=$($st.sustenance) title=$($st.title) fasten=$($st.fasteningOpen)"
Post "/habits/$($st.keystone.id)/show-up" @{} $dt | Out-Null
$st = (GetJson '/status' $dt).body
Write-Output "FED state=$($st.bindingState) fame=$($st.sustenance) leave=$($st.leaveLine)"
$ch = (First (GetJson "/skills/$sid" $dt).body.challenges).id
Post "/challenges/$ch/pass" @{ note='The page exists.' } $dt | Out-Null
$sk = (GetJson "/skills/$sid" $dt).body
$st = (GetJson '/status' $dt).body
Write-Output "RANK $($sk.rank) fame=$($st.sustenance) label=$($st.faculties[0].label)"
Post '/parties' @{ name='Near' } $dt | Out-Null
$inv = Post '/invites' @{} $dt
Write-Output "CODE $($inv.body.code)"
$hostReg = Post '/auth/register' @{ email='host@binding.local'; password='password123'; displayName='Host'; inviteCode=$inv.body.code } $null
if ($hostReg.status -ne 200) { throw "host $($hostReg.status) $($hostReg.body)" }
$ht = $hostReg.body.token
Post '/skills' @{ name='Running'; meaning='Moving' } $ht | Out-Null
$hs = (First (GetJson '/skills' $ht).body).id
Post "/skills/$hs/challenges" @{ name='One lap'; detail='Around the block'; required=$true } $ht | Out-Null
Post '/habits' @{ identity='I am someone who runs.'; cue='After waking'; cueTime='06:15'; twoMinute='Put on shoes.'; fullVersion='Run.'; weekdays=127; keystone=$true } $ht | Out-Null
$acc = Post "/invites/$($inv.body.code)/accept" @{} $ht
Write-Output "ACCEPT $($acc.status) $($acc.body | ConvertTo-Json -Compress)"
$mine = GetJson '/parties/mine' $ht
Post '/parties/quests' @{ name='Shared clear'; steps=@('Sit together') } $dt | Out-Null
$mine = GetJson '/parties/mine' $ht
$step = (First $mine.body.quest.milestones).id
Post "/milestones/$step/complete" @{} $ht | Out-Null
$guild = Post '/guilds' @{ name='The House'; creed='Eat together' } $dt
$gid = $guild.body.id
$gcode = Post "/guilds/$gid/invites" @{} $dt
$gacc = Post "/invites/$($gcode.body.code)/accept" @{} $ht
Write-Output "GUILD ACCEPT $($gacc.status) $($gacc.body)"
$house = GetJson '/guilds/mine' $dt
Write-Output "HOUSE $($house.body.line) members=$(@( $house.body.members ).Count)"
Post '/guilds/quests' @{ guildId=$gid; name='House work'; steps=@('One step') } $dt | Out-Null
$gq = GetJson '/guilds/mine' $dt
$gs = (First $gq.body.quest.milestones).id
Post "/milestones/$gs/complete" @{} $dt | Out-Null
$card = (GetJson "/players/$($me.body.id)" $ht).body
Write-Output "CARD house=$($card.house) gathering=$($card.gathering) stateProp=$($card.PSObject.Properties.Name -contains 'bindingState')"
$hostId = (GetJson '/auth/me' $ht).body.id
Post '/duels' @{ opponentId=$hostId; terms='Write one line'; deadline='2026-10-30' } $dt | Out-Null
$duel = First ((GetJson '/duels' $ht).body | Where-Object { $_.status -eq 'Pending' })
Post "/duels/$($duel.id)/accept" @{} $ht | Out-Null
Post "/duels/$($duel.id)/clear" @{ note='I wrote it.' } $dt | Out-Null
Post "/duels/$($duel.id)/clear" @{ note='I wrote mine.' } $ht | Out-Null
$done = First (GetJson '/duels' $dt).body
Write-Output "DUEL $($done.status) winner=$($done.winnerId)"
$admin = Post "/admin/hosts/$hostId/sustenance" @{ amount=7; reason='A correction from the keeper of the machine.' } $dt
Write-Output "ADMIN $($admin.status)"
$log = First (GetJson '/log' $ht).body
Write-Output "HOSTLOG $($log.body)"
Write-Output "HOSTADMIN $((GetJson '/admin' $ht).status)"
Write-Output "AUDIT $((First (GetJson '/admin/audit' $dt).body).reason)"
Write-Output 'OK'
