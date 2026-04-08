using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MonitorListas.Server.Services
{
    public static class ValidadorXml
    {
        public static (bool IsValido, List<string> Erros) Validar(string caminhoDoArquivo)
        {
            var erros = new List<string>();
            var contagemRegistros = new Dictionary<(string cd, string de), int>();

            try
            {
                using (var stream = new FileStream(caminhoDoArquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    while (reader.Read())
                    {
                        // 1. Localiza a tag de abertura do registro
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "TB_LV")
                        {
                            // Se a tag pai for <TB_LV />, já é um erro de estrutura
                            if (reader.IsEmptyElement)
                            {
                                erros.Add("Erro: A tag <TB_LV /> está auto-fechada. O registro está vazio.");
                                continue;
                            }

                            // Criamos variáveis para armazenar os dados que seriam do XElement
                            string cdListado = "", deListado = "", cpfCnpj = "", dtNasc = "", dtDesat = "";

                            // 2. Entramos no conteúdo de TB_LV para validar cada tag filha
                            using (XmlReader inner = reader.ReadSubtree())
                            {
                                inner.Read(); // Avança para o nó "TB_LV"

                                while (inner.Read())
                                {
                                    if (inner.NodeType == XmlNodeType.Element)
                                    {
                                        // CHECAGEM CRÍTICA: Se a tag filha for <tag />, gera o erro
                                        if (inner.IsEmptyElement)
                                        {
                                            erros.Add($"Erro de Estrutura: A tag <{inner.Name} /> dentro de TB_LV não é permitida.");
                                        }

                                        // Captura dos valores para sua lógica de negócio
                                        string nomeTag = inner.Name;

                                        // Lemos o conteúdo da tag (isso funciona para <tag>valor</tag> e para <tag></tag>)
                                        string valor = inner.ReadElementContentAsString().Trim();

                                        switch (nomeTag)
                                        {
                                            case "CD_LISTADO": cdListado = valor; break;
                                            case "DE_LISTADO": deListado = valor; break;
                                            case "CPF_CNPJ": cpfCnpj = valor; break;
                                            case "DT_NASC_FUNDACAO": dtNasc = valor; break;
                                            case "DT_DESATIVACAO": dtDesat = valor; break;
                                        }
                                    }
                                }
                            }

                            // 3. SUA LÓGICA DE VALIDAÇÃO DE DADOS (Agora com as variáveis preenchidas)
                            var chave = (cdListado, deListado);
                            if (contagemRegistros.TryGetValue(chave, out int count))
                                contagemRegistros[chave] = count + 1;
                            else
                                contagemRegistros[chave] = 1;

                            // Validações de Regra de Negócio
                            if (cdListado.Length > 20)
                                erros.Add($"CD_LISTADO: Tamanho inválido ({cdListado.Length})");

                            if (deListado.Length > 120)
                                erros.Add($"DE_LISTADO: Tamanho inválido ({deListado.Length}), CD_LISTADO: {cdListado}");

                            if (string.IsNullOrEmpty(cpfCnpj))
                            {
                                erros.Add($"CPF_CNPJ: Obrigatório, CD_LISTADO: {cdListado}");
                            }
                            else if (cpfCnpj != "0")
                            {
                                if (cpfCnpj.Length != 11 && cpfCnpj.Length != 14)
                                    erros.Add($"CPF_CNPJ: Tamanho {cpfCnpj.Length} inválido, CD_LISTADO: {cdListado}");
                                else if (!cpfCnpj.All(char.IsDigit))
                                    erros.Add($"CPF_CNPJ: Valor não numérico, CD_LISTADO: {cdListado}");
                                //else if (cpfCnpj.Length == 11 && !ValidarCpfOtimizado(cpfCnpj))
                                //    erros.Add($"CPF_CNPJ: CPF {cpfCnpj} inválido, CD_LISTADO :{cdListado}");
                                //else if (cpfCnpj.Length == 14 && !ValidarCnpjOtimizado(cpfCnpj))
                                //    erros.Add($"CPF_CNPJ: CNPJ {cpfCnpj} inválido, CD_LISTADO :{cdListado}");
                            }

                            if (!IsDateValid(dtNasc))
                                erros.Add($"DT_NASC_FUNDACAO: Data '{dtNasc}' inválida, CD_LISTADO: {cdListado}");

                            if (!IsDateValid(dtDesat))
                                erros.Add($"DT_DESATIVACAO: Data '{dtDesat}' inválida, CD_LISTADO: {cdListado}");
                        }
                    }
                }

                foreach (var kvp in contagemRegistros)
                {
                    if (kvp.Value > 1)
                    {
                        erros.Add($"TB_LV: Duplicado ({kvp.Value}x): {kvp.Key.cd} - {kvp.Key.de}");
                    }
                }
            }
            catch (Exception ex)
            {
                erros.Add("SISTEMA/XML: " + ex.Message);
            }

            return (erros.Count == 0, erros);
        }

        private static bool IsDateValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy/MM/dd" };
            return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static bool ValidarCpfOtimizado(string cpf)
        {
            if (cpf.Length != 11) return false;
            bool todosIguais = true;
            for (int i = 1; i < 11; i++) if (cpf[i] != cpf[0]) { todosIguais = false; break; }
            if (todosIguais) return false;

            int soma = 0;
            for (int i = 0; i < 9; i++) soma += (cpf[i] - '0') * (10 - i);
            int resto = soma % 11;
            int digito1 = resto < 2 ? 0 : 11 - resto;
            if ((cpf[9] - '0') != digito1) return false;

            soma = 0;
            for (int i = 0; i < 10; i++) soma += (cpf[i] - '0') * (11 - i);
            resto = soma % 11;
            int digito2 = resto < 2 ? 0 : 11 - resto;

            return (cpf[10] - '0') == digito2;
        }

        private static bool ValidarCnpjOtimizado(string cnpj)
        {
            if (cnpj.Length != 14) return false;

            bool todosIguais = true;
            for (int i = 1; i < 14; i++) if (cnpj[i] != cnpj[0]) { todosIguais = false; break; }
            if (todosIguais) return false;

            int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            int soma = 0;
            for (int i = 0; i < 12; i++) soma += (cnpj[i] - '0') * mult1[i];
            int resto = soma % 11;
            int digito1 = resto < 2 ? 0 : 11 - resto;
            if ((cnpj[12] - '0') != digito1) return false;

            soma = 0;
            for (int i = 0; i < 13; i++) soma += (cnpj[i] - '0') * mult2[i];
            resto = soma % 11;
            int digito2 = resto < 2 ? 0 : 11 - resto;

            return (cnpj[13] - '0') == digito2;
        }
    }
}