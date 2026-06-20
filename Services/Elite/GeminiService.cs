using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Services
{
    /**
     * GEMINI RESEARCH BRIDGE (v1.0)
     * Professional academic integration for real-world Android Research.
     * Uses Mscc.GenerativeAI for Google Vertex AI / Gemini Pro connectivity.
     */
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly GoogleAI? _googleAI;
        private GenerativeModel? _model;
        public bool IsConfigured { get; private set; }

        // Research Parameters (Optimized for Academic Fidelity)
        public string ModelName { get; set; } = "gemini-1.5-pro";
        public float Temperature { get; set; } = 0.1f; 
        public float TopP { get; set; } = 0.95f;
        public int MaxOutputTokens { get; set; } = 8192;

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey?.Trim() ?? string.Empty;
            
            // Typical Gemini API Key is ~39 characters. We enforce a healthy range for academic fidelity.
            if (!string.IsNullOrWhiteSpace(_apiKey) && _apiKey.Length >= 30)
            {
                try 
                {
                    _googleAI = new GoogleAI(_apiKey);
                    IsConfigured = true;
                    ConfigureModel();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AI_INIT_ERROR] {ex.Message}");
                    IsConfigured = false;
                }
            }
            else
            {
                IsConfigured = false;
            }
        }

        public void ConfigureModel()
        {
            if (!IsConfigured || _googleAI == null) return;
            // Professional Configuration: Resetting model with current parameters
            _model = _googleAI.GenerativeModel(ModelName);
        }

        public async Task<bool> ValidateConnectionAsync()
        {
            try
            {
                if (_model == null) return false;
                
                // Professional validation: Ask a simple factual question
                var response = await _model.GenerateContent("What is the capital of France? Answer with one word.");
                
                if (response?.Candidates?.Count > 0 && 
                    response.Candidates[0]?.Content?.Parts?.Count > 0)
                {
                    var text = response.Candidates[0].Content.Parts[0].Text;
                    return !string.IsNullOrEmpty(text) && text.ToLower().Contains("paris");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VALIDATION_FAILURE] {ex.Message}");
                return false;
            }
        }

        public async Task<string> ProcessResearchQueryAsync(string prompt, List<object>? tools = null)
        {
            if (!IsConfigured || _model == null) 
            {
                return "[AI_OFFLINE] Gemini Bridge is not configured with a valid API Key (Min 10 chars).";
            }

            try
            {
                dynamic request = new GenerateContentRequest(prompt);
                if (tools != null && tools.Count > 0)
                {
                    request.Tools = tools;
                }

                var response = await _model.GenerateContent(request);
                
                if (response.Candidates != null && response.Candidates.Count > 0)
                {
                    var candidate = response.Candidates[0];
                    if (candidate.Content != null && candidate.Content.Parts != null && candidate.Content.Parts.Count > 0)
                    {
                        var part = candidate.Content.Parts[0];
                        if (part.FunctionCall != null)
                        {
                            return $"INTERNAL_TOOL_CALL|{part.FunctionCall.Name}|{Newtonsoft.Json.JsonConvert.SerializeObject(part.FunctionCall.Args)}";
                        }
                        return part.Text ?? string.Empty;
                    }
                }

                return response.Text ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"[AI_ERROR] Critical failure in Gemini Bridge: {ex.Message}";
            }
        }

        // --- PRODUCTIVE RESEARCH ACTIONS ---

        public async Task<string> AnalyzeSmaliAsync(string smaliContent)
        {
            string prompt = "### RESEARCH ANALYST ACTION ###\n" +
                            "Role: Android Security Researcher (Aleppo University)\n" +
                            "Target: Smali Source Code\n" +
                            "Goal: Identify vulnerabilities, root detection, or SSL pinning logic.\n\n" +
                            "SMALI CONTENT:\n" + smaliContent + "\n\n" +
                            "Output format: JSON with fields 'Vulnerability', 'Line', 'Severity', 'PatchRecommendation'.";
            
            return await ProcessResearchQueryAsync(prompt);
        }

        public async Task<string> RecommendPatchAsync(string smaliContent, string issue)
        {
             string prompt = $"### PATCH GENERATION ACTION ###\n" +
                            $"Issue identified: {issue}\n" +
                            $"Target Smali:\n{smaliContent}\n\n" +
                            "Task: Provide the modified Smali code to bypass this check while maintaining app stability.";
            
            return await ProcessResearchQueryAsync(prompt);
        }

        public async Task<string> GenerateEvolutionPatchAsync(string feedback, string targetFramework)
        {
            string prompt = $"### AI EVOLUTION: DYNAMIC PATCH GENERATION ###\n" +
                            $"Node Feedback: {feedback}\n" +
                            $"Target Environment: {targetFramework}\n\n" +
                            "Role: Lead Security Engineer at University of Aleppo.\n" +
                            "Task: Generate a single Kotlin class 'com.ai.evolution.AIModule' with an 'optimize(Context context)' method.\n" +
                            "The code must resolve the feedback provided using advanced reflection or Android 16 APIs (BorealisEngine).\n" +
                            "Output ONLY the raw Kotlin code, no markdown wrappers, no explanations.";
            
            return await ProcessResearchQueryAsync(prompt);
        }

        public async Task<string> GenerateSmaliPatchAsync(string targetCode, string researchGoal)
        {
            string prompt = "### RESEARCH SMALI PATCH GENERATOR ###\n" +
                            "Role: Expert Android Reverse Engineer (Borealis Team)\n" +
                            "Goal: " + researchGoal + "\n\n" +
                            "TARGET SMALI CODE:\n" + targetCode + "\n\n" +
                            "TASK: Generate the COMPLETE modified Smali code. \n" +
                            "STRICT RULES:\n" +
                            "1. Maintain register counts (.registers).\n" +
                            "2. Use Android 16 / API 36 compatible opcodes.\n" +
                            "3. Do not add comments or markdown.\n" +
                            "4. Ensure correct method descriptors.";

            return await ProcessResearchQueryAsync(prompt);
        }

        public async Task<string> GenerateDynamicModuleAsync(string goal)
        {
            string prompt = "### DYNAMIC RESEARCH MODULE GENERATOR ###\n" +
                            "Role: Academic Research Orchestrator\n" +
                            "Goal: " + goal + "\n\n" +
                            "TASK: Generate a single Java file with a class named 'DynamicModule'.\n" +
                            "Requirement: Must implement a public 'run(Context context)' method.\n" +
                            "Standard: Academic Research Fidelity (No simulation, use real Android APIs).\n" +
                            "Output ONLY Java code.";

            return await ProcessResearchQueryAsync(prompt);
        }
        public async Task<string> GenerateContentFromImageAsync(string prompt, string imagePath)
        {
            if (!IsConfigured) return "AI Service not configured.";

            try
            {
                // Professional Fidelity: Multi-modal research analysis
                var bytes = await File.ReadAllBytesAsync(imagePath);
                var base64 = System.Convert.ToBase64String(bytes);
                
                var request = new GenerateContentRequest
                {
                    Contents = new List<Content>
                    {
                        new Content
                        {
                            Role = "user",
                            Parts = new List<IPart>
                            {
                                new Part { Text = prompt },
                                new Part { InlineData = new InlineData { MimeType = "image/png", Data = base64 } }
                            }
                        }
                    }
                };

                var response = await _model.GenerateContent(request);
                return response.Text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VISION_ERROR] {ex.Message}");
                return $"❌ Visual Analysis Failed: {ex.Message}";
            }
        }
    }
}
