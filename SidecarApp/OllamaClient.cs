using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SidecarApp
{
    /// <summary>
    /// Client for communicating with local Ollama instance.
    /// </summary>
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaClient(string baseUrl = "http://localhost:11434", string model = "llama2")
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _baseUrl = baseUrl;
            _model = model;
        }

        /// <summary>
        /// Generate AI translation suggestion based on source and current translation.
        /// </summary>
        public async Task<string> GenerateSuggestionAsync(string sourceText, string currentTranslation, string sourceLang = "en", string targetLang = "ko")
        {
            try
            {
                string prompt = BuildPrompt(sourceText, currentTranslation, sourceLang, targetLang);

                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.7,
                        top_p = 0.9,
                        max_tokens = 100
                    }
                };

                string json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(responseBody);

                return result?.response?.ToString()?.Trim() ?? "";
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Ollama] Connection failed: {ex.Message}");
                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Ollama] Request failed: {ex.Message}");
                return "";
            }
        }

        private string BuildPrompt(string sourceText, string currentTranslation, string sourceLang, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(currentTranslation))
            {
                // Initial translation request
                return $@"Translate the following text from {sourceLang} to {targetLang}. Provide only the translation without explanations.

Source: {sourceText}

Translation:";
            }
            else
            {
                // Continuation suggestion
                return $@"You are helping with translation from {sourceLang} to {targetLang}.

Source: {sourceText}
Current translation (incomplete): {currentTranslation}

Suggest how to continue or complete the translation. Provide only the continuation text, no explanations:";
            }
        }

        /// <summary>
        /// Check if Ollama is running and accessible.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
