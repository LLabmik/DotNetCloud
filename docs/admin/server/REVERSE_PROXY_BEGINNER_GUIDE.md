# Reverse Proxy Beginner Guide

This guide is for the beginner setup path where DotNetCloud is installed for a public domain name behind a reverse proxy.

If you chose the beginner option for a public domain behind a reverse proxy, DotNetCloud itself should usually stay on local HTTP:

- DotNetCloud internal URL: `http://localhost:5080`
- Public URL: `https://your-domain.example.com`

The reverse proxy sits in front of DotNetCloud and handles public HTTPS.

## Why Use A Reverse Proxy?

For a public installation, a reverse proxy is usually the easiest long-term setup because it handles:

- TLS certificates and HTTPS on ports `80` and `443`
- HTTP to HTTPS redirects
- Cleaner public URLs
- WebSocket forwarding for SignalR
- Running multiple services on the same machine later
- Easier certificate renewal with tools like Certbot or built-in Caddy automation

DotNetCloud can serve HTTPS directly, but that usually means more manual certificate handling and less flexibility later.

## Recommended Beginner Choice

If you are not already experienced with reverse proxies, use:

- **Apache** if you prefer a traditional, explicit config you can read and control
- **Caddy** if you want fewer configuration lines and automatic HTTPS

This guide includes both. Apache is shown first because it is the more explicit beginner reference.

## Before You Start

Make sure all of these are true:

1. You have already installed DotNetCloud.
2. DotNetCloud is running locally on the server.
3. Your domain name points to your server's public IP address.
4. Ports `80` and `443` are open in your firewall/router.
5. DotNetCloud is reachable locally:

```bash
curl -fsS http://localhost:5080/health/live
```

If that local health check fails, fix DotNetCloud first before setting up Apache or Caddy.

## What The Reverse Proxy Does

The reverse proxy accepts traffic from the internet and forwards it to DotNetCloud on the local machine.

The request flow looks like this:

```text
Browser -> https://cloud.example.com -> Apache/Caddy -> http://localhost:5080 -> DotNetCloud
```

DotNetCloud stays private on `localhost`, while Apache or Caddy handles the public-facing HTTPS endpoint.

## Apache Setup

### 1. Install Apache

```bash
sudo apt update
sudo apt install apache2
```

### 2. Enable The Required Apache Modules

```bash
sudo a2enmod proxy proxy_http proxy_wstunnel ssl rewrite headers
```

### 3. Create The Apache Site Configuration

Replace `cloud.example.com` with your real domain.

Create this file:

`/etc/apache2/sites-available/dotnetcloud.conf`

```apache
<VirtualHost *:80>
    ServerName cloud.example.com

    RewriteEngine On
    RewriteRule ^(.*)$ https://%{HTTP_HOST}$1 [R=301,L]
</VirtualHost>

<VirtualHost *:443>
    ServerName cloud.example.com

    SSLEngine on
    SSLCertificateFile /etc/letsencrypt/live/cloud.example.com/fullchain.pem
    SSLCertificateKeyFile /etc/letsencrypt/live/cloud.example.com/privkey.pem
    SSLProtocol all -SSLv3 -TLSv1 -TLSv1.1

    ProxyPreserveHost On
    ProxyPass / http://127.0.0.1:5080/
    ProxyPassReverse / http://127.0.0.1:5080/

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

### 4. Enable The Site

```bash
sudo a2ensite dotnetcloud
sudo apache2ctl configtest
sudo systemctl reload apache2
```

### 5. Get A TLS Certificate With Certbot

```bash
sudo apt install certbot python3-certbot-apache
sudo certbot --apache -d cloud.example.com
```

Certbot will usually:

- request the certificate
- update the Apache site config
- install redirects if needed
- configure renewal

### 6. Verify Apache + DotNetCloud

Run:

```bash
curl -I https://cloud.example.com
curl -I https://cloud.example.com/health/live
```

Then open the site in your browser:

```text
https://cloud.example.com
```

## Caddy Setup

Caddy is simpler if you want automatic HTTPS with less manual certificate work.

### 1. Install Caddy

Use the official Caddy install steps for your distro, or on Debian/Ubuntu:

```bash
sudo apt update
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install caddy
```

### 2. Create The Caddy Configuration

Edit:

`/etc/caddy/Caddyfile`

```caddy
cloud.example.com {
    reverse_proxy 127.0.0.1:5080
}
```

### 3. Reload Caddy

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy will attempt to obtain and renew HTTPS certificates automatically as long as:

- your domain points to the server
- ports `80` and `443` are reachable from the internet

### 4. Verify Caddy + DotNetCloud

```bash
curl -I https://cloud.example.com
curl -I https://cloud.example.com/health/live
```

Then open:

```text
https://cloud.example.com
```

## Which Should You Choose?

Choose **Apache** if:

- you want explicit configuration files
- you like seeing each proxy and header rule directly
- you are already familiar with Apache-style site configs

Choose **Caddy** if:

- you want the fewest steps
- you want automatic HTTPS with minimal config
- you are okay with a more opinionated reverse proxy

## Common Beginner Questions

### Why not expose DotNetCloud directly on the internet?

You can. But a reverse proxy usually makes public hosting easier because it centralizes TLS and lets DotNetCloud stay on a local-only port.

### Why is DotNetCloud still on `http://localhost:5080`?

Because in reverse-proxy mode, Apache or Caddy handles public HTTPS and forwards requests to DotNetCloud locally.

### Do I still get real-time chat and WebSockets?

Yes. The Apache and Caddy examples above support DotNetCloud's WebSocket usage.

### What if I want to host more apps later?

That is another reason to use a reverse proxy. Apache or Caddy can route different domains or subpaths to different local services.

## Troubleshooting

### DotNetCloud Works Locally But The Public Domain Does Not

Check:

1. DNS points to the correct public IP.
2. Ports `80` and `443` are open.
3. Apache or Caddy is running.
4. DotNetCloud is healthy locally:

```bash
curl -fsS http://localhost:5080/health/live
```

### Apache Config Errors

```bash
sudo apache2ctl configtest
sudo journalctl -u apache2 -n 50 --no-pager
```

### Caddy Errors

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo journalctl -u caddy -n 50 --no-pager
```

### DotNetCloud Service Errors

```bash
sudo systemctl status dotnetcloud
sudo journalctl -u dotnetcloud -n 50 --no-pager
```

## After Reverse Proxy Setup

Once Apache or Caddy is working, your beginner public-domain setup is complete.

Use your public URL in the browser:

```text
https://cloud.example.com
```

If you later want to review your DotNetCloud choices again, run:

```bash
sudo dotnetcloud setup --beginner
```

## Related Docs

- Main server install guide: [INSTALLATION.md](INSTALLATION.md)
- General reverse proxy reference: [../../development/REVERSE_PROXY.md](../../development/REVERSE_PROXY.md)
- Server configuration reference: [CONFIGURATION.md](CONFIGURATION.md)