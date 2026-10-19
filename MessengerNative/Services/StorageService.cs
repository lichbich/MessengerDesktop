using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MessengerNative.Models;

namespace MessengerNative.Services
{
    public class StorageService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MessengerNative"
        );
        private static readonly string ThreadsFilePath = Path.Combine(AppDataFolder, "threads.json");
        private static readonly string MessagesFilePath = Path.Combine(AppDataFolder, "messages.json");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");

        public StorageService()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public List<ThreadItem> LoadThreads()
        {
            try
            {
                if (File.Exists(ThreadsFilePath))
                {
                    var json = File.ReadAllText(ThreadsFilePath);
                    return JsonSerializer.Deserialize<List<ThreadItem>>(json) ?? new List<ThreadItem>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load threads: {ex.Message}");
            }
            return new List<ThreadItem>();
        }

        public void SaveThreads(IEnumerable<ThreadItem> threads)
        {
            try
            {
                var json = JsonSerializer.Serialize(threads, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ThreadsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save threads: {ex.Message}");
            }
        }

        public Dictionary<string, List<ChatMessage>> LoadAllMessages()
        {
            try
            {
                if (File.Exists(MessagesFilePath))
                {
                    var json = File.ReadAllText(MessagesFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, List<ChatMessage>>>(json) 
                           ?? new Dictionary<string, List<ChatMessage>>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load messages: {ex.Message}");
            }
            return new Dictionary<string, List<ChatMessage>>();
        }

        public void SaveAllMessages(Dictionary<string, List<ChatMessage>> messagesMap)
        {
            try
            {
                var json = JsonSerializer.Serialize(messagesMap, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MessagesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save messages: {ex.Message}");
            }
        }
    }
}
