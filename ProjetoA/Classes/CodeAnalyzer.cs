﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjetoA.Classes;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net;
using Windows.UI.Xaml.Shapes;
using System.IO;
using Projeto.Classes;
using System.Diagnostics;
using Windows.UI.Xaml.Documents;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI.Xaml;
using System.Threading.Tasks;

namespace ProjetoA
{
    //Hora de testar os outros métodos

    public class CodeAnalyzer
    {
        static Dictionary<int, int> linhasVulneraveis;


        public static string GerarRelatorioHTML(string code)
        {
            linhasVulneraveis = new Dictionary<int, int>();
            
            var htmlBuilder = new StringBuilder();
            code = code.Trim();

            // Início do HTML
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine("<html lang=\"pt\">");
            htmlBuilder.AppendLine("<head><meta charset=\"utf-8\"><title>Análise de Código</title>");
            htmlBuilder.AppendLine("<style>body {\r\n                                font-family: Arial, sans-serif;\r\n                            }\r\n                        \r\n                            h1 {\r\n                                text-align: center;\r\n                                margin-bottom: 20px;\r\n                            }\r\n                        \r\n                            h2 {\r\n                            text-align: center;\r\n                            margin-bottom: 15px;\r\n                            margin-top: 80px; /* Ajuste o valor conforme necessário */\r\n                            }\r\n                        \r\n                            h3 {\r\n                                text-align: center;\r\n                                margin-bottom: 10px;\r\n                            }\r\n                        \r\n                            a {\r\n                                text-decoration: none;\r\n                                color: #333;\r\n                                cursor: pointer;\r\n                            }\r\n                        \r\n                            a:hover {\r\n                                color: #007bff;\r\n                            }\r\n                        \r\n                            .indice {\r\n                                text-align: center;\r\n                                margin-bottom: 30px;\r\n                                display: block;\r\n                            }\r\n                        \r\n                            ul {\r\n                                list-style: none;\r\n                                padding: 0;\r\n                            }\r\n                        \r\n                            li {\r\n                                margin-bottom: 10px;\r\n                                font-size: 18px;\r\n                            }\r\n                        \r\n                            table {\r\n                                width: 100%;\r\n                                border-collapse: collapse;\r\n                                margin-top: 20px;\r\n                            }\r\n                        \r\n                            /* Estilo para as células da tabela */\r\n                            table td, table th {\r\n                                padding: 10px;\r\n                                border: 1px solid #ddd;\r\n                                text-align: left;\r\n                            }\r\n                        \r\n                            /* Estilo para o cabeçalho da tabela */\r\n                            table th {\r\n                                background-color: #f2f2f2;\r\n                            }\r\n                        \r\n                            /* Estilo para alternância de cores nas linhas */\r\n                            .table tr:nth-child(even) {\r\n                                background-color: #f9f9f9;\r\n                            }\r\n                        \r\n                            .alto {\r\n                                background-color: rgb(238, 93, 93);\r\n                                font-weight: bold;\r\n                            }\r\n                        \r\n                            .medio {\r\n                                background-color: yellow;\r\n                                \r\n                            }\r\n                        \r\n                            .baixo {\r\n                                background-color: greenyellow;\r\n                                \r\n                            }\r\n                        \r\n                            /* Estilo para o código analisado */\r\n                            .codigo-container {\r\n                                margin-top: 20px;\r\n                                padding: 10px;\r\n                                background-color: #f2f2f2;\r\n                            }\r\n                        \r\n                            .codigo-container pre {\r\n                                white-space: pre-wrap;\r\n                                font-size: 14px;\r\n                            }\r\n                            \r\n                            span{\r\n                                color: rgb(137, 8, 8);\r\n                            }</style>");
            //htmlBuilder.AppendLine("<style type=\"text/css\" id=\"operaUserStyle\"></style>");
            htmlBuilder.AppendLine("<script> function mostrarSecao(id) {\r\n            var secao = document.getElementById(id);\r\n            \r\n            if (secao.style.display == '' || secao.style.display == \"none\") {\r\n                secao.style.display = \"block\";\r\n            } \r\n                    \r\n            else {\r\n                secao.style.display = \"none\";\r\n            }\r\n        }\r\n        \r\n        function modificarPadrao(num,risco){\r\n        var minhaDiv = document.getElementById('linha-numero'+num);\r\n\r\n        switch(risco){\r\n            case 0: minhaDiv.classList.add('alto'); break;\r\n            case 1: minhaDiv.classList.add('medio'); break;\r\n            case 2: minhaDiv.classList.add('baixo'); break;\r\n        }\r\n    }</script>");
            htmlBuilder.AppendLine("</head>");

            // Início do corpo HTML
            htmlBuilder.AppendLine("<body>");
            // Título do relatório
            htmlBuilder.AppendLine("<h1>Relatório de Análise de Código C#</h1>");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Verificar Sintaxe do código
            if (EncontrouErrosSintaxe(htmlBuilder, code))
            {
                stopwatch.Stop();

                htmlBuilder.AppendLine("<h2>Não foi possivel efetuar uma análise profunda do código, pois este apresenta erros de sintaxe!</h2>");
                htmlBuilder.AppendLine($"<p>Tempo Total de Análise: {stopwatch.ElapsedMilliseconds}ms</p>");
                htmlBuilder.Append("</body></html>");
                return htmlBuilder.ToString();
            }

            string[] linhasSeparadas = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var linhas = GuardarEmDicionario(linhasSeparadas);

            htmlBuilder.AppendLine("<h2>Índice</h2>\r\n<div class=\"indice\">\r\n<ul>\r\n    " +
                "<li><a onclick=\"mostrarSecao('analise-vulnerabilidade')\">Análise de Vulnerabilidade</a></li>\r\n    " +
                "<li><a onclick=\"mostrarSecao('complexidade-ciclomatica')\">Complexidade Ciclomática</a></li>\r\n   " +
                "<li><a onclick=\"mostrarSecao('analise-dependencias')\">Análise de Dependências</a></li>\r\n   " +
                "<li><a onclick=\"mostrarSecao('mau-desempenho')\">Identificação de Práticas de Mau Desempenho</a></li>\r\n   " +
                "<li><a onclick=\"mostrarSecao('analise-excecoes')\">Análise de Exceções</a></li>\r\n    " +
                "<li><a onclick=\"mostrarSecao('repeticao-codigo')\">Análise de Repetição de Código</a></li>\r\n    " +
                "<li><a onclick=\"mostrarSecao('concorrencia')\">Análise de Concorrência</a></li>\r\n    " +
                "<li><a onclick=\"mostrarSecao('tempo')\">Tempo Total de Análise</a></li>");
            htmlBuilder.AppendLine($"</ul></div>");

            // Adicione a chamada para o método AnalisarVulnerabilidade
          
            var analises = AnalisarCodigo(linhas);

            htmlBuilder.Append(analises.Result);
            

            // Realiza a análise de complexidade ciclomática
            int complexidadeCiclomatica = ComplexidadeCiclomatica.CalcularComplexidadeCiclomatica(code);
            htmlBuilder.AppendLine("<div id=\"complexidade-ciclomatica\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Complexidade Ciclomática: {complexidadeCiclomatica}</h2>");
            htmlBuilder.AppendLine("</div>");

            //Analise de Dependencias
            htmlBuilder.AppendLine("<div id=\"analise-dependencias\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Dependências:</h2>");
            htmlBuilder.Append(AnalizarDependencias(linhas));
            htmlBuilder.AppendLine("</div>");

            // Identificar práticas que afetam o desempenho
            htmlBuilder.AppendLine("<div id=\"mau-desempenho\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Identificação de Práticas de Mau Desempenho:</h2>");
            //IdentificarPraticasDesempenho(htmlBuilder, code);
            htmlBuilder.AppendLine("</div>");

            // Identificar Exceções no código:
            htmlBuilder.AppendLine("<div id=\"analise-excecoes\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Exceções:</h2>");
            AnalisarExcecoes(htmlBuilder, linhas);
            htmlBuilder.AppendLine("</div>");

            //Verificar Repetição de código
            htmlBuilder.AppendLine("<div id=\"repeticao-codigo\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Repetição de código</h2>");
            //VerificarRepeticao(htmlBuilder, linhas);
            htmlBuilder.AppendLine("</div>");

            // Análise de Concorrência
            htmlBuilder.AppendLine("<div id=\"concorrencia\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Concorrência:</h2>");
            //AnalisarConcorrencia(htmlBuilder, code);
            htmlBuilder.AppendLine("</div>");


            stopwatch.Stop();

            htmlBuilder.AppendLine("<div id=\"tempo\" style=\"display:none;\">");
            htmlBuilder.AppendLine($"<h2>Tempo Total de Análise:{stopwatch.ElapsedMilliseconds} ms</h2>");
            htmlBuilder.AppendLine("</div>");

            htmlBuilder.AppendLine($"<h2 id=\"codigo-analisado\">Código Analisado:</h2>");
            ExibirCodigo(linhasSeparadas, htmlBuilder);

            //Marca as linhas que estão com alguma vulnerabilidade
            htmlBuilder.AppendLine("<script>");
            
            if(linhasVulneraveis!=null)
            {
                modificarBackground(linhasVulneraveis, htmlBuilder);
            }
          
            htmlBuilder.AppendLine("</script>");
            // Feche as tags HTML
            htmlBuilder.AppendLine("</body></html>");

            return htmlBuilder.ToString();
        }

        static Dictionary<string, List<int>> GuardarEmDicionario(string[] linhasSeparadas)
        {
            Dictionary<string, List<int>> dicionario = new Dictionary<string, List<int>>();

            int numeroLinha = 1;
            bool isMultiLine = false;

            foreach (string linha in linhasSeparadas)
            {
                string linhaSemComentarios = RemoverComentarios(linha, ref isMultiLine);

                if (!string.IsNullOrWhiteSpace(linhaSemComentarios))
                {
                    if (!dicionario.ContainsKey(linhaSemComentarios))
                    {
                        dicionario[linhaSemComentarios] = new List<int>();
                    }

                    dicionario[linhaSemComentarios].Add(numeroLinha);
                }

                numeroLinha++;
            }

            return dicionario;
        }
        static string RemoverComentarios(string linha, ref bool isMultiline)
        {
            if (string.IsNullOrEmpty(linha))
            {
                return null;
            }

            linha = linha.Trim();
            int fimComentario;

            if (isMultiline)
            {
                fimComentario = linha.IndexOf("*/");

                if (fimComentario != -1)
                {
                    isMultiline = false;
                    linha = linha.Substring(0, fimComentario);
                }

                else
                {
                    return null;
                }
            }

            bool dentroString = false;
            char charAnterior = '\0';

            for (int i = 0; i < linha.Length; i++)
            {
                if (linha[i] == '"' && charAnterior != '\\')
                {
                    dentroString = !dentroString;
                }

                if (!dentroString)
                {
                    int inicioComentario;

                    // Verificar se a linha contém um comentário de uma única linha
                    if (linha[i] == '/' && i + 1 < linha.Length && linha[i + 1] == '/')
                    {
                        linha = linha.Substring(0, i);
                        break;
                    }

                    else if (linha[i] == '/' && i + 1 < linha.Length && linha[i + 1] == '*')
                    {
                        inicioComentario = i;
                        fimComentario = linha.IndexOf("*/", inicioComentario);

                        if (fimComentario != -1)
                        {
                            linha = linha.Remove(inicioComentario, fimComentario - inicioComentario + 2);
                            i = inicioComentario - 1;
                        }
                        else
                        {
                            isMultiline = true;
                            linha = linha.Substring(0, inicioComentario);
                            break;
                        }
                    }
                }

                charAnterior = linha[i];
            }

            return linha;
        }

        static async Task<StringBuilder> AnalisarCodigo(Dictionary<string, List<int>> lines)
        {
            // Inicia as duas tarefas em paralelo
            // Adicione a chamada para o método AnalisarVulnerabilidade

            var taskAnalisarVulnerabilidades = AnalisarVulnerabilidades(lines);
            var taskAnalizarDependencias = AnalizarDependencias(lines);

            // Concatena as strings HTML
            StringBuilder resultadoFinal = new StringBuilder();
            
            resultadoFinal.Append(await taskAnalisarVulnerabilidades);
            resultadoFinal.Append(await taskAnalizarDependencias);

            // Retorna a junção das strings HTML
            return resultadoFinal;
        }




        static async Task<StringBuilder> AnalisarVulnerabilidades(Dictionary<string, List<int>> code)
        {
            StringBuilder htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<div id=\"analise-vulnerabilidade\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Vulnerabilidades:</h2>");

            var vulnerabilidadeVisitor = new VulnerabilidadeVisitor();
            vulnerabilidadeVisitor.Visit(code);

            if(vulnerabilidadeVisitor.VulnerabilidadesEncontradas.Count()==0)
            {
                htmlBuilder.AppendLine("<h3>Não foi encontrada nenhuma vulnerabilidade de segurança!</h3>");
                htmlBuilder.AppendLine("</div>");
                linhasVulneraveis = null;
                return await Task.FromResult(htmlBuilder);
            }

            linhasVulneraveis = new Dictionary<int, int>();
            // Analisar o código usando o visitor
            

            // Construir tabela HTML
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Nome da Vulnerabilidade</th><th>Código</th><th>Linhas</th><th>Nível de Risco</th></tr>");

            foreach (var vulnerabilidade in vulnerabilidadeVisitor.VulnerabilidadesEncontradas)
            {
                htmlBuilder.AppendLine("<tr>");
                htmlBuilder.AppendLine($"<td>{vulnerabilidade.Tipo}</td>");
                htmlBuilder.AppendLine($"<td>{vulnerabilidade.Codigo}</td>");

                var linhas = vulnerabilidade.Linhas;

                htmlBuilder.AppendLine("<td>");

                for (int i = 0; i < linhas.Count(); i++)
                {
                    htmlBuilder.Append($"<a href=\"#linha-numero{linhas[i]}\">{linhas[i]}</a>");

                    linhasVulneraveis[linhas[i]] = (int)vulnerabilidade.NivelRisco;

                    if (i + 1 < linhas.Count)
                    {
                        htmlBuilder.Append(',');
                    }
                }

                htmlBuilder.Append("</td>");

                switch (vulnerabilidade.NivelRisco)
                {
                    case NivelRisco.Baixo: htmlBuilder.AppendLine("<td class=\"baixo\">Baixo</td>"); break;
                    case NivelRisco.Medio: htmlBuilder.AppendLine("<td class=\"medio\">Médio</td>"); break;
                    case NivelRisco.Alto: htmlBuilder.AppendLine("<td class=\"alto\">Alto</td>"); break;
                }

                htmlBuilder.AppendLine("</tr>");
            }

            htmlBuilder.AppendLine("</table>");

            htmlBuilder.AppendLine($"<h3>Taxa de Precisão de Análise de Vulnerabilidades: {vulnerabilidadeVisitor.getPrecision()}%</h3>");
            htmlBuilder.AppendLine("</div>");

            return await Task.FromResult(htmlBuilder);

        }
        static async Task<StringBuilder> AnalizarDependencias( Dictionary<string, List<int>> lines)
        {
            // Expressão regular para encontrar os usings
            StringBuilder htmlBuilder = new StringBuilder();
            
            htmlBuilder.AppendLine("<div id=\"analise-dependencias\" style=\"display: none;\">");
            htmlBuilder.AppendLine($"<h2>Análise de Dependências:</h2>");

            Regex usingRegex = new Regex(@"\busing\s+([^\s;]+)\s*;");

            bool tabelaVazia = true;
            StringBuilder tabelaHtml = new StringBuilder();

            // Dividir o código em linhas

            tabelaHtml.AppendLine("<table><tr><th>Excerto do Código</th><th>Linhas</th></tr>");

            foreach (var codigo in lines.Keys)
            {
                Match match = usingRegex.Match(codigo);

                if (match.Success)
                {
                    tabelaHtml.AppendLine("<tr>");
                    tabelaHtml.AppendLine($"<td>{codigo}</td>");
                    tabelaHtml.AppendLine($"<td>");

                    for (int i = 0; i < lines[codigo].Count(); i++)
                    {
                        tabelaHtml.Append($"<a href=\"#linha-numero{lines[codigo][i]}\">{lines[codigo][i]}</a>");

                        if (i + 1 < lines[codigo].Count())
                        {
                            tabelaHtml.Append(',');
                        }
                    }

                    tabelaVazia = false;

                    tabelaHtml.Append($"</td></tr>");
                }
            }

            tabelaHtml.AppendLine("</table>");

            if (tabelaVazia)
            {
                htmlBuilder.AppendLine("<h3>Não foi encontrada nenhuma dependência!</h3>");
                htmlBuilder.AppendLine("</div>");
                return await Task.FromResult(htmlBuilder);
            }

            else
            {
                htmlBuilder.Append(tabelaHtml);
                htmlBuilder.AppendLine("</div>");
                return await Task.FromResult(htmlBuilder);
            }
        }

        static void ExibirCodigo(string[] linhasDeCodigo, StringBuilder htmlBuilder)
        {
            htmlBuilder.AppendLine("<div class=\"codigo-container\">"); // Adiciona uma div de contêiner

            htmlBuilder.AppendLine("<pre><code class=\"csharp\">");


            // Descobrir quantos dígitos tem o número da última linha para ajustar a formatação
            int numeroLinhas = linhasDeCodigo.Length;

            // Adicionar cada linha com o número da linha à esquerda
            for (int i = 0; i < numeroLinhas; i++)
            {
                // Adicionar a linha de código com o número da linha à esquerda
                htmlBuilder.AppendLine($"<div id=\"linha-numero{i + 1}\"><span>{i + 1}</span> {WebUtility.HtmlEncode(linhasDeCodigo[i])}</div>");

            }

            htmlBuilder.AppendLine("</code></pre>");
            htmlBuilder.AppendLine("</div>"); // Fecha a div de contêiner
        }

        static void modificarBackground(Dictionary<int, int> linhasVulneraveis, StringBuilder htmlBuilder)
        {
            foreach (var linha in linhasVulneraveis.Keys)
            {
                htmlBuilder.AppendLine($"modificarPadrao({linha},{linhasVulneraveis[linha]})");
            }
        }

        static bool EncontrouErrosSintaxe(StringBuilder htmlBuilder, string code)
        {

            SyntaxTree syntaxTree;

            try
            {
                syntaxTree = CSharpSyntaxTree.ParseText(code);
            }
            catch (Exception ex)
            {
                htmlBuilder.AppendLine($"<tr><td>1</td><td>{WebUtility.HtmlEncode(ex.Message)}</td></tr>");
                htmlBuilder.AppendLine("</table>");
                return false;
            }

            var diagnostics = syntaxTree.GetDiagnostics();

            if (diagnostics.Count() != 0)
            {
                return true;
            }

            else
            {
                return false;
            }

        }


        /*static void IdentificarPraticasDesempenho(StringBuilder htmlBuilder, string code)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        SyntaxNode root = tree.GetRoot();

        // Verificar loops desnecessariamente complexos
        IEnumerable<SyntaxNode> loopNodes = root.DescendantNodes()
            .Where(node => node.IsKind(SyntaxKind.ForStatement) || node.IsKind(SyntaxKind.WhileStatement));
        AdicionarRelatorio(htmlBuilder, "Loops desnecessariamente complexos identificados", loopNodes, tree);

        // Verificar alocações excessivas de memória
        IEnumerable<SyntaxNode> allocationNodes = root.DescendantNodes()
            .Where(node => node.IsKind(SyntaxKind.ArrayCreationExpression) ||
                            node.IsKind(SyntaxKind.ObjectCreationExpression) ||
                            (node.IsKind(SyntaxKind.GenericName) &&
                            ((GenericNameSyntax)node).TypeArgumentList?.Arguments.Any() == true));
        AdicionarRelatorio(htmlBuilder, "Possíveis alocações excessivas de memória identificadas", allocationNodes, tree);

        // Verificar uso excessivo de boxing e unboxing
        IEnumerable<SyntaxNode> boxingNodes = root.DescendantNodes()
            .Where(node => node.IsKind(SyntaxKind.CastExpression) ||
                            node.IsKind(SyntaxKind.AsExpression));
        AdicionarRelatorio(htmlBuilder, "Uso excessivo de boxing e unboxing identificado", boxingNodes, tree);

        // Verificar falha de otimização em consultas LINQ
        IEnumerable<SyntaxNode> linqQueryNodes = root.DescendantNodes()
            .Where(node => node.IsKind(SyntaxKind.QueryExpression) ||
                            node.IsKind(SyntaxKind.QueryContinuation));
        AdicionarRelatorio(htmlBuilder, "Possíveis falhas de otimização em consultas LINQ identificadas", linqQueryNodes, tree);

        // Verificar StringBuilder para manipulação de strings
        IEnumerable<SyntaxNode> stringBuilderNodes = root.DescendantNodes()
            .Where(node => node.IsKind(SyntaxKind.ObjectCreationExpression) &&
                            ((ObjectCreationExpressionSyntax)node).Type.ToString() == "StringBuilder");
        AdicionarRelatorio(htmlBuilder, "Uso de StringBuilder para manipulação de strings identificado", stringBuilderNodes, tree);

        // Verificar uso incorreto de cache
        IEnumerable<SyntaxNode> cacheUsageNodes = root.DescendantNodes()
    .Where(node => node.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
                    ((MemberAccessExpressionSyntax)node).Name.Identifier.Text == "Cache");

        AdicionarRelatorio(htmlBuilder, "Uso incorreto de cache identificado", cacheUsageNodes, tree);
    }*/

        /*static void AdicionarRelatorio(StringBuilder relatorio, string mensagem, IEnumerable<SyntaxNode> nodes, SyntaxTree tree)
    {
        if (nodes != null && nodes.Any())
        {
            relatorio.AppendLine($"<h3>{mensagem}</h3>");
            relatorio.AppendLine("<table>");
            relatorio.AppendLine("<tr><th>Código</th><th>Linha</th></tr>");

            StringBuilder tableContent = new StringBuilder();

            // Antes do loop foreach
            HashSet<int> linhasIncluidas = new HashSet<int>();

            foreach (SyntaxNode node in nodes)
            {
                var lineSpan = tree.GetLineSpan(node.Span);
                int linha = lineSpan.StartLinePosition.Line + 1;

                if (linhasIncluidas.Contains(linha))
                {
                    continue; // Pule esta linha se já estiver incluída na tabela
                }

                // Use o método GetNodeContentWithoutComments para obter o conteúdo sem comentários
                string codigoCompleto = GetNodeContentWithoutComments(node);

                // Definir um limite para o comprimento máximo do código exibido
                int comprimentoMaximo = 120; // ajusta conforme necessário

                // Se o código for muito grande, exibe apenas uma parte dele
                string codigoFormatado = codigoCompleto.Length > comprimentoMaximo
                    ? WebUtility.HtmlEncode(codigoCompleto.Substring(0, comprimentoMaximo) + "...")
                    : WebUtility.HtmlEncode(codigoCompleto);

                linhasIncluidas.Add(linha);

                tableContent.AppendLine($"<tr><td>{codigoFormatado}</td><td><a href=\"#linha-numero{linha}\" onclick=\"destacarLinha({linha})\">{linha}</a></td></tr>");
            }

            relatorio.Append(tableContent.ToString());
            relatorio.AppendLine("</table>");
        }
    }*/

        static void AnalisarExcecoes(StringBuilder relatorio, Dictionary<string, List<int>> lines)
        {
            StringBuilder tabelaHtml = new StringBuilder();
            bool tabelaVazia = true;

            // Iniciar a tabela HTML no relatório
            tabelaHtml.AppendLine("<table>");
            tabelaHtml.AppendLine("<tr><th>Nome da Exceção</th><th>Código</th><th>Linhas</th></tr>");

            // Iterar sobre cada código no dicionário
            foreach (var codigo in lines.Keys)
            {
                
                if (codigo.Contains("catch"))
                {
                    string[] partes = codigo.Split('(', ')');
                    string tipoExcecao = partes[1];

                    tabelaHtml.AppendLine("<tr>");
                    tabelaHtml.AppendLine($"<td>{tipoExcecao}</td>");
                    tabelaHtml.AppendLine($"<td>{codigo}</td>");
                    tabelaHtml.AppendLine("<td>");

                    // Iterar sobre cada linha onde a exceção é capturada
                    for (int i = 0; i < lines[codigo].Count(); i++)
                    {
                        tabelaHtml.Append($"<a href=\"#linha-numero{lines[codigo][i]}\">{lines[codigo][i]}</a>");

                        if (i + 1 < lines[codigo].Count())
                        {
                            tabelaHtml.Append(", ");
                        }
                    }

                    tabelaVazia = false;

                    // Fechar a tag 'td' após listar todas as linhas
                    tabelaHtml.AppendLine("</td>");
                    tabelaHtml.AppendLine("</tr>");
                }
            }

            tabelaHtml.AppendLine("</table>");

            if (tabelaVazia)
            {
                relatorio.AppendLine("<h3>Não foi encontrada nenhuma dependência exceção!</h3>");
            }
            else
            {
                relatorio.Append(tabelaHtml.ToString());
            }
        
        }

        static void VerificarRepeticao(StringBuilder htmlBuilder, Dictionary<string, List<int>> lines)
        {
            StringBuilder tabela = new StringBuilder();
            bool tabelaVazia = true;
            
            tabela.AppendLine("<table><tr><th>Código Repetido</th><th>Linhas</th></tr>");

            foreach(var chave in lines.Keys)
            {
                if (lines[chave].Count() > 1)
                {
                    tabela.AppendLine("<tr>");
                    tabela.Append($"<td>{lines[chave]}</td>");
                    for(int i = 0; i < lines[chave].Count();i++)
                    {
                        tabela.Append($"<td>{i}</td>");

                        if (i + 1 > lines[chave].Count())
                        {
                            tabela.Append(',');
                        }

                        tabelaVazia = false;
                    }
                }
            }

            if (tabelaVazia)
            {
                htmlBuilder.AppendLine("<h3>Não foi encontrado código repetido!</h3>");
            }
            
            else
            {
                htmlBuilder.AppendLine(tabela.ToString());
            }
        
        }

        static Dictionary<string, List<int>> VerificarRepeticao(IEnumerable<SyntaxNode> nodes)
    {
        var repetidos = new Dictionary<string, List<int>>();

        foreach (var node in nodes)
        {
            var nome = ObtenerNombre(node);

            if (!repetidos.ContainsKey(nome))
            {
                repetidos[nome] = new List<int>();
            }

            repetidos[nome].Add(node.GetLocation().GetMappedLineSpan().StartLinePosition.Line + 1);
        }

        // Remover entradas que não têm duplicatas
        repetidos = repetidos.Where(entry => entry.Value.Count > 1).ToDictionary(entry => entry.Key, entry => entry.Value);

        return repetidos;
    }

        static bool GerarTabelaHTML(StringBuilder htmlBuilder, string tipo, Dictionary<string, List<int>> repetidosPorLinha)
    {
        if (repetidosPorLinha.Count == 0)
        {
            return false;
        }

        htmlBuilder.AppendLine($"<h3>{tipo}</h3>");
        htmlBuilder.AppendLine("<table>");
        htmlBuilder.AppendLine("<tr><th>Nome</th><th>Linha(s) Encontrada(s)</th></tr>");

        foreach (var entry in repetidosPorLinha)
        {
            var nome = entry.Key;
            var linhas = entry.Value;

            var linkLinhas = linhas.Select(linha => $"<a href=\"#linha-numero{linha}\" onclick=\"destacarLinha({linha})\">{linha}</a>");

            htmlBuilder.AppendLine($"<tr><td>{nome}</td><td>{string.Join(", ", linkLinhas)}</td></tr>");
        }

        htmlBuilder.AppendLine("</table>");

        return true;
    }

        static string ObtenerNombre(SyntaxNode node)
    {
        // Lógica para obtener el nombre según el tipo de nodo
        if (node is MethodDeclarationSyntax methodSyntax)
        {
            return methodSyntax.Identifier.ValueText;
        }
        else if (node is VariableDeclarationSyntax variableSyntax)
        {
            return variableSyntax.Variables.FirstOrDefault()?.Identifier.ValueText;
        }
        else if (node is ClassDeclarationSyntax classSyntax)
        {
            return classSyntax.Identifier.ValueText;
        }

        return string.Empty;
    }

        static void AnalisarConcorrencia(StringBuilder htmlBuilder, string code)
    {
        ConcurrencyAnalyzer concurrencyAnalyzer = new ConcurrencyAnalyzer();
        List<DependencyInfo> dependencies = concurrencyAnalyzer.AnalyzeConcurrencyIssues(code);

        if (dependencies.Count != 0)
        {
            // Adicionar cabeçalhos da tabela
            htmlBuilder.AppendLine("<table><tr><th>Nome da Concorrência</th><th>Linha do Código</th></tr>");

            foreach (var dependency in dependencies)
            {
                // Adicionar uma linha na tabela para cada dependência
                htmlBuilder.AppendLine("<tr>");

                // Coluna 1: Nome da Concorrência
                htmlBuilder.AppendLine($"<td>{dependency.DependencyType}</td>");

                // Coluna 2: Linha do Código
                htmlBuilder.AppendLine($"<td><a href=\"#linha-numero{dependency.LineNumber}\" onclick=\"destacarLinha({dependency.LineNumber})\">{dependency.LineNumber}</a></td>");

                // Fechar a linha na tabela
                htmlBuilder.AppendLine("</tr>");
            }

            // Fechar a tabela HTML
            htmlBuilder.AppendLine("</table>");
        }
        else
        {
            htmlBuilder.AppendLine("<h3>Não foi encontrada nenhuma concorrência.</h3>");
        }
    }
    }
}