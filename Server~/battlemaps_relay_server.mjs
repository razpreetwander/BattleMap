// Zero-dependency Node.js WebSocket Relay Server for BattleMaps_Unity LAN / Online Multiplayer
// Run with: node battlemaps_relay_server.mjs
import http from "node:http";
import crypto from "node:crypto";

const PORT = Number(process.env.PORT || 8787);
const clients = new Set();

function encodeWsFrame(text) {
  const payload = Buffer.from(text, "utf8");
  const len = payload.length;
  let header;
  if (len < 126) {
    header = Buffer.from([0x81, len]);
  } else {
    header = Buffer.alloc(4);
    header[0] = 0x81;
    header[1] = 126;
    header.writeUInt16BE(len, 2);
  }
  return Buffer.concat([header, payload]);
}

function decodeWsFrame(buf) {
  if (buf.length < 2) return null;
  const masked = (buf[1] & 0x80) !== 0;
  let len = buf[1] & 0x7f;
  let offset = 2;
  if (len === 126) {
    len = buf.readUInt16BE(2);
    offset = 4;
  }
  if (!masked || buf.length < offset + 4 + len) return null;
  const mask = buf.subarray(offset, offset + 4);
  const data = Buffer.alloc(len);
  for (let i = 0; i < len; i++) data[i] = buf[offset + 4 + i] ^ mask[i % 4];
  return data.toString("utf8");
}

const server = http.createServer((req, res) => {
  res.writeHead(200, { "Content-Type": "text/plain" });
  res.end("BattleMaps Relay Server Running\n");
});

server.on("upgrade", (req, socket) => {
  const key = req.headers["sec-websocket-key"];
  if (!key) return socket.destroy();
  const accept = crypto
    .createHash("sha1")
    .update(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
    .digest("base64");
  socket.write(
    "HTTP/1.1 101 Switching Protocols\r\n" +
    "Upgrade: websocket\r\n" +
    "Connection: Upgrade\r\n" +
    `Sec-WebSocket-Accept: ${accept}\r\n\r\n`
  );
  clients.add(socket);

  socket.on("data", chunk => {
    const text = decodeWsFrame(chunk);
    if (!text) return;
    const frame = encodeWsFrame(text);
    for (const peer of clients) {
      if (peer !== socket && !peer.destroyed) peer.write(frame);
    }
  });

  socket.on("close", () => clients.delete(socket));
  socket.on("error", () => clients.delete(socket));
});

server.listen(PORT, () => {
  console.log(`[BattleMaps Relay] WebSocket server listening on ws://127.0.0.1:${PORT}`);
});
