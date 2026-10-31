#!/bin/bash
# Atualiza o UsinaUFVMaravilhaSite já deployado no servidor (não faz setup inicial -
# usuário, systemd, Nginx, SELinux e firewall são configurados uma vez só, ver README do deploy/).
set -e

REPO_DIR=/tmp/UsinaUFVMaravilhaSite
PUBLISH_DIR=/var/www/usinaufvmaravilhasite
SERVICE=usinaufvmaravilhasite

echo "==> Parando o serviço (evita travar arquivos durante o publish)"
sudo systemctl stop "$SERVICE"

echo "==> Atualizando o código"
cd "$REPO_DIR"
git pull

echo "==> Publicando"
dotnet publish UsinaUFVMaravilhaSite/UsinaUFVMaravilhaSite.csproj -c Release -o "$PUBLISH_DIR"

echo "==> Ajustando dono dos arquivos"
sudo chown -R "$SERVICE:$SERVICE" "$PUBLISH_DIR"

echo "==> Subindo o serviço"
sudo systemctl start "$SERVICE"
sudo systemctl status "$SERVICE" --no-pager
