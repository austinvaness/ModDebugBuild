using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Scripting;
using VRage.Utils;

namespace avaness.ModDebugBuild
{
    public class MyDebugScriptCompiler
    {
		MyScriptCompiler instance;

		CSharpCompilationOptions m_debugCompilationOptions;
		CSharpParseOptions m_conditionalParseOptions;

		private static FieldInfo field_m_conditionalCompilationSymbols;
		HashSetReader<string> m_conditionalCompilationSymbols => (HashSet<string>)field_m_conditionalCompilationSymbols.GetValue(instance);

		private static FieldInfo field_m_ignoredWarnings;
		HashSet<string> m_ignoredWarnings => (HashSet<string>)field_m_ignoredWarnings.GetValue(instance);

		private static FieldInfo field_m_modApiWhitelistDiagnosticAnalyzer;
		DiagnosticAnalyzer m_modApiWhitelistDiagnosticAnalyzer => (DiagnosticAnalyzer)field_m_modApiWhitelistDiagnosticAnalyzer.GetValue(instance);

		private static FieldInfo field_m_metadataReferences;
		List<MetadataReference> m_metadataReferences => (List<MetadataReference>)field_m_metadataReferences.GetValue(instance);


		public static void Init()
        {
			Type t = typeof(MyScriptCompiler);
			field_m_conditionalCompilationSymbols = t.GetField(nameof(m_conditionalCompilationSymbols), BindingFlags.Instance | BindingFlags.NonPublic);
			field_m_ignoredWarnings = t.GetField(nameof(m_ignoredWarnings), BindingFlags.Instance | BindingFlags.NonPublic);
			field_m_modApiWhitelistDiagnosticAnalyzer = t.GetField(nameof(m_modApiWhitelistDiagnosticAnalyzer), BindingFlags.Instance | BindingFlags.NonPublic);
			field_m_metadataReferences = t.GetField(nameof(m_metadataReferences), BindingFlags.Instance | BindingFlags.NonPublic);

		}

		public MyDebugScriptCompiler(MyScriptCompiler instance)
        {
			this.instance = instance;

			if(field_m_conditionalCompilationSymbols == null)
				Init();

			m_debugCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, reportSuppressedDiagnostics: false, null, null, null, null, OptimizationLevel.Debug, checkOverflow: false, allowUnsafe: false, null, null, default(ImmutableArray<byte>), null, Platform.X64);
			m_conditionalParseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.None);
		}

		public async Task<Assembly> Compile(MyApiTarget target, string assemblyName, IEnumerable<Script> scripts, List<Message> messages, string friendlyName)
		{
			if (friendlyName == null)
				friendlyName = "<No Name>";

			DiagnosticAnalyzer whitelistAnalyzer = m_modApiWhitelistDiagnosticAnalyzer;
			IEnumerable<EmbeddedText> texts;
			CSharpCompilation compilation = CreateCompilation(assemblyName, scripts, out texts);

			CSharpCompilation compilationWithoutInjection = compilation;
			bool injectionFailed = false;

			CompilationWithAnalyzers analyticCompilation = null;
			if (whitelistAnalyzer != null)
			{
				analyticCompilation = compilation.WithAnalyzers(ImmutableArray.Create(whitelistAnalyzer));
				compilation = (CSharpCompilation)analyticCompilation.Compilation;
			}
			using (MemoryStream pdbStream = new MemoryStream())
			{
				using (MemoryStream assemblyStream = new MemoryStream())
				{
					EmitResult emitResult = compilation.Emit(assemblyStream, pdbStream, 
						embeddedTexts: texts,
						options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: Path.ChangeExtension(assemblyName, "pdb")));
					bool emitSuccess = emitResult.Success;

					MyBlacklistSyntaxVisitor myBlacklistSyntaxVisitor = new MyBlacklistSyntaxVisitor();
					ImmutableArray<SyntaxTree>.Enumerator enumerator = compilation.SyntaxTrees.GetEnumerator();
					while (enumerator.MoveNext())
					{
						SyntaxTree current = enumerator.Current;
						myBlacklistSyntaxVisitor.SetSemanticModel(compilation.GetSemanticModel(current));
						myBlacklistSyntaxVisitor.Visit(current.GetRoot());
					}
					if (myBlacklistSyntaxVisitor.HasAnyResult())
					{
						myBlacklistSyntaxVisitor.GetResultMessages(messages);
						return null;
					}

					emitSuccess = await EmitDiagnostics(analyticCompilation, emitResult, messages, emitSuccess).ConfigureAwait(continueOnCapturedContext: false);

					pdbStream.Seek(0L, SeekOrigin.Begin);
					assemblyStream.Seek(0L, SeekOrigin.Begin);
					if (!injectionFailed)
					{
						if (emitSuccess)
							return Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());

						emitResult = compilationWithoutInjection.Emit(assemblyStream);
						await EmitDiagnostics(analyticCompilation, emitResult, messages, success: false).ConfigureAwait(continueOnCapturedContext: false);
					}
					return null;
				}
			}
		}

		private CSharpCompilation CreateCompilation(string assemblyFileName, IEnumerable<Script> scripts, out IEnumerable<EmbeddedText> embeddedexts)
		{
			List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
			List<EmbeddedText> texts = new List<EmbeddedText>();
			embeddedexts = texts;
			if (scripts != null)
			{
				CSharpParseOptions parseOptions = m_conditionalParseOptions.WithPreprocessorSymbols(m_conditionalCompilationSymbols);
				foreach(Script s in scripts)
				{
					if (!File.Exists(s.Name))
						throw new FileNotFoundException(s.Name + " not found!");

					using (Stream stream = File.OpenRead(s.Name))
					{
						if(s.Name.Contains("settingschat.cs", StringComparison.OrdinalIgnoreCase))
                        {
							File.WriteAllText(@"C:\Users\austi\Desktop\SettingsChat.cs", s.Code);
                        }
						SourceText source = SourceText.From(stream, canBeEmbedded: true);
						texts.Add(EmbeddedText.FromSource(s.Name, source));
						syntaxTrees.Add(CSharpSyntaxTree.ParseText(source, parseOptions, s.Name));
					}
				}
			}
			return CSharpCompilation.Create(MakeAssemblyName(assemblyFileName), syntaxTrees, m_metadataReferences, m_debugCompilationOptions);
		}

		private static string MakeAssemblyName(string name)
		{
			if (name == null)
			{
				return "scripts.dll";
			}
			return Path.GetFileName(name);
		}

		private async Task<bool> EmitDiagnostics(CompilationWithAnalyzers analyticCompilation, EmitResult result, List<Message> messages, bool success)
		{
			messages.Clear();
			if (analyticCompilation != null)
			{
				AnalyzeDiagnostics(await analyticCompilation.GetAllDiagnosticsAsync().ConfigureAwait(continueOnCapturedContext: false), messages, ref success);
			}
			else
			{
				AnalyzeDiagnostics(result.Diagnostics, messages, ref success);
			}
			return success;
		}

		/// <summary>
		///     Analyzes the given diagnostics and places errors and warnings in the messages lists.
		/// </summary>
		/// <param name="diagnostics"></param>
		/// <param name="messages"></param>
		/// <param name="success"></param>
		private void AnalyzeDiagnostics(ImmutableArray<Diagnostic> diagnostics, List<Message> messages, ref bool success)
		{
			success = success && !diagnostics.Any((Diagnostic d) => d.Severity == DiagnosticSeverity.Error);
			foreach (Diagnostic item in from d in diagnostics
										where d.Severity >= DiagnosticSeverity.Warning
										orderby d.Severity descending
										select d)
			{
				if (item.Severity != DiagnosticSeverity.Warning || (success && !m_ignoredWarnings.Contains(item.Id)))
				{
					FileLinePositionSpan mappedLineSpan = item.Location.GetMappedLineSpan();
					string text = $"{mappedLineSpan.Path}({mappedLineSpan.StartLinePosition.Line + 1},{mappedLineSpan.StartLinePosition.Character}): {item.Severity}: {item.GetMessage()}";
					messages.Add(new Message(item.Severity == DiagnosticSeverity.Error, text));
				}
			}
		}
	}
}
