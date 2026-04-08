import re
import xml.etree.ElementTree as ET
from datetime import datetime
from collections import Counter
import io
from flask import Flask, render_template, request
from concurrent.futures import ThreadPoolExecutor

app = Flask(__name__)

# --- OTIMIZAÇÃO 1: Regex Negativa (Caça os inválidos instantaneamente) ---
INVALID_XML_CHARS = re.compile(r"[^\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\U00010000-\U0010FFFF]")

def find_invalid_chars(text):
    return INVALID_XML_CHARS.findall(text)

# --- FUNÇÕES DE VALIDAÇÃO ---
def is_date(value):
    if not value or value.strip() == "":
        return True
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%Y/%m/%d"):
        try:
            datetime.strptime(value.strip(), fmt)
            return True
        except ValueError:
            continue
    return False

def is_numeric(value):
    return value.isdigit() if value else True

def validar_cpf(cpf):
    if not cpf or len(cpf) != 11 or not cpf.isdigit(): return False
    if cpf == cpf[0] * 11: return False
    soma1 = sum(int(cpf[i]) * (10 - i) for i in range(9))
    dig1 = ((soma1 * 10) % 11) % 10
    soma2 = sum(int(cpf[i]) * (11 - i) for i in range(10))
    dig2 = ((soma2 * 10) % 11) % 10
    return dig1 == int(cpf[9]) and dig2 == int(cpf[10])

def validar_cnpj(cnpj):
    if not cnpj or len(cnpj) != 14 or not cnpj.isdigit(): return False
    if cnpj == cnpj[0] * 14: return False
    peso1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
    peso2 = [6] + peso1
    soma1 = sum(int(cnpj[i]) * peso1[i] for i in range(12))
    dig1 = 11 - (soma1 % 11)
    dig1 = 0 if dig1 >= 10 else dig1
    soma2 = sum(int(cnpj[i]) * peso2[i] for i in range(13))
    dig2 = 11 - (soma2 % 11)
    dig2 = 0 if dig2 >= 10 else dig2
    return dig1 == int(cnpj[12]) and dig2 == int(cnpj[13])

# --- PROCESSAMENTO PRINCIPAL ---
def processar_xml(conteudo_bytes):
    """Recebe os bytes puros para ser seguro no processamento paralelo"""
    erros = []
    try:
        conteudo_txt = conteudo_bytes.decode('utf-8')
        linhas = conteudo_txt.splitlines()

        # ETAPA 1: Caracteres inválidos (Muito mais rápido agora)
        for num_linha, linha in enumerate(linhas, start=1):
            invalidos = find_invalid_chars(linha)
            if invalidos:
                tag = re.search(r"<(/?)(\w+)", linha)
                tag_nome = tag.group(2) if tag else "desconhecida"
                detalhe = f"Caracteres inválidos: {repr(list(set(invalidos)))}"
                erros.append({"tipo": "CARACTERE INVALIDO", "tag": tag_nome, "detalhe": detalhe, "linha": num_linha})

        # ETAPA 2: Parsing e Negócio
        root = ET.fromstring(conteudo_bytes)
        registros = []

        for listado in root.findall("./LISTADO/TB_LV"):
            cd_listado = listado.findtext("CD_LISTADO", "").strip()
            de_listado = listado.findtext("DE_LISTADO", "").strip()
            cpf_cnpj = listado.findtext("CPF_CNPJ", "").strip()
            dt_nasc = listado.findtext("DT_NASC_FUNDACAO", "").strip()
            dt_desat = listado.findtext("DT_DESATIVACAO", "").strip()

            registros.append((cd_listado, de_listado))

            if len(cd_listado) > 20: erros.append({"tipo": "VALIDAÇÃO CAMPOS", "tag": "CD_LISTADO", "detalhe": f"Tamanho inválido ({len(cd_listado)})", "linha": "-"})
            if len(de_listado) > 120: erros.append({"tipo": "VALIDAÇÃO CAMPOS", "tag": "DE_LISTADO", "detalhe": f"Tamanho inválido ({len(de_listado)})", "linha": "-"})

            if cpf_cnpj == "": erros.append({"tipo": "VALIDAÇÃO CAMPOS", "tag": "CPF_CNPJ", "detalhe": "CPF/CNPJ obrigatório", "linha": "-"})
            elif cpf_cnpj != "0":
                if len(cpf_cnpj) not in (11, 14): erros.append({"tipo": "VALIDAÇÃO TAMANHO", "tag": "CPF_CNPJ", "detalhe": f"Tamanho {len(cpf_cnpj)} inválido", "linha": "-"})
                elif not is_numeric(cpf_cnpj): erros.append({"tipo": "VALIDAÇÃO CAMPOS", "tag": "CPF_CNPJ", "detalhe": "Valor não numérico", "linha": "-"})
                else:
                    if len(cpf_cnpj) == 11 and not validar_cpf(cpf_cnpj): erros.append({"tipo": "VALIDAÇÃO CPF", "tag": "CPF_CNPJ", "detalhe": f"CPF {cpf_cnpj} inválido", "linha": "-"})
                    if len(cpf_cnpj) == 14 and not validar_cnpj(cpf_cnpj): erros.append({"tipo": "VALIDAÇÃO CNPJ", "tag": "CPF_CNPJ", "detalhe": f"CNPJ {cpf_cnpj} inválido", "linha": "-"})

            if not is_date(dt_nasc): erros.append({"tipo": "VALIDAÇÃO DATA", "tag": "DT_NASC_FUNDACAO", "detalhe": f"Data '{dt_nasc}' inválida", "linha": "-"})
            if not is_date(dt_desat): erros.append({"tipo": "VALIDAÇÃO DATA", "tag": "DT_DESATIVACAO", "detalhe": f"Data '{dt_desat}' inválida", "linha": "-"})

        # Duplicidades
        contagem = Counter(registros)
        for (cd, de), qtd in contagem.items():
            if qtd > 1:
                erros.append({"tipo": "VALIDAÇÃO DUPLICIDADE", "tag": "TB_LV", "detalhe": f"Duplicado ({qtd}x): {cd} - {de}", "linha": "-"})

    except Exception as e:
        erros.append({"tipo": "ERRO GERAL", "tag": "SISTEMA", "detalhe": str(e), "linha": "-"})

    return erros

def processar_arquivo_worker(conteudo_bytes, filename):
    """Função empacotadora para rodar em paralelo"""
    lista_erros = processar_xml(conteudo_bytes)
    resumo = dict(Counter(e["tipo"] for e in lista_erros))
    return {'arquivo': filename, 'erros': lista_erros, 'resumo': resumo}

@app.route('/', methods=['GET', 'POST'])
def index():
    resultados = []
    if request.method == 'POST':
        files = request.files.getlist('file')
        
        # Lê os arquivos para a memória primeiro
        arquivos_preparados = []
        for file in files:
            if file and file.filename.lower().endswith('.xml'):
                arquivos_preparados.append((file.read(), file.filename))
        
        # OTIMIZAÇÃO 2: Processamento Paralelo (Dispara 20 trabalhadores virtuais)
        with ThreadPoolExecutor(max_workers=20) as executor:
            futures = [executor.submit(processar_arquivo_worker, dados, nome) for dados, nome in arquivos_preparados]
            for future in futures:
                resultados.append(future.result())

    return render_template('index.html', resultados=resultados)

if __name__ == '__main__':
    app.run(debug=True)