# Certificado HTTPS do gateway

Coloque aqui `cert.pem` (certificado + cadeia) e `key.pem` (chave privada).
Com os dois arquivos, o gateway sobe em HTTPS e redireciona o HTTP; sem eles, fica só em HTTP (ambiente local).

Produção: use um certificado válido para o domínio (ex.: Let's Encrypt via certbot ou o do seu provedor/CDN).
Teste local (o navegador vai avisar que não é confiável):

    openssl req -x509 -newkey rsa:2048 -nodes -days 365 -subj "/CN=localhost" \
      -keyout docker/ssl/key.pem -out docker/ssl/cert.pem

Os arquivos `.pem` não vão para o git.
