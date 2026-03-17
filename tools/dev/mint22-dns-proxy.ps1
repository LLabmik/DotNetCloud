# Minimal DNS proxy for Android emulator development
# - Resolves 'mint22' -> 192.168.0.112
# - Forwards all other queries to 8.8.8.8
# Usage: Run as Administrator, then start emulator with -dns-server 10.0.2.2

param(
    [string]$HostName = "mint22",
    [string]$HostIp   = "192.168.0.112",
    [string]$Upstream  = "8.8.8.8",
    [int]$UpstreamPort = 53,
    [int]$ListenPort   = 53
)

function Get-DnsQueryName([byte[]]$data, [int]$start) {
    $offset = $start
    $labels = [System.Collections.Generic.List[string]]::new()
    while ($offset -lt $data.Length -and $data[$offset] -ne 0) {
        $len = $data[$offset]
        if (($len -band 0xC0) -eq 0xC0) { break }   # compression pointer - skip
        $offset++
        $labels.Add([System.Text.Encoding]::ASCII.GetString($data, $offset, $len))
        $offset += $len
    }
    return ($labels -join ".").ToLower()
}

function New-ARecordResponse([byte[]]$query, [byte[]]$ip) {
    # Allocate response = full query copy + 16-byte answer RR
    $resp = [byte[]]::new($query.Length + 16)
    [Array]::Copy($query, $resp, $query.Length)

    # Flags: QR=1 (response), RD=1, RA=1, RCODE=0
    $resp[2] = 0x81; $resp[3] = 0x80
    # ANCOUNT = 1, NSCOUNT = 0, ARCOUNT = 0
    $resp[6] = 0x00; $resp[7] = 0x01
    $resp[8] = 0x00; $resp[9] = 0x00
    $resp[10] = 0x00; $resp[11] = 0x00

    # Answer RR (appended after question section)
    $o = $query.Length
    $resp[$o+0]  = 0xC0; $resp[$o+1]  = 0x0C  # Name: ptr -> offset 12 (question name)
    $resp[$o+2]  = 0x00; $resp[$o+3]  = 0x01  # Type: A
    $resp[$o+4]  = 0x00; $resp[$o+5]  = 0x01  # Class: IN
    $resp[$o+6]  = 0x00; $resp[$o+7]  = 0x00  # TTL high
    $resp[$o+8]  = 0x00; $resp[$o+9]  = 0x3C  # TTL = 60 s
    $resp[$o+10] = 0x00; $resp[$o+11] = 0x04  # RDLENGTH = 4
    $resp[$o+12] = $ip[0]; $resp[$o+13] = $ip[1]
    $resp[$o+14] = $ip[2]; $resp[$o+15] = $ip[3]
    return $resp
}

$ipBytes = [System.Net.IPAddress]::Parse($HostIp).GetAddressBytes()
$upstreamEndpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Parse($Upstream), $UpstreamPort)

Write-Host "DNS proxy starting on 0.0.0.0:$ListenPort"
Write-Host "  $HostName -> $HostIp"
Write-Host "  All other queries forwarded to $Upstream"
Write-Host "  Start emulator with: -dns-server 10.0.2.2"
Write-Host ""

$udp = New-Object System.Net.Sockets.UdpClient($ListenPort)
$clientEp = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)

try {
    while ($true) {
        $data = $udp.Receive([ref]$clientEp)
        $name = Get-DnsQueryName $data 12

        if ($name -eq $HostName.ToLower()) {
            $resp = New-ARecordResponse $data $ipBytes
            $udp.Send($resp, $resp.Length, $clientEp) | Out-Null
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $name -> $HostIp (local)"
        }
        else {
            $fwd = New-Object System.Net.Sockets.UdpClient
            $fwd.Client.ReceiveTimeout = 3000
            $fwd.Send($data, $data.Length, $upstreamEndpoint) | Out-Null
            try {
                $reply = $fwd.Receive([ref]$upstreamEndpoint)
                $udp.Send($reply, $reply.Length, $clientEp) | Out-Null
                Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $name -> forwarded to $Upstream"
            }
            catch {
                Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $name -> upstream timeout"
            }
            finally { $fwd.Close() }
        }
    }
}
finally {
    $udp.Close()
    Write-Host "DNS proxy stopped."
}
