using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace TcpServerApp.Services
{
    /// <summary>
    /// Professional Code Generation Service – NO SIMULATION.
    /// Uses Google Gemini Pro to generate real-world Android 16 research code.
    /// </summary>
    public class CodeGenerationService
    {
        private readonly string _outputPath;
        private GeminiService? _geminiService;
        
        public CodeGenerationService(string outputPath = null)
        {
            _outputPath = outputPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TcpServerApp", "GeneratedApps");
            Directory.CreateDirectory(_outputPath);
        }

        public void SetGeminiService(GeminiService gemini) => _geminiService = gemini;
        
        public async Task<string> GenerateAndroid16ActivityAsync(string className, string packageName, string specificRequirements = "")
        {
            if (_geminiService == null) return "❌ [PIPELINE_ERROR] Gemini Bridge not established. Real-world engineering requires active AI connection.";

            // OMEGA_CONTEXT: System metadata for maximum fidelity
            string buildSpecs = "Target: Android 16 (Baklava), API: 36, Rev: 36.1.0, Logic: OMEGA_CORE";
            
            string prompt = $"### BOREALIS OMEGA SYSTEM ENGINEERING (ALEPPO UNIVERSITY STANDARDS) ###\n" +
                            $"System Context: {buildSpecs}\n" +
                            $"Goal: Generate high-fidelity research code for {className}.\n" +
                            $"Package: {packageName}\n" +
                            $"Requirements: {specificRequirements}\n" +
                            $"Directives: NO MOCKING. Use real 'android.federatedcompute', 'android.adservices', and Binder IPC hooks. " +
                            $"The code must be production-ready for Android 16 system images.\n" +
                            $"Output: Raw Java/Kotlin code (Elite Grade).";

            string code = await _geminiService.ProcessResearchQueryAsync(prompt);
            string cleanCode = CleanCode(code);
            
            var outputFile = Path.Combine(_outputPath, $"{className}.java");
            await File.WriteAllTextAsync(outputFile, cleanCode);
            
            return $"✅ Generated High-Fidelity Activity: {outputFile}\n\n```java\n{cleanCode}\n```";
        }

        public async Task<string> GenerateFederatedComputeServiceAsync(string className, string specificRequirements = "")
        {
            if (_geminiService == null) return "❌ [PIPELINE_ERROR] Gemini Bridge not established.";

            string prompt = $"### BOREALIS AI FEDERATED COMPUTE ENGINEERING ###\n" +
                            $"Goal: Generate an Android 16 Federated Compute Service.\n" +
                            $"Class: {className}\n" +
                            $"Requirements: {specificRequirements}\n" +
                            $"Standards: API 36, FederatedCompute API, Isolated Process.\n" +
                            $"Output: Raw Java code ONLY.";

            string code = await _geminiService.ProcessResearchQueryAsync(prompt);
            string cleanCode = CleanCode(code);
            
            var outputFile = Path.Combine(_outputPath, $"{className}.java");
            await File.WriteAllTextAsync(outputFile, cleanCode);
            
            return $"✅ Generated Federated Compute Service: {outputFile}\n\n```java\n{cleanCode}\n```";
        }
        
        public async Task<string> GenerateKotlinResearchModuleAsync(string moduleName, string specificRequirements = "")
        {
            if (_geminiService == null) return "❌ [PIPELINE_ERROR] Gemini Bridge not established. Academic Kotlin generation requires AI validation.";

            string prompt = $"### BOREALIS AI ENGINEERING TASK ###\n" +
                            $"Goal: Generate an advanced Android 16 Kotlin research module.\n" +
                            $"Name: {moduleName}\n" +
                            $"Requirements: {specificRequirements}\n" +
                            $"Standards: API 36 Official, Modern AndroidX.\n" +
                            $"Output: Raw Kotlin code ONLY.";

            string code = await _geminiService.ProcessResearchQueryAsync(prompt);
            string cleanCode = CleanCode(code);
            
            var outputFile = Path.Combine(_outputPath, $"{moduleName}.kt");
            await File.WriteAllTextAsync(outputFile, cleanCode);
            
            return $"✅ Generated Advanced Kotlin Module: {outputFile}\n\n```kotlin\n{cleanCode}\n```";
        }

        public List<string> GetAvailableTemplates()
        {
            string templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "ResearchPayloadTools", "templates");
            if (!Directory.Exists(templatesPath)) return new List<string>();
            return Directory.GetDirectories(templatesPath).Select(Path.GetFileName).ToList();
        }

        public async Task<string> GenerateFromTemplateAsync(string templateName, string className, string packageName, string requirements)
        {
            if (_geminiService == null) return "❌ [PIPELINE_ERROR] Gemini Bridge not established.";

            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "ResearchPayloadTools", "templates", templateName);
            if (!Directory.Exists(templatePath)) return $"❌ [ERROR] Template '{templateName}' not found.";

            string manifest = await File.ReadAllTextAsync(Path.Combine(templatePath, "AndroidManifest.xml"));
            string aidlContext = "";
            string aidlDir = Path.Combine(templatePath, "aidl");
            if (Directory.Exists(aidlDir))
            {
                var aidlFiles = Directory.GetFiles(aidlDir, "*.aidl", SearchOption.AllDirectories);
                foreach (var f in aidlFiles) aidlContext += $"\nAIDL Interface [{Path.GetFileName(f)}]:\n{await File.ReadAllTextAsync(f)}\n";
            }

            string prompt = $"### BOREALIS OMEGA TEMPLATE ENGINEERING ###\n" +
                            $"Template: {templateName}\n" +
                            $"Base Manifest:\n{manifest}\n" +
                            $"{aidlContext}\n" +
                            $"Target Class: {className}\n" +
                            $"Target Package: {packageName}\n" +
                            $"Requirements: {requirements}\n" +
                            $"Task: Generate the Main Activity or Service logic that implements these high-fidelity research nodes. " +
                            $"Leverage the AIDL interfaces and Native hooks provided in the template context. " +
                            $"Provide ONLY the raw code.";

            string code = await _geminiService.ProcessResearchQueryAsync(prompt);
            string cleanCode = CleanCode(code);
            
            // Create project-specific directory
            string projectPath = Path.Combine(_outputPath, templateName + "_" + DateTime.Now.Ticks);
            Directory.CreateDirectory(projectPath);
            
            // Copy template files
            CopyDirectory(templatePath, projectPath);
            
            // Write generated code
            string srcDir = Path.Combine(projectPath, "src");
            Directory.CreateDirectory(srcDir);
            string ext = requirements.ToLower().Contains("kotlin") ? ".kt" : ".java";
            await File.WriteAllTextAsync(Path.Combine(srcDir, className + ext), cleanCode);
            
            return $"✅ Project Engineered from Template [{templateName}]: {projectPath}\n\n```" + (ext == ".kt" ? "kotlin" : "java") + $"\n{cleanCode}\n```";
        }

        public async Task<string> ApplyAIPolymorphicProcessingAsync(string sourceCode)
        {
            if (_geminiService == null) return sourceCode; // Fallback to original

            string prompt = $"### BOREALIS AI POLYMORPHIC ENGINE ###\n" +
                            $"Goal: Perform an advanced polymorphic rewrite of this Android/Kotlin code.\n" +
                            $"Instructions:\n" +
                            $"1. Change variable and method names to sound like legitimate business logic (e.g., UI rendering, database fetch).\n" +
                            $"2. Inject decoy logic (harmless math operations or fake API checks) to confuse static analysis.\n" +
                            $"3. DO NOT break the core functionality. The execution must remain identical.\n" +
                            $"4. Provide ONLY the raw code output (no markdown, no explanations).\n\n" +
                            $"TARGET SOURCE:\n" +
                            $"{sourceCode}";

            string newCode = await _geminiService.ProcessResearchQueryAsync(prompt);
            string cleanCode = CleanCode(newCode);
            
            // Validate that Gemini didn't return an error message instead of code
            if (cleanCode.Contains("[AI_ERROR]") || string.IsNullOrWhiteSpace(cleanCode))
                return sourceCode;
                
            return cleanCode;
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destinationDir));

            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceDir, destinationDir), true);
        }

        private string CleanCode(string input)
        {
            string result = input.Trim();
            if (result.StartsWith("```"))
            {
                var lines = result.Split('\n').ToList();
                if (lines.Count > 2)
                {
                    lines.RemoveAt(0);
                    if (lines.Last().StartsWith("```")) lines.RemoveAt(lines.Count - 1);
                    result = string.Join("\n", lines);
                }
            }
            return result;
        }
    }
}
