[![](../../workflows/gh-pages/badge.svg)](../../actions)


This project contains the official wallets for the Rival Coins ecosystem.

Start Local Mock rivalcoins.money
docker run -it --rm -p 8080:8080 -v /home/jerome/MockRivalCoinsSite:/public danjellz/http-server
docker run -it -d `
  -e CORS_REVERSE_PROXY_TARGET_URL=http://172.241.0.1:8080 `
  -e CORS_REVERSE_PROXY_HOST=0.0.0.0 `
  -p 80:8081 `
  --name mock-rivalcoins-website-cors-proxy `
  kaishuu0123/cors-reverse-proxy

Start Local Stellar + Horizon + Friendbot
docker run --rm -it -p "8000:8000" --name stellar stellar/quickstart --standalone

Rival Coins Server
docker run -it -d `
  -e CORS_REVERSE_PROXY_TARGET_URL=http://localhost:5123 `
  -e CORS_REVERSE_PROXY_HOST=0.0.0.0 `
  -p 6123:8081 `
  --name wallet-server-cors-proxy `
  kaishuu0123/cors-reverse-proxy


Horizon => http://localhost:8000
Friendbot => http://localhost:8000/friendbot?addr={YOUR ACCOUNT ID}