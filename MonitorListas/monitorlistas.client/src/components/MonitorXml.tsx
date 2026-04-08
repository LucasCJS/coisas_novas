import React, { useEffect, useState, useMemo } from 'react';
import * as signalR from '@microsoft/signalr';

// ==========================================
// INTERFACES
// ==========================================
export interface ArquivoXml {
    nome: string;
    dataGeracao: string;
    isValido: boolean;
    erros: string[];
}

export interface ListaRegistro {
    codigoLista: string;
    nomeLista: string;
    statusLista: string;
    inicioExecucao: string;
    fimExecucao: string;
}

export const MonitorXml: React.FC = () => {
    const [arquivos, setArquivos] = useState<ArquivoXml[]>([]);
    const [executaveis, setExecutaveis] = useState<ListaRegistro[]>([]);
    const [loading, setLoading] = useState(true);
    const [connected, setConnected] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [abaAtiva, setAbaAtiva] = useState<'atuais' | 'historico' | 'executaveis'>('atuais');
    const [busca, setBusca] = useState('');

    // Controle de paginação
    const [paginaAtual, setPaginaAtual] = useState(1);
    const itensPorPagina = 50;

    // ==========================================
    // FUNÇÃO 1: APAGAR DA PASTA ORIGINAL
    // ==========================================
    const apagarOriginal = async (nomeArquivo: string) => {
        if (!window.confirm(`Deseja apagar o arquivo '${nomeArquivo}' APENAS da pasta original?\n\n(O registro de erro continuará no painel)`)) return;

        try {
            const token = localStorage.getItem('monitor_token');
            const response = await fetch(`/api/xml/apagar-original?arquivo=${encodeURIComponent(nomeArquivo)}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                alert("Arquivo apagado da pasta original com sucesso!");
            } else if (response.status === 401) {
                window.location.reload();
            } else {
                alert("Não foi possível apagar o arquivo da pasta original.");
            }
        } catch (erro) {
            alert("Erro de comunicação com o servidor.");
        }
    };

    // ==========================================
    // FUNÇÃO 2: APAGAR DO MONITOR (HARD DELETE)
    // ==========================================
    const apagarDoMonitor = async (nomeArquivo: string) => {
        if (!window.confirm(`ATENÇÃO: Deseja excluir DEFINITIVAMENTE o arquivo '${nomeArquivo}'?\n\nIsso limpará o erro da tela, do banco SQLite e da Quarentena.`)) return;

        try {
            const token = localStorage.getItem('monitor_token');
            const response = await fetch(`/api/xml/apagar-quarentena?arquivo=${encodeURIComponent(nomeArquivo)}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                setArquivos(prev => prev.filter(a => a.nome !== nomeArquivo));
            } else if (response.status === 401) {
                window.location.reload();
            } else {
                alert("Não foi possível excluir o registro do monitor.");
            }
        } catch (erro) {
            alert("Erro de comunicação com o servidor.");
        }
    };

    const handleLogout = () => {
        if (window.confirm("Deseja realmente sair do painel?")) {
            localStorage.removeItem('monitor_token');
            window.location.reload();
        }
    };

    // ==========================================
    // RADAR DE ATUALIZAÇÃO E SIGNALR
    // ==========================================
    useEffect(() => {
        let isMounted = true;

        const carregarDados = async () => {
            const token = localStorage.getItem('monitor_token');
            if (!token) { window.location.reload(); return; }

            try {
                const authHeaders = { 'Authorization': `Bearer ${token}` };
                const [resXml, resExecutaveis] = await Promise.all([
                    fetch('/api/xml/recentes', { headers: authHeaders }),
                    fetch('/api/executaveis/status', { headers: authHeaders })
                ]);

                if (resXml.status === 401 || resExecutaveis.status === 401) {
                    localStorage.removeItem('monitor_token');
                    window.location.reload(); return;
                }

                if (!resXml.ok || !resExecutaveis.ok) throw new Error("Erro ao buscar dados.");

                const dadosXml: ArquivoXml[] = await resXml.json();
                const dadosExecutaveis: ListaRegistro[] = await resExecutaveis.json();

                if (isMounted) {
                    setArquivos(dadosXml);
                    setExecutaveis(dadosExecutaveis);
                    setError(null);
                }
            } catch (erro) {
                if (isMounted) setError("Erro ao carregar dados. Tentando novamente...");
            } finally {
                if (isMounted) setLoading(false);
            }
        };

        carregarDados();
        const interval = setInterval(() => { carregarDados(); }, 5000);
        return () => { isMounted = false; clearInterval(interval); };
    }, []);

    useEffect(() => {
        const token = localStorage.getItem('monitor_token');
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/monitorHub", { accessTokenFactory: () => token || "" })
            .withAutomaticReconnect()
            .build();

        const startConnection = async () => {
            try { await connection.start(); setConnected(true); }
            catch (erro) { setConnected(false); setTimeout(startConnection, 5000); }
        };

        startConnection();

        connection.on("ReceberNovoArquivo", (novoArquivo: ArquivoXml) => {
            setArquivos(prev => {
                if (prev.some(a => a.nome === novoArquivo.nome)) return prev;
                return [novoArquivo, ...prev];
            });
        });

        connection.onreconnecting(() => setConnected(false));
        connection.onreconnected(() => setConnected(true));
        connection.onclose(() => setConnected(false));

        return () => { connection.stop(); };
    }, []);

    // ==========================================
    // LÓGICA DE FILTROS E PAGINAÇÃO
    // ==========================================
    const arquivosDaAba = useMemo(() => {
        if (abaAtiva === 'atuais') return arquivos.filter(a => a.isValido);
        if (abaAtiva === 'historico') return arquivos.filter(a => !a.isValido);
        return [];
    }, [arquivos, abaAtiva]);

    const arquivosFiltrados = useMemo(() => {
        let resultado = arquivosDaAba;
        if (busca) resultado = resultado.filter(arq => arq.nome.toLowerCase().includes(busca.toLowerCase()));
        return resultado;
    }, [arquivosDaAba, busca]);

    const executaveisFiltrados = useMemo(() => {
        if (!busca) return executaveis;
        return executaveis.filter(e => e.nomeLista.toLowerCase().includes(busca.toLowerCase()) || e.codigoLista.toLowerCase().includes(busca.toLowerCase()));
    }, [executaveis, busca]);

    const totalPaginas = Math.ceil((abaAtiva === 'executaveis' ? executaveisFiltrados.length : arquivosFiltrados.length) / itensPorPagina);

    const arquivosDaPagina = useMemo(() => {
        const indiceUltimoItem = paginaAtual * itensPorPagina;
        const indicePrimeiroItem = indiceUltimoItem - itensPorPagina;
        return arquivosFiltrados.slice(indicePrimeiroItem, indiceUltimoItem);
    }, [arquivosFiltrados, paginaAtual, itensPorPagina]);

    const executaveisDaPagina = useMemo(() => {
        const indiceUltimoItem = paginaAtual * itensPorPagina;
        const indicePrimeiroItem = indiceUltimoItem - itensPorPagina;
        return executaveisFiltrados.slice(indicePrimeiroItem, indiceUltimoItem);
    }, [executaveisFiltrados, paginaAtual, itensPorPagina]);

    useEffect(() => { setPaginaAtual(1); setBusca(''); }, [abaAtiva]);

    const renderStatusBadge = (status: string) => {
        if (!status) return null;

        const s = status.toUpperCase();
        let bgColor = '#f1f5f9';
        let color = '#475569';
        let badgeText = status;
        let errorMessage = '';

        if (s.includes('ERRO') || s.includes('INVALID') || status.length > 20) {
            bgColor = '#fef2f2';
            color = '#991b1b';
            badgeText = 'ERRO';
            errorMessage = status.replace(/^ERRO:\s*/i, '');
        } else if (s.includes('FINALIZADO') || s.includes('PROCESSADO')) {
            bgColor = '#dcfce7';
            color = '#166534';
        } else if (s.includes('PROCESSANDO')) {
            bgColor = '#dbeafe';
            color = '#1e40af';
        } else if (s.includes('PENDENTE')) {
            bgColor = '#fef3c7';
            color = '#92400e';
        }

        return (
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '6px' }}>
                <span style={{ backgroundColor: bgColor, color: color, padding: '4px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', letterSpacing: '0.05em', whiteSpace: 'nowrap' }}>
                    {badgeText}
                </span>
                {errorMessage && (
                    <div style={{ fontSize: '12px', color: '#b91c1c', maxWidth: '350px', lineHeight: '1.4', wordWrap: 'break-word' }}>
                        {errorMessage}
                    </div>
                )}
            </div>
        );
    };

    return (
        <div style={styles.container}>
            <div style={styles.card}>
                <div style={styles.header}>
                    <div style={styles.headerContent}>
                        <div style={styles.iconContainer}>
                            <svg style={styles.icon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z" />
                                <polyline points="13 2 13 9 20 9" />
                            </svg>
                        </div>
                        <div>
                            <h1 style={styles.title}>Painel de Auditoria</h1>
                            <p style={styles.subtitle}>Monitoramento de XML e Processamento de Listas</p>
                        </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                        <div style={{ ...styles.badge, backgroundColor: connected ? '#dcfce7' : '#fef3c7', color: connected ? '#166534' : '#92400e' }}>
                            <span style={{ ...styles.dot, backgroundColor: connected ? '#22c55e' : '#f59e0b' }} />
                            {connected ? 'Conectado' : 'Reconectando...'}
                        </div>
                        <button onClick={handleLogout} style={styles.logoutButton} title="Sair do Sistema">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ width: '18px', height: '18px' }}>
                                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                                <polyline points="16 17 21 12 16 7" />
                                <line x1="21" y1="12" x2="9" y2="12" />
                            </svg>
                            <span style={{ fontSize: '13px', fontWeight: '600' }}>Sair</span>
                        </button>
                    </div>
                </div>

                <div style={styles.tabsContainer}>
                    <button
                        style={abaAtiva === 'atuais' ? styles.tabActive : styles.tabInactive}
                        onClick={() => setAbaAtiva('atuais')}
                    >
                        XMLs Válidos (Pasta)
                    </button>
                    <button
                        style={abaAtiva === 'historico' ? styles.tabActive : styles.tabInactive}
                        onClick={() => setAbaAtiva('historico')}
                    >
                        XMLs Inválidos (Erros)
                    </button>
                    <button
                        style={abaAtiva === 'executaveis' ? { ...styles.tabActive, backgroundColor: '#2563eb' } : styles.tabInactive}
                        onClick={() => setAbaAtiva('executaveis')}
                    >
                        Status das Listas (TopShelf)
                    </button>
                </div>

                <div style={styles.toolbar}>
                    <div style={styles.statsContainer}>
                        {/* Único Card de Status, muda a cor conforme a aba */}
                        <div style={{
                            ...styles.statCard,
                            borderLeft: abaAtiva === 'executaveis' ? '4px solid #2563eb' : (abaAtiva === 'atuais' ? '4px solid #22c55e' : '4px solid #ef4444'),
                            cursor: 'default'
                        }}>
                            <span style={styles.statLabel}>Total na Aba</span>
                            <span style={styles.statValue}>
                                {abaAtiva === 'executaveis' ? executaveis.length : arquivosDaAba.length}
                            </span>
                        </div>
                    </div>

                    <div style={styles.searchContainer}>
                        <input
                            type="text"
                            placeholder={abaAtiva === 'executaveis' ? "Pesquisar lista..." : "Pesquisar por nome do XML..."}
                            value={busca}
                            onChange={(e) => setBusca(e.target.value)}
                            style={styles.searchInput}
                        />
                    </div>
                </div>

                {error && <div style={styles.errorBanner}>{error}</div>}

                <div style={styles.tableContainer}>
                    {abaAtiva !== 'executaveis' && (
                        <table style={styles.table}>
                            <thead>
                                <tr style={styles.tableHeaderRow}>
                                    <th style={styles.th}>Nome do Arquivo</th>
                                    <th style={styles.th}>Data de Geração</th>
                                    <th style={{ ...styles.th, textAlign: 'center' }}>Ações</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading ? (
                                    <tr><td colSpan={3} style={styles.loadingCell}><div style={styles.spinner} /><span>Carregando...</span></td></tr>
                                ) : arquivosFiltrados.length === 0 ? (
                                    <tr><td colSpan={3} style={styles.emptyCell}>Nenhum arquivo encontrado.</td></tr>
                                ) : (
                                    arquivosDaPagina.map((arq, index) => (
                                        <tr key={`${arq.nome}-${index}`} style={{ ...styles.tableRow, backgroundColor: arq.isValido ? 'transparent' : '#fef2f2' }}>
                                            <td style={styles.td}>
                                                <div style={styles.fileName}>
                                                    <svg style={{ ...styles.fileIcon, color: arq.isValido ? '#475569' : '#dc2626' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                                        <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                                                        <polyline points="14 2 14 8 20 8" />
                                                    </svg>
                                                    <span style={{ color: arq.isValido ? '#1e293b' : '#dc2626', fontWeight: arq.isValido ? '500' : 'bold' }}>{arq.nome}</span>
                                                    {!arq.isValido && <span style={styles.errorTag}>INVÁLIDO</span>}
                                                </div>

                                                {/* CAIXA DE ERRO COM SCROLL */}
                                                {!arq.isValido && arq.erros && (
                                                    <div style={styles.errorBox}>
                                                        <ul style={styles.errorList}>
                                                            {arq.erros.map((erroMsg, i) => <li key={i}>{erroMsg}</li>)}
                                                        </ul>
                                                    </div>
                                                )}
                                            </td>
                                            <td style={styles.td}>{new Date(arq.dataGeracao).toLocaleString('pt-BR')}</td>
                                            <td style={{ ...styles.td, textAlign: 'center' }}>
                                                <div style={{ display: 'flex', justifyContent: 'center', gap: '8px' }}>
                                                    {/* Botão de Download (Todos) */}
                                                    <a href={`/api/xml/download?arquivo=${encodeURIComponent(arq.nome)}`} download={arq.nome} style={styles.downloadButton} title="Baixar Arquivo">
                                                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={styles.downloadIcon}>
                                                            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="7 10 12 15 17 10" /><line x1="12" y1="15" x2="12" y2="3" />
                                                        </svg>
                                                    </a>

                                                    {/* Botões de Exclusão (APENAS na aba de Histórico/Inválidos) */}
                                                    {!arq.isValido && (
                                                        <>
                                                            <button onClick={() => apagarOriginal(arq.nome)} style={{ ...styles.downloadButton, backgroundColor: '#fffbeb', color: '#d97706', borderColor: '#fde68a' }} title="Apagar apenas da Pasta Original (Drop ERP)">
                                                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={styles.downloadIcon}>
                                                                    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                                                                    <line x1="9" y1="14" x2="15" y2="14" />
                                                                </svg>
                                                            </button>

                                                            <button onClick={() => apagarDoMonitor(arq.nome)} style={{ ...styles.downloadButton, backgroundColor: '#fef2f2', color: '#dc2626', borderColor: '#fecaca' }} title="Excluir Definitivamente do Painel (SQLite + Quarentena)">
                                                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={styles.downloadIcon}>
                                                                    <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                                                                </svg>
                                                            </button>
                                                        </>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    )}

                    {abaAtiva === 'executaveis' && (
                        <table style={styles.table}>
                            <thead>
                                <tr style={styles.tableHeaderRow}>
                                    <th style={{ ...styles.th, width: '25%' }}>Nome da Lista / ID</th>
                                    <th style={{ ...styles.th, width: '45%' }}>Status Atual</th>
                                    <th style={styles.th}>Início</th>
                                    <th style={styles.th}>Última Atualização</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading ? (
                                    <tr><td colSpan={4} style={styles.loadingCell}><div style={styles.spinner} /><span>Buscando Listas...</span></td></tr>
                                ) : executaveisFiltrados.length === 0 ? (
                                    <tr><td colSpan={4} style={styles.emptyCell}>Nenhuma lista encontrada.</td></tr>
                                ) : (
                                    executaveisDaPagina.map((exe, index) => (
                                        <tr key={`${exe.codigoLista}-${index}`} style={styles.tableRow}>
                                            <td style={styles.td}>
                                                <div style={{ fontWeight: '600', color: '#0f172a', marginBottom: '4px' }}>{exe.nomeLista}</div>
                                                <div style={{ fontSize: '11px', color: '#64748b', wordWrap: 'break-word', maxWidth: '200px' }}>ID: {exe.codigoLista}</div>
                                            </td>
                                            <td style={{ ...styles.td, verticalAlign: 'top' }}>
                                                {renderStatusBadge(exe.statusLista)}
                                            </td>
                                            <td style={styles.td}>
                                                {new Date(exe.inicioExecucao).getFullYear() > 2000
                                                    ? new Date(exe.inicioExecucao).toLocaleString('pt-BR')
                                                    : '-'}
                                            </td>
                                            <td style={styles.td}>
                                                {new Date(exe.fimExecucao).getFullYear() > 2000
                                                    ? new Date(exe.fimExecucao).toLocaleString('pt-BR')
                                                    : '-'}
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    )}
                </div>

                <div style={styles.footer}>
                    <span style={styles.footerText}>
                        Mostrando {abaAtiva === 'executaveis' ? executaveisDaPagina.length : arquivosDaPagina.length} de {abaAtiva === 'executaveis' ? executaveisFiltrados.length : arquivosFiltrados.length} registro(s)
                    </span>

                    {totalPaginas > 1 && (
                        <div style={styles.pagination}>
                            <button style={{ ...styles.pageButton, opacity: paginaAtual === 1 ? 0.5 : 1 }} onClick={() => setPaginaAtual(p => Math.max(p - 1, 1))} disabled={paginaAtual === 1}>Anterior</button>
                            <span style={styles.pageInfo}>Página {paginaAtual} de {totalPaginas}</span>
                            <button style={{ ...styles.pageButton, opacity: paginaAtual === totalPaginas ? 0.5 : 1 }} onClick={() => setPaginaAtual(p => Math.min(p + 1, totalPaginas))} disabled={paginaAtual === totalPaginas}>Próxima</button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

// --- ESTILOS ---
const styles: { [key: string]: React.CSSProperties } = {
    container: { minHeight: '100vh', backgroundColor: '#f8fafc', padding: '48px 16px', fontFamily: 'system-ui, -apple-system, sans-serif' },
    card: { maxWidth: '1100px', margin: '0 auto', backgroundColor: '#ffffff', borderRadius: '16px', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)', overflow: 'hidden', border: '1px solid #e2e8f0' },
    header: { padding: '28px 32px', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' },
    headerContent: { display: 'flex', alignItems: 'center', gap: '16px' },
    iconContainer: { width: '52px', height: '52px', backgroundColor: '#0f172a', borderRadius: '12px', display: 'flex', alignItems: 'center', justifyContent: 'center' },
    icon: { width: '26px', height: '26px', color: '#ffffff' },
    title: { fontSize: '22px', fontWeight: '700', color: '#0f172a', margin: 0 },
    subtitle: { fontSize: '14px', color: '#64748b', margin: '4px 0 0 0' },
    badge: { display: 'flex', alignItems: 'center', gap: '8px', padding: '8px 16px', borderRadius: '9999px', fontSize: '14px', fontWeight: '500' },
    dot: { width: '8px', height: '8px', borderRadius: '50%', animation: 'pulse 2s infinite' },
    tabsContainer: { display: 'flex', gap: '12px', padding: '20px 32px 0 32px', backgroundColor: '#f8fafc' },
    tabActive: { padding: '10px 20px', backgroundColor: '#0f172a', color: '#ffffff', border: 'none', borderRadius: '8px', fontWeight: '600', cursor: 'pointer', transition: 'all 0.2s ease', boxShadow: '0 2px 4px rgba(0,0,0,0.1)' },
    tabInactive: { padding: '10px 20px', backgroundColor: '#e2e8f0', color: '#475569', border: 'none', borderRadius: '8px', fontWeight: '500', cursor: 'pointer', transition: 'all 0.2s ease' },
    toolbar: { padding: '20px 32px', borderBottom: '1px solid #e2e8f0', backgroundColor: '#f8fafc', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '20px' },
    statsContainer: { display: 'flex', gap: '16px', flexWrap: 'wrap' },
    statCard: { backgroundColor: 'white', padding: '12px 20px', borderRadius: '8px', border: '1px solid #e2e8f0', display: 'flex', flexDirection: 'column', minWidth: '120px', boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)', transition: 'all 0.2s ease' },
    statLabel: { fontSize: '12px', color: '#64748b', textTransform: 'uppercase', fontWeight: '600', marginBottom: '4px', pointerEvents: 'none' },
    statValue: { fontSize: '24px', fontWeight: 'bold', color: '#0f172a', pointerEvents: 'none' },
    searchContainer: { flex: '1', minWidth: '250px', display: 'flex', justifyContent: 'flex-end' },
    searchInput: { padding: '10px 16px', borderRadius: '8px', border: '1px solid #cbd5e1', width: '100%', maxWidth: '300px', fontSize: '14px', outline: 'none' },
    errorBanner: { padding: '12px 32px', backgroundColor: '#fef2f2', color: '#991b1b', fontSize: '14px', borderBottom: '1px solid #fecaca' },
    tableContainer: { overflowX: 'auto' },
    table: { width: '100%', borderCollapse: 'collapse' },
    tableHeaderRow: { backgroundColor: '#ffffff', borderBottom: '2px solid #e2e8f0' },
    th: { padding: '14px 32px', textAlign: 'left', fontSize: '11px', fontWeight: '700', color: '#475569', textTransform: 'uppercase', letterSpacing: '0.08em' },
    tableRow: { borderBottom: '1px solid #f1f5f9', transition: 'background-color 0.15s ease' },
    td: { padding: '18px 32px', fontSize: '14px', color: '#334155' },
    fileName: { display: 'flex', alignItems: 'center', gap: '12px' },
    fileIcon: { width: '20px', height: '20px' },
    errorTag: { backgroundColor: '#dc2626', color: 'white', padding: '2px 8px', borderRadius: '4px', fontSize: '10px', fontWeight: 'bold', letterSpacing: '0.05em' },
    errorBox: { marginTop: '12px', marginLeft: '32px', maxHeight: '100px', overflowY: 'auto', backgroundColor: '#ffffff', border: '1px solid #fecaca', borderRadius: '6px', padding: '8px 12px' },
    errorList: { margin: 0, paddingLeft: '16px', color: '#b91c1c', fontSize: '12px', lineHeight: '1.6' },
    loadingCell: { padding: '64px', textAlign: 'center', color: '#64748b', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '16px' },
    spinner: { width: '32px', height: '32px', border: '3px solid #e2e8f0', borderTopColor: '#0f172a', borderRadius: '50%', animation: 'spin 1s linear infinite' },
    emptyCell: { padding: '64px', textAlign: 'center', color: '#94a3b8', fontSize: '15px' },
    footer: { padding: '16px 32px', borderTop: '1px solid #e2e8f0', backgroundColor: '#fafbfc', textAlign: 'center' },
    footerText: { fontSize: '13px', color: '#64748b', fontWeight: '500' },
    pagination: { display: 'flex', alignItems: 'center', gap: '16px', marginTop: '12px', justifyContent: 'center' },
    pageButton: { padding: '8px 16px', backgroundColor: '#0f172a', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '14px', fontWeight: '500' },
    pageInfo: { fontSize: '14px', color: '#475569', fontWeight: '500' },
    downloadButton: { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', padding: '8px', backgroundColor: '#f1f5f9', color: '#0f172a', borderRadius: '6px', textDecoration: 'none', transition: 'background-color 0.2s ease', border: '1px solid #e2e8f0', cursor: 'pointer' },
    downloadIcon: { width: '18px', height: '18px' },
    logoutButton: { display: 'flex', alignItems: 'center', gap: '6px', padding: '8px 12px', backgroundColor: '#fef2f2', color: '#991b1b', border: '1px solid #fecaca', borderRadius: '8px', cursor: 'pointer', transition: 'all 0.2s ease', outline: 'none' },
};