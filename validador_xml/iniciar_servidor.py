import os
from waitress import serve
from app import app  # Importa o seu Flask

if __name__ == '__main__':
    # O IIS passa a porta através de uma variável de ambiente. 
    # Se não existir (a testar fora do IIS), usa a 8080 por defeito.
    porta = int(os.environ.get("PORT", 8080))
    
    print(f"A iniciar o Waitress na porta {porta}...")
    
    # O host '127.0.0.1' é mais seguro aqui, pois o IIS é que estará virado para a rede local
    serve(app, host='127.0.0.1', port=porta, threads=20)