using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace GameVocal.Editor
{
    public class GameVocalApiClient
    {
        private const string BASE_URL = "https://api.gamevocal.com/api/v1";

        public event Action<string> OnError;

        public async Task<JObject> RequestAsync(string endpoint, string method = "GET", string payload = null)
        {
            string apiKey = GameVocalSettings.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                OnError?.Invoke("API Key is missing.");
                return null;
            }

            string url = BASE_URL + endpoint;
            using (UnityWebRequest www = new UnityWebRequest(url, method))
            {
                www.SetRequestHeader("Authorization", "Bearer " + apiKey);
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("X-GameVocal-Engine", "unity");

                if (!string.IsNullOrEmpty(payload) && method != "GET")
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                }

                www.downloadHandler = new DownloadHandlerBuffer();

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Delay(10);

                if (www.result != UnityWebRequest.Result.Success)
                {
                    OnError?.Invoke($"API Error ({www.responseCode}): {www.error}\n{www.downloadHandler.text}");
                    return null;
                }

                try
                {
                    return JObject.Parse(www.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke("Failed to parse JSON response: " + ex.Message);
                    return null;
                }
            }
        }
        
        public async Task<JArray> RequestArrayAsync(string endpoint, string method = "GET")
        {
            // Same logic as RequestAsync but returns JArray for endpoints returning arrays
            string apiKey = GameVocalSettings.ApiKey;
            if (string.IsNullOrEmpty(apiKey)) return null;

            string url = BASE_URL + endpoint;
            using (UnityWebRequest www = new UnityWebRequest(url, method))
            {
                www.SetRequestHeader("Authorization", "Bearer " + apiKey);
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("X-GameVocal-Engine", "unity");
                www.downloadHandler = new DownloadHandlerBuffer();

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Delay(10);

                if (www.result != UnityWebRequest.Result.Success) return null;

                try
                {
                    return JArray.Parse(www.downloadHandler.text);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
