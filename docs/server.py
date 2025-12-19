#!/usr/bin/env python3
"""
Simple HTTP server with Brotli and Gzip support for Unity WebGL builds.
"""

import http.server
import os

class BrotliHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        # Add CORS headers
        self.send_header('Access-Control-Allow-Origin', '*')
        super().end_headers()

    def guess_type(self, path):
        # Handle compressed files
        if path.endswith('.br'):
            base_path = path[:-3]
            if base_path.endswith('.js'):
                return 'application/javascript'
            elif base_path.endswith('.wasm'):
                return 'application/wasm'
            elif base_path.endswith('.data'):
                return 'application/octet-stream'
        elif path.endswith('.gz'):
            base_path = path[:-3]
            if base_path.endswith('.js'):
                return 'application/javascript'
            elif base_path.endswith('.wasm'):
                return 'application/wasm'
            elif base_path.endswith('.data'):
                return 'application/octet-stream'
        return super().guess_type(path)

    def send_head(self):
        path = self.translate_path(self.path)

        # Check for .br or .gz files and add appropriate headers
        if os.path.exists(path):
            if path.endswith('.br'):
                f = self.send_head_with_encoding(path, 'br')
                return f
            elif path.endswith('.gz'):
                f = self.send_head_with_encoding(path, 'gzip')
                return f

        return super().send_head()

    def send_head_with_encoding(self, path, encoding):
        try:
            f = open(path, 'rb')
        except OSError:
            self.send_error(404, "File not found")
            return None

        try:
            fs = os.fstat(f.fileno())
            self.send_response(200)
            self.send_header("Content-type", self.guess_type(path))
            self.send_header("Content-Length", str(fs.st_size))
            self.send_header("Content-Encoding", encoding)
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            return f
        except:
            f.close()
            raise

if __name__ == '__main__':
    PORT = 8000
    print(f"Starting server at http://localhost:{PORT}")
    print("Press Ctrl+C to stop")

    with http.server.HTTPServer(("", PORT), BrotliHTTPRequestHandler) as httpd:
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nServer stopped.")
