# Trecho comum aos modos HTTP e HTTPS (incluído no bloco http do Nginx)

# Não revela a versão do Nginx em cabeçalhos e páginas de erro
server_tokens off;

client_max_body_size 20M;

gzip on;
gzip_vary on;
gzip_proxied any;
gzip_comp_level 6;
gzip_types text/plain text/css text/xml text/javascript
           application/json application/javascript application/xml+rss;

# Limite geral de requisições por IP para a API (o login tem limite próprio, mais rígido, na API)
limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;

upstream pklots_api {
    zone pklots_api 64k;
    server pklots-backend:8080 resolve;
}
