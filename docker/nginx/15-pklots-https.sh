#!/bin/sh
# Escolhe o modo do gateway antes do envsubst (20-envsubst-on-templates.sh):
# com certificado em /etc/nginx/ssl → HTTPS (HTTP redireciona); sem certificado → só HTTP.
set -e
mkdir -p /etc/nginx/templates
if [ -f /etc/nginx/ssl/cert.pem ] && [ -f /etc/nginx/ssl/key.pem ]; then
    cp /etc/nginx/modelos/https.conf.template /etc/nginx/templates/default.conf.template
    echo "pklots: certificado encontrado, gateway em HTTPS"
else
    cp /etc/nginx/modelos/http.conf.template /etc/nginx/templates/default.conf.template
    echo "pklots: sem certificado em /etc/nginx/ssl, gateway em HTTP (apenas para ambiente local)"
fi
