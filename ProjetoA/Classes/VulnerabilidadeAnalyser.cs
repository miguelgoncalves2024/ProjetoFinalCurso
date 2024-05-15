﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ProjetoA.Classes;
using System.Collections.Concurrent;
using System.Text;
using System.Reflection;
using System.Data.SqlClient;

namespace ProjetoA.Analyzers
{
    internal enum NivelRisco
    {
        Alto,
        Medio,
        Baixo
    }

    internal class Vulnerability
    {
        public string Tipo { get; }
        public string Codigo { get; }
        public List<int> Linhas { get; }
        public NivelRisco Risco { get; }

        public Vulnerability(string type, string code, NivelRisco riskLevel, List<int> linhas)
        {
            Tipo = type;
            Codigo = code;
            Risco = riskLevel;
            Linhas = linhas;
        }
    }

    internal static class VulnerabilityAnalyzer
    {
        private static readonly object _lock = new object();
        /*delegate Task Analyze(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model);*/
        static List<Vulnerability> Vulnerabilidades = new List<Vulnerability>();

        //static Analyze[] analises;

        public static async Task<List<Vulnerability>> AnalyzeVulnerabilities(SyntaxTree tree)
        {
            var vulnerabilities = new List<Vulnerability>();

            /* analises = new Analyze[] {
                 AnalyzeSqlInjection,
                 AnalyzeXss,
                 AnalyzeXxe,
                 AnalyzeInsecureDeserialization,
                 AnalyzeRemoteCodeExecution,
                 AnalyzeNoSqlInjection,
                 AnalyzeCsrf,
                 AnalyzeEncryption,
                 AnalyzeArbitraryFileWrites,
                 AnalyzeDirectoryTraversal
             };*/

            /*var compilation = CSharpCompilation.Create("MyCompilation", new[] { tree }, new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            });

            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();*/

            List<Task> tasks = new List<Task>();

            AnalyzeSqlInjection(tree.GetRoot());

            /*foreach (var a in analises)
            {
                tasks.Add(a(assignments, model));
            }*/

            //await Task.WhenAll(tasks);

            return vulnerabilities;
        }

        public static void AnalyzeSqlInjection(SyntaxNode root)
        {
            // Procura por strings concatenadas em comandos SQL
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                if (memberAccess == null) continue;

                var identifier = memberAccess.Name.Identifier.Text;
                if (identifier.Contains("Execute") || identifier.Contains("Query"))
                {
                    var argumentList = invocation.ArgumentList.Arguments;
                    foreach (var argument in argumentList)
                    {
                        if (argument.Expression is BinaryExpressionSyntax binaryExpression &&
                            binaryExpression.IsKind(SyntaxKind.AddExpression))
                        {
                            var riskLevel = DetermineRiskLevel(argument.Expression);
                            var lineSpan = binaryExpression.SyntaxTree.GetLineSpan(binaryExpression.Span);
                            var lines = new List<int> { lineSpan.StartLinePosition.Line + 1 };

                            Vulnerabilidades.Add(new Vulnerability(
                                "SQL Injection",
                                binaryExpression.ToString(),
                                riskLevel,
                                lines));
                        }
                    }
                }
                else if (invocation.ArgumentList != null)
                {
                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        if (argument.Expression is IdentifierNameSyntax identifierName)
                        {
                            var riskLevel = DetermineRiskLevel(argument.Expression);
                            var lineSpan = identifierName.SyntaxTree.GetLineSpan(identifierName.Span);
                            var lines = new List<int> { lineSpan.StartLinePosition.Line + 1 };

                            if (riskLevel != NivelRisco.Baixo)
                            {
                                Vulnerabilidades.Add(new Vulnerability(
                                    "SQL Injection",
                                    identifierName.ToString(),
                                    riskLevel,
                                    lines));
                            }
                        }
                    }
                }
            }
        }

        private static NivelRisco DetermineRiskLevel(ExpressionSyntax expression)
        {
            // Obtém a representação da expressão em texto
            var expressionString = expression.ToString().ToLower();

            // Verifica se a expressão sugere uma possível vulnerabilidade
            if (expressionString.Contains("password") || expressionString.Contains("secret") || expressionString.Contains("auth"))
            {
                // Se a expressão contiver palavras-chave comuns relacionadas a entrada de usuário,
                // consideramos um risco alto
                return NivelRisco.Alto;
            }
            else if (expressionString.Contains("userinput") || expressionString.Contains("input") || expressionString.Contains("request"))
            {
                // Se a expressão contiver palavras-chave relacionadas a senhas ou autenticação,
                // consideramos um risco médio
                return NivelRisco.Medio;
            }

            // Se não houver indícios de vulnerabilidade, consideramos um risco baixo
            return NivelRisco.Baixo;
        }

        private static void AnalyzeSqlInjection(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
        {
            //await Task.Run(() =>
            //{
            foreach (var assignment in assignments)
            {
                var right = assignment.Right;
                var left = assignment.Left;

                var symbol = model.GetSymbolInfo(left).Symbol as ILocalSymbol;
                if (symbol == null)
                    continue;

                var type = model.GetTypeInfo(right).Type;
                if (type == null || type.SpecialType != SpecialType.System_String)
                    continue;

                var dataFlowAnalysis = model.AnalyzeDataFlow(assignment);
                if (dataFlowAnalysis == null)
                    continue;

                var usedSymbols = dataFlowAnalysis.ReadInside.Union(dataFlowAnalysis.WrittenInside, SymbolEqualityComparer.Default);

                bool isUserInput = usedSymbols.Any(s => s.Kind == SymbolKind.Parameter || (s.Kind == SymbolKind.Local && s.Name.Contains("input")));

                if (isUserInput)
                {
                    NivelRisco riskLevel = NivelRisco.Baixo;

                    var sqlMethods = new[] { "ExecuteQuery", "ExecuteNonQuery", "ExecuteScalar" };

                    var parentInvocation = assignment.AncestorsAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .FirstOrDefault(invocation =>
                        {
                            var methodSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                            return methodSymbol != null && sqlMethods.Contains(methodSymbol.Name);
                        });

                    if (parentInvocation != null)
                    {
                        riskLevel = NivelRisco.Alto;
                    }
                    else
                    {
                        riskLevel = NivelRisco.Medio;
                    }

                    var lines = new List<int> { assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1 };
                    var vulnerability = new Vulnerability("SQL Injection", assignment.ToString(), riskLevel, lines);

                    lock (_lock)
                    {
                        Vulnerabilidades.Add(vulnerability);
                    }
                }
            }
            //});
        }

        /*    private static bool ContainsUserInput(SyntaxNode node, SemanticModel model)
            {
                if (node is LiteralExpressionSyntax)
                {
                    return false;
                }

                if (node is IdentifierNameSyntax identifier)
                {
                    var symbol = model.GetSymbolInfo(identifier).Symbol as ILocalSymbol;
                    if (symbol != null && symbol.Type.SpecialType == SpecialType.System_String)
                    {
                        return true;
                    }
                }

                foreach (var child in node.ChildNodes())
                {
                    if (ContainsUserInput(child, model))
                    {
                        return true;
                    }
                }

                return false;
            }
            private static NivelRisco AvaliarNivelDeRiscoSQLInjection(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("SELECT") || codigo.Contains("INSERT") || codigo.Contains("UPDATE") || codigo.Contains("DELETE"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("WHERE") || codigo.Contains("AND") || codigo.Contains("OR"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeXss(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.Web.UI.WebControls.Literal.Text"))
                            {
                                if (assignment.Right is LiteralExpressionSyntax)
                                {
                                    var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "XSS" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            // Determinando o nível de risco
                                            var nivelRisco = DetermineNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("XSS", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("<script>"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("javascript:"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("http://"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeXxe(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.Xml.XmlDocument"))
                            {
                                if (assignment.Right is ObjectCreationExpressionSyntax)
                                {
                                    var lineNumber = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "XXE" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            var nivelRisco = DetermineXxeRiskLevel(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("XXE", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineXxeRiskLevel(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("LoadXml("))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("XmlDocument"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("Parse("))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeInsecureDeserialization(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.Runtime.Serialization.Json.DataContractJsonSerializer") ||
                                symbolInfo.Symbol.ToString().StartsWith("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter"))
                            {
                                if (assignment.Right is ObjectCreationExpressionSyntax)
                                {
                                    var lineNumber = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "Insecure Deserialization" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            // Determinando o nível de risco
                                            var nivelRisco = DetermineInsecureDeserializationNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("Insecure Deserialization", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineInsecureDeserializationNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("Deserialize"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("DeserializeObject"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("FromBase64String"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeRemoteCodeExecution(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            // Aqui, você precisa adaptar a lógica para identificar atribuições que podem resultar em execução remota de código.
                            // Isso pode incluir chamadas a APIs perigosas, execução de comandos do sistema, entre outros.
                            if (assignment.Right.ToString().Contains("System.Diagnostics.Process.Start(") || assignment.Right.ToString().Contains("exec("))
                            {
                                var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                lock (_lock)
                                {
                                    var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "Remote Code Execution" && v.Codigo == assignment.ToString());
                                    if (existingVulnerability != null)
                                    {
                                        existingVulnerability.Linhas.Add(lineNumber);
                                    }
                                    else
                                    {
                                        var nivelRisco = DetermineRemoteCodeExecutionNivelRisco(assignment.Right.ToString());
                                        Vulnerabilidades.Add(new Vulnerability("Remote Code Execution", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineRemoteCodeExecutionNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("System.Diagnostics.Process.Start("))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("exec("))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeNoSqlInjection(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("NoSqlDatabase.Query"))
                            {
                                if (assignment.Right is LiteralExpressionSyntax)
                                {
                                    var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "NoSQL Injection" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            // Determinando o nível de risco
                                            var nivelRisco = DetermineNoSqlInjectionNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("NoSQL Injection", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineNoSqlInjectionNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("$ne") || codigo.Contains("$regex"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("$gt") || codigo.Contains("$lt") || codigo.Contains("$or") || codigo.Contains("$and"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeCsrf(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.Web.UI.WebControls."))
                            {
                                if (assignment.Right is LiteralExpressionSyntax)
                                {
                                    var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "CSRF" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            // Determinando o nível de risco
                                            var nivelRisco = DetermineNivelRiscoCsrf(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("CSRF", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineNivelRiscoCsrf(string codigo)
            {
                // Implemente suas regras de determinação de risco para CSRF aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("ValidateAntiForgeryToken"))
                {
                    return NivelRisco.Baixo;
                }
                else if (codigo.Contains("Request.UrlReferrer"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Alto;
                }
            }

            private static async Task AnalyzeEncryption(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.Security.Cryptography"))
                            {
                                if (assignment.Right is ObjectCreationExpressionSyntax)
                                {
                                    var lineNumber = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "Encryption" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            var nivelRisco = DetermineEncryptionNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("Encryption", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineEncryptionNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("DES") || codigo.Contains("MD5"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("RC4") || codigo.Contains("SHA-1"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeArbitraryFileWrites(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            // Aqui você deve verificar se a atribuição está relacionada a operações de gravação de arquivo
                            if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
                                methodSymbol.ContainingType.Name == "File" &&
                               (methodSymbol.Name.StartsWith("Write") || methodSymbol.Name.StartsWith("Append")))
                            {
                                if (assignment.Right is LiteralExpressionSyntax)
                                {
                                    var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "ArbitraryFileWrites" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            var nivelRisco = DetermineArbitraryFileWritesNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("ArbitraryFileWrites", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineArbitraryFileWritesNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("File.WriteAll") || codigo.Contains("File.WriteAllText"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("File.AppendAllText") || codigo.Contains("File.AppendAllLines"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

            private static async Task AnalyzeDirectoryTraversal(IEnumerable<AssignmentExpressionSyntax> assignments, SemanticModel model)
            {
                await Task.Run(() =>
                {
                    foreach (var assignment in assignments)
                    {
                        var symbolInfo = model.GetSymbolInfo(assignment.Left);
                        if (symbolInfo.Symbol != null)
                        {
                            if (symbolInfo.Symbol.ToString().StartsWith("System.IO.Path"))
                            {
                                if (assignment.Right is LiteralExpressionSyntax)
                                {
                                    var lineNumber = ((LiteralExpressionSyntax)assignment.Right).GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                    lock (_lock)
                                    {
                                        var existingVulnerability = Vulnerabilidades.FirstOrDefault(v => v.Tipo == "Directory Traversal" && v.Codigo == assignment.ToString());
                                        if (existingVulnerability != null)
                                        {
                                            existingVulnerability.Linhas.Add(lineNumber);
                                        }
                                        else
                                        {
                                            // Determinando o nível de risco
                                            var nivelRisco = DetermineDirectoryTraversalNivelRisco(assignment.Right.ToString());
                                            Vulnerabilidades.Add(new Vulnerability("Directory Traversal", assignment.ToString(), nivelRisco, new List<int> { lineNumber }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            private static NivelRisco DetermineDirectoryTraversalNivelRisco(string codigo)
            {
                // Implemente suas regras de determinação de risco aqui.
                // Este é apenas um exemplo simplificado.
                if (codigo.Contains("..\\") || codigo.Contains("../"))
                {
                    return NivelRisco.Alto;
                }
                else if (codigo.Contains("\\") || codigo.Contains("/"))
                {
                    return NivelRisco.Medio;
                }
                else
                {
                    return NivelRisco.Baixo;
                }
            }

        }*/
    }
}