import React, { useState } from 'react';

export const Login: React.FC = () => {
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        setError(null);

        try {
            // O proxy do Vite vai redirecionar isso para a porta do seu C#
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ username, password })
            });

            if (response.ok) {
                const data = await response.json();

                // Salva o token JWT no navegador
                localStorage.setItem('monitor_token', data.token);

                // Recarrega a página para o App.tsx detectar o token e mostrar o painel
                window.location.reload();
            } else {
                // Tenta ler a mensagem de erro que o backend mandou
                const errData = await response.json();
                setError(errData.mensagem || "Usuário ou senha inválidos.");
            }
        } catch (err) {
            setError("Erro ao tentar conectar com o servidor.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={styles.container}>
            <div style={styles.card}>
                {/* Cabeçalho do Login */}
                <div style={styles.header}>
                    <div style={styles.iconContainer}>
                        <svg style={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                        </svg>
                    </div>
                    <h1 style={styles.title}>Acesso ao Monitor</h1>
                    <p style={styles.subtitle}>Insira suas credenciais</p>
                </div>

                {/* Formulário */}
                <form onSubmit={handleLogin} style={styles.form}>
                    {error && <div style={styles.errorBanner}>{error}</div>}

                    <div style={styles.inputGroup}>
                        <label style={styles.label} htmlFor="username">Usuário (Login)</label>
                        <input
                            id="username"
                            type="text"
                            value={username}
                            onChange={(e) => setUsername(e.target.value)}
                            style={styles.input}
                            placeholder="Digite seu login"
                            required
                        />
                    </div>

                    <div style={styles.inputGroup}>
                        <label style={styles.label} htmlFor="password">Senha</label>
                        <input
                            id="password"
                            type="password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            style={styles.input}
                            placeholder="••••••••"
                            required
                        />
                    </div>

                    <button
                        type="submit"
                        style={{ ...styles.button, opacity: loading ? 0.7 : 1 }}
                        disabled={loading}
                    >
                        {loading ? (
                            <div style={styles.spinner} />
                        ) : (
                            'Entrar'
                        )}
                    </button>
                </form>
            </div>
        </div>
    );
};

// --- ESTILOS ---
const styles: { [key: string]: React.CSSProperties } = {
    container: {
        minHeight: '100vh',
        backgroundColor: '#f8fafc',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '16px',
        fontFamily: 'system-ui, -apple-system, sans-serif'
    },
    card: {
        width: '100%',
        maxWidth: '400px',
        backgroundColor: '#ffffff',
        borderRadius: '16px',
        boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)',
        overflow: 'hidden',
        border: '1px solid #e2e8f0'
    },
    header: {
        padding: '32px 32px 24px 32px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center'
    },
    iconContainer: {
        width: '56px',
        height: '56px',
        backgroundColor: '#0f172a',
        borderRadius: '16px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: '16px'
    },
    icon: {
        width: '28px',
        height: '28px',
        color: '#ffffff'
    },
    title: {
        fontSize: '24px',
        fontWeight: '700',
        color: '#0f172a',
        margin: '0 0 8px 0'
    },
    subtitle: {
        fontSize: '14px',
        color: '#64748b',
        margin: 0
    },
    form: {
        padding: '0 32px 32px 32px',
        display: 'flex',
        flexDirection: 'column',
        gap: '20px'
    },
    inputGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px'
    },
    label: {
        fontSize: '13px',
        fontWeight: '600',
        color: '#475569',
        textTransform: 'uppercase',
        letterSpacing: '0.05em'
    },
    input: {
        padding: '12px 16px',
        borderRadius: '8px',
        border: '1px solid #cbd5e1',
        fontSize: '15px',
        outline: 'none',
        color: '#0f172a',
        backgroundColor: '#f8fafc',
        transition: 'border-color 0.2s ease'
    },
    button: {
        marginTop: '8px',
        padding: '14px',
        backgroundColor: '#0f172a',
        color: '#ffffff',
        border: 'none',
        borderRadius: '8px',
        fontSize: '16px',
        fontWeight: '600',
        cursor: 'pointer',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        transition: 'background-color 0.2s ease',
        boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)'
    },
    errorBanner: {
        padding: '12px',
        backgroundColor: '#fef2f2',
        color: '#991b1b',
        fontSize: '13px',
        borderRadius: '8px',
        border: '1px solid #fecaca',
        textAlign: 'center',
        fontWeight: '500'
    },
    spinner: {
        width: '20px',
        height: '20px',
        border: '3px solid rgba(255,255,255,0.3)',
        borderTopColor: '#ffffff',
        borderRadius: '50%',
        animation: 'spin 1s linear infinite'
    }
};