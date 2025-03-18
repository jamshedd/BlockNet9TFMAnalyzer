using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BlockNet9TFMAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BlockNet9TFMAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "BlockNet9TFMAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
        private static readonly DiagnosticDescriptor TargetFrameworkRule = new DiagnosticDescriptor("TF001", "Target Framework Error", "Target framework version .NET 9 is not supported.", "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule, TargetFrameworkRule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.LocalDeclarationStatement);
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var localDeclaration = (LocalDeclarationStatementSyntax)context.Node;

            if (localDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                return;
            }

            DataFlowAnalysis dataFlowAnalysis = context.SemanticModel.AnalyzeDataFlow(localDeclaration);

            VariableDeclaratorSyntax variable = localDeclaration.Declaration.Variables.Single();
            ISymbol variableSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
            if (dataFlowAnalysis.WrittenOutside.Contains(variableSymbol))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), localDeclaration.Declaration.Variables.First().Identifier.ValueText));
        }

        private void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var targetFrameworkAttribute = context.Compilation.Assembly.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.Name == "TargetFrameworkAttribute");

            if (targetFrameworkAttribute != null)
            {
                var frameworkName = targetFrameworkAttribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (frameworkName != null && frameworkName.Contains(".NETCoreApp,Version=v9.0"))
                {
                    var diagnostic = Diagnostic.Create(TargetFrameworkRule, Location.None);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
