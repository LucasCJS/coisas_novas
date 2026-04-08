import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import basicSsl from '@vitejs/plugin-basic-ssl'; // <-- 1. Importe o plugin aqui

export default defineConfig({
    plugins: [
        react(),
        basicSsl() // <-- 2. Ative o plugin aqui
    ],
    server: {
        port: 52912,
        strictPort: true,

        // ... (o resto do seu proxy continua igualzinho)
        proxy: {
            '/api': {
                target: 'https://localhost:7109',
                changeOrigin: true,
                secure: false,
            },
            '/monitorHub': {
                target: 'https://localhost:7109',
                changeOrigin: true,
                secure: false,
                ws: true,
            }
        }
    }
});