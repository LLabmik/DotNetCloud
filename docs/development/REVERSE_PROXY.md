# Reverse Proxy Configuration Guide

This guide explains how to configure popular web servers as reverse proxies for DotNetCloud.

## Overview

DotNetCloud runs as a Kestrel web server and is designed to be deployed behind a reverse proxy in production. The reverse proxy handles:

- **TLS termination** — SSL/TLS certificates and HTTPS
- **Static file serving** — Offload static assets
- **Load balancing** — Distribute traffic across multiple instances
- **Request buffering** — Protect against slow clients
- **WebSocket proxying** — Required for SignalR real-time features

## Prerequisites

- DotNetCloud server running on the configured HTTP port (default: `5080`)
- A domain name pointing to your server
- TLS certificate (Let's Encrypt recommended for production)

## Forwarded Headers

DotNetCloud is pre-configured to accept `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` headers. These are automatically processed via `UseForwardedHeaders()` in the middleware pipeline.

---

## nginx

### Installation

```bash
# Ubuntu/Debian
sudo apt install nginx

# CentOS/RHEL
sudo yum install nginx
```

### Configuration

Create `/etc/nginx/sites-available/dotnetcloud`:

```nginx
upstream dotnetcloud {
    server 127.0.0.1:5080;
}

server {
    listen 80;
    listen [::]:80;
    server_name dotnetcloud.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name dotnetcloud.example.com;

    ssl_certificate /etc/letsencrypt/live/dotnetcloud.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/dotnetcloud.example.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;

    # Proxy settings
    location / {
        proxy_pass http://dotnetcloud;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }

    # WebSocket support for SignalR
    location /hubs/ {
        proxy_pass http://dotnetcloud;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }

    client_max_body_size 50m;

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml;
    gzip_min_length 1000;
}

map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}
```

Enable and reload:

```bash
sudo ln -s /etc/nginx/sites-available/dotnetcloud /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Apache

### Installation

```bash
# Ubuntu/Debian
sudo apt install apache2
sudo a2enmod proxy proxy_http proxy_wstunnel ssl rewrite headers
```

### Configuration

Create `/etc/apache2/sites-available/dotnetcloud.conf`:

```apache
<VirtualHost *:80>
    ServerName dotnetcloud.example.com
    RewriteEngine On
    RewriteRule ^(.*)$ https://%{HTTP_HOST}$1 [R=301,L]
</VirtualHost>

<VirtualHost *:443>
    ServerName dotnetcloud.example.com

    SSLEngine on
    SSLCertificateFile /etc/letsencrypt/live/dotnetcloud.example.com/fullchain.pem
    SSLCertificateKeyFile /etc/letsencrypt/live/dotnetcloud.example.com/privkey.pem
    SSLProtocol all -SSLv3 -TLSv1 -TLSv1.1

    ProxyPreserveHost On
    ProxyPass / http://127.0.0.1:5080/
    ProxyPassReverse / http://127.0.0.1:5080/

    # WebSocket support for SignalR
    RewriteEngine On
    RewriteCond %{HTTP:Upgrade} websocket [NC]
    RewriteCond %{HTTP:Connection} upgrade [NC]
    RewriteRule ^/?(.*) ws://127.0.0.1:5080/$1 [P,L]

    RequestHeader set X-Forwarded-Proto "https"
    RequestHeader set X-Forwarded-Host %{HTTP_HOST}s
    RequestHeader set X-Forwarded-Port %{SERVER_PORT}s

    ProxyTimeout 60
    Timeout 60
</VirtualHost>
```

Enable and reload:

```bash
sudo a2ensite dotnetcloud
sudo apache2ctl configtest
sudo systemctl reload apache2
```

---

## IIS (Windows)

### Prerequisites

- IIS with ASP.NET Core Hosting Bundle installed
- URL Rewrite module

### Configuration

Place `web.config` in the application root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\DotNetCloud.Core.Server.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="InProcess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="ASPNETCORE_FORWARDEDHEADERS_ENABLED" value="true" />
        </environmentVariables>
      </aspNetCore>
      <security>
        <requestFiltering>
          <requestLimits maxAllowedContentLength="52428800" />
        </requestFiltering>
      </security>
    </system.webServer>
  </location>
</configuration>
```

### WebSocket Support

Ensure the WebSocket Protocol feature is enabled in IIS:

1. Open Server Manager → Add Roles and Features
2. Navigate to Web Server → Application Development
3. Check "WebSocket Protocol"

---

## Configuration Validation

DotNetCloud includes a built-in configuration validator for reverse proxy setups. You can validate your configuration programmatically:

```csharp
using DotNetCloud.Core.Server.Configuration;

// Validate nginx config
var nginxConfig = File.ReadAllText("/etc/nginx/sites-available/dotnetcloud");
var result = ReverseProxyTemplates.ValidateConfiguration("nginx", nginxConfig);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"ERROR: {error}");
}

foreach (var warning in result.Warnings)
    Console.WriteLine($"WARNING: {warning}");
```

---

## Troubleshooting

### Common Issues

1. **502 Bad Gateway**: The DotNetCloud server is not running or is not listening on the expected port.
2. **WebSocket connection fails**: Ensure WebSocket upgrade headers are being forwarded.
3. **HTTPS redirect loop**: Make sure `X-Forwarded-Proto` is being set correctly.
4. **Large file upload fails**: Check both the reverse proxy and Kestrel `MaxRequestBodySize` limits.
5. **CORS errors**: Ensure the `Cors:AllowedOrigins` configuration includes your frontend domain.

### Verifying Forwarded Headers

Send a request and check the response headers:

```bash
curl -v https://dotnetcloud.example.com/health
```

The response should include proper `X-Forwarded-*` headers being processed.
