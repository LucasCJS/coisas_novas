import React, { useState, useEffect } from 'react';
import { Login } from './components/Login'; // Ajuste o caminho para onde você salvou o Login
import { MonitorXml } from './components/MonitorXml'; // Ajuste o caminho

function App() {
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        // Ao carregar a aplicação, checa se existe o Token
        const token = localStorage.getItem('monitor_token');
        if (token) {
            setIsAuthenticated(true);
        }
        setLoading(false);
    }, []);

    if (loading) {
        return <div>Carregando aplicação...</div>;
    }

    // Se tem a chave, mostra o painel. Se não, mostra a tela de login.
    return (
        <>
            {isAuthenticated ? <MonitorXml /> : <Login />}
        </>
    );
}

export default App;