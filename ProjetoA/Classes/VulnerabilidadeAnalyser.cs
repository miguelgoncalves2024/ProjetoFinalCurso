﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Windows.Storage;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Media.Animation;

public class Vulnerability
{
    public string Tipo { get; set; }
    public string Codigo { get; set; }
    public NivelRisco Risco { get; set; }
    public HashSet<int> Linhas { get; set; }

    public Vulnerability(string type, string node, NivelRisco riskLevel, HashSet<int> lineNumbers)
    {
        Tipo = type;
        Codigo = node;
        Risco = riskLevel;
        Linhas = lineNumbers;
    }
}

public enum NivelRisco
{
    Alto,
    Medio,
    Baixo
}

public static class VulnerabilidadeAnalyzer
{
    static List<Vulnerability> vulnerabilities;

    public static List<Vulnerability> AnalisarVulnerabilidades(SyntaxNode root)
    {
        vulnerabilities = new List<Vulnerability>();

        /*var compilation = CSharpCompilation.Create("MyCompilation")
                                            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                                            .AddSyntaxTrees(tree);*/

        //var semanticModel = compilation.GetSemanticModel(tree);

        // Analisar vulnerabilidades de XSS
        AnalyzeForSQLInjection(root);

        // Analisar vulnerabilidades de SQL Injection
        //var sqlVulnerabilities = AnalyzeSQLInjection(root);
        //vulnerabilities.AddRange(sqlVulnerabilities);

        return vulnerabilities; 
    }

    private static SyntaxNode GetScope(SyntaxNode node)
    {
        while (node != null && !(node is MethodDeclarationSyntax || node is ConstructorDeclarationSyntax || node is ClassDeclarationSyntax))
        {
            node = node.Parent;
        }
        return node;
    }

    static void PrepararParaAdiconarVulnerabilidade(SyntaxNode node,string tipo,NivelRisco risco)
    {
        char[] mudanca = new char[] { ';', '\n', '\r' };
        int linha = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        int index = node.ToString().IndexOfAny(mudanca);

        string codigo;

        try
        {
            codigo = node.ToString().Substring(0, index);
        }

        catch (ArgumentOutOfRangeException)
        {
            codigo = node.ToString();
        }

        AdicionarVulnerabilidade(tipo, codigo, risco, linha);
    }
    static void AdicionarVulnerabilidade(string tipo, string codigo, NivelRisco risco, int linha)
    {
        object obj = new object();

        var index = vulnerabilities.IndexOf(
            vulnerabilities.FirstOrDefault(v => v.Codigo == codigo && v.Tipo == tipo));
                       

        lock (obj)
        {
            if (index > -1)
            {
                // Adicionar a nova linha à lista de linhas da vulnerabilidade existente

                vulnerabilities[index].Linhas.Add(linha);

            }
            
            else
            {
                // Adicionar nova vulnerabilidade
                var lineNumbers = new HashSet<int> { linha };
                vulnerabilities.Add(new Vulnerability(tipo, codigo, risco, lineNumbers));
            }
        }
    }

    static void AnalyzeForSQLInjection(SyntaxNode root)
    {
        var tipo = "SQL Injection";
        var risco = NivelRisco.Alto;

        string[] sqlReservedKeywords = new string[]
        {
        "select",
        "from",
        "where",
        "values",
        "update",
        "set",
        "delete",
        "create",
        "alter",
        "drop",
        "join",
        "group by",
        "having",
        "order by",
        "distinct",
        };

        var expressions = root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                            .Where(v => v.Right is BinaryExpressionSyntax binaryExpression &&
                                        binaryExpression.IsKind(SyntaxKind.AddExpression));

        foreach (var exp in expressions)
        {
            var binaryExpression = (BinaryExpressionSyntax)exp.Right;

            string expressionText = binaryExpression.ToString();
            int keywordCount = 0;

            foreach (var keyword in sqlReservedKeywords)
            {
                if (expressionText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    keywordCount++;
                }
            }

            if (keywordCount >= 2)
            {
                //var codigo = variable.ToString();
                //var linha = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                PrepararParaAdiconarVulnerabilidade(exp, tipo, risco);

            }
        }
    }
    static void AnalyzeForXSS(SyntaxNode root)
    {
        var tipo = "XSS";
        var risco = NivelRisco.Alto; // Ajuste conforme necessário

        // Encontrar variáveis inicializadas com Request.QueryString, Request.Form, Request.Params
        var variaveis = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                            .Where(v => v.Initializer != null &&
                                        (v.Initializer.Value.ToString().Contains("Request.QueryString") ||
                                         v.Initializer.Value.ToString().Contains("Request.Form") ||
                                         v.Initializer.Value.ToString().Contains("Request.Params") ||
                                         v.Initializer.Value.ToString().Contains(".Text")));

        // Verificar se existe pelo menos uma variável encontrada
        if (!variaveis.Any())
        {
            return;
        }

        // Encontrar todas as chamadas para HttpUtility.HtmlEncode ou Server.HtmlEncode
        var codificacoes = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Where(i => i.Expression.ToString().Contains("HttpUtility.HtmlEncode") ||
                                        i.Expression.ToString().Contains("Server.HtmlEncode"));

        // Verificar se cada variável encontrada é codificada antes de ser usada em uma saída HTML
        foreach (var v in variaveis)
        {
            bool isEncoded = false;

            foreach (var codificacao in codificacoes)
            {
                //var scope = GetScope(codificacao);

                var parent = codificacao.Parent;

                while (parent != null)
                {
                    if (parent.Contains(v))
                    {
                        isEncoded = codificacao.ArgumentList.Arguments
                                .Any(arg => arg.ToString().Contains(v.Identifier.Text));
                        if (isEncoded)
                        {

                            break;
                        }
                    }
                }

                if (parent!=null)
                {
                    break;
                }

            }

            if (!isEncoded)
            {
                PrepararParaAdiconarVulnerabilidade(v.Parent, tipo, risco);
            }
        }
    }
    static void AnalyzeForCSRF(SyntaxNode root)
    {
        var tipo = "CSRF";
        var risco = NivelRisco.Alto;

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach(var m in methods)
        {
            var listaAtributos = m.AttributeLists;

            int httpPost = 0;
            int antiForgery = 0;

            foreach (var a in listaAtributos)
            {
                if(a.ToString() == "[HttpPost]")
                {
                    httpPost++;
                }

                else if(a.ToString() == "[ValidateAntiForgeryToken]")
                {
                    antiForgery++;
                }
            }

            if(httpPost > antiForgery)
            {
                PrepararParaAdiconarVulnerabilidade(m, tipo, risco);
            }
        }


    }
    static void AnalyzeForInsecureDeserialization(SyntaxNode root) 
    {
        var tipo = "Deserialização Insegura";
        var risco = NivelRisco.Alto;

        IEnumerable<VariableDeclaratorSyntax> variaveis;
        IEnumerable<InvocationExpressionSyntax> incovations;
        
        variaveis = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
        .Where(i => i.Initializer.ToString().Contains("BinaryFormatter"));

        foreach (var v in variaveis)
        {
            incovations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString().Contains(v.Identifier + ".Deserialize"));

            foreach (var i in incovations)
            {
                PrepararParaAdiconarVulnerabilidade(i.Parent, tipo, risco);
            }
       
        }
            
    }
    static void AnalyzeForInsecureRedirects(SyntaxNode root) 
    {
        var tipo = "Deserialização Insegura";
        var risco = NivelRisco.Alto;

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                  .Where(m => m.Expression.ToString().Contains("Redirect"));

        foreach (var inv in invocations)
        {
            var argumentList = inv.ArgumentList.Arguments;
            if (argumentList.Count > 0)
            {
                var firstArgument = argumentList[0].ToString();

                // Verificar se o argumento é uma string literal
                if (firstArgument.StartsWith("\"") && firstArgument.EndsWith("\""))
                {
                    PrepararParaAdiconarVulnerabilidade(inv.Parent, tipo, risco);
                }
                
                else
                {
                    // Verificar se existe um if statement com Url.IsLocalUrl no mesmo escopo
                    //var parentScope = GetScope(inv);

                    var parentScope = inv.Parent;

                    while (parentScope != null)
                    {
                        if (parentScope.DescendantNodes().OfType<IfStatementSyntax>()
                            .Any(ifStmt => ifStmt.Condition.ToString()
                            .Contains($"Url.IsLocalUrl({firstArgument})")))
                        {
                            PrepararParaAdiconarVulnerabilidade(inv.Parent, tipo, risco);

                            break;
                        }

                        parentScope = parentScope.Parent;
                    }

                 
                }
            }
        }

        // Exibir vulnerabilidades encontradas

    }

    public static void AnalyzeForNoSQLInjection(SyntaxNode root)
    {
        var tipo = "NoSQL Injection";
        var risco = NivelRisco.Alto;

        // Procura por invocações de método
        var methodInvocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                             .Where(m => m.Expression.ToString().Contains(".Find") &&
                                    m.ArgumentList.Arguments.Count() > 0);

        foreach (var method in methodInvocations)
        {
            // Nome do objeto que chama aquele método
            int index = method.Expression.ToString().IndexOf(".");
            string nome = method.Expression.ToString().Substring(0, index);
            var parent = method.Parent;
            dynamic variavel = null;

            // Procura a definição da variável a partir deste nó para cima
            while (parent != null)
            {
                variavel = parent.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(v => v.Identifier.ToString() == nome);

                if (variavel != null)
                {
                    // Verifica se a variável tem o data type igual a IMongoCollection<BsonDocument> ou IMongoCollection<JsonDocument>
                    if (variavel.As<dynamic>().Initializer?.Value.ToString().Contains("GetCollection<BsonDocument>") ?? false)
                    {
                        // Se o data type for IMongoCollection<BsonDocument>, faz algo...
                        break;
                    }
                    else if (variavel.As<dynamic>().Initializer?.Value.ToString().Contains("GetCollection<JsonDocument>") ?? false)
                    {
                        // Se o data type for IMongoCollection<JsonDocument>, faz algo...
                        break;
                    }
                }

                parent = parent.Parent;
            }

            if (variavel != null)
            {
                var argumentos = method.ArgumentList.Arguments;

                foreach (var arg in argumentos)
                {
                    // Se o argumento contiver a seguinte expressão: Builders<BsonDocument>.Filter ou Builders<JsonDocument>.Filter
                    // Ou se o argumento for identifcador de nome para uma variavel que tenha esse valor, chama o método PrepararParaAdicionarVulnerabilidade
                    if (arg.Expression.ToString().Contains("Builders<BsonDocument>.Filter") ||
                        arg.Expression.ToString().Contains("Builders<JsonDocument>.Filter"))
                    {
                        PrepararParaAdiconarVulnerabilidade(method, tipo, risco);
                    }
                    else if (arg.Expression is IdentifierNameSyntax identifierName)
                    {
                        // Procura a definição da variável a partir deste nó para cima
                        parent = method.Parent;

                        while (parent != null)
                        {
                            variavel = parent.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                                .FirstOrDefault(v => v.Identifier.ToString() == identifierName.Identifier.ToString());

                            if (variavel != null)
                            {
                                // Verifica se a variável tem o valor esperado para a vulnerabilidade e chama o método adequado
                                if (variavel.As<dynamic>().Initializer?.Value.ToString().Contains("Builders<BsonDocument>.Filter") ?? false ||
                                    variavel.As<dynamic>().Initializer?.Value.ToString().Contains("Builders<JsonDocument>.Filter") ?? false)
                                {
                                    PrepararParaAdiconarVulnerabilidade(method, tipo, risco);
                                    break;
                                }
                            }

                            parent = parent.Parent;
                        }
                    }
                }
            }
        }
    }

    static void AnalyzeForInsecureEncryption(SyntaxNode root) { }
    static void AnalyzeForLDAPInjection(SyntaxNode root) { }

}